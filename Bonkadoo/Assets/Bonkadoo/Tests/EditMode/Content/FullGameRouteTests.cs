using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Narrative;
using Bonkadoo.Tests.Support;
using NUnit.Framework;

namespace Bonkadoo.Tests.Content
{
    /// <summary>
    /// Plays every authored route through GameFlow + InteractionController exactly as the UI does
    /// (tap = default verb per hotspot kind, select item then tap target, exits by tapping), including
    /// conversations, chapter transitions and endings. Proves wiring and reachability, not fun.
    /// </summary>
    public class FullGameRouteTests
    {
        private sealed class Ticks : ITickSource { public long Now { get; set; } = 1; }

        private static int _nonce;

        private static GameFlow NewFlow(InMemoryFileStore files = null) =>
            new GameFlow(TestContent.Load(), new SaveService(files ?? new InMemoryFileStore()), new Ticks(), () => "n" + ++_nonce);

        /// <summary>Plays queued conversations to the end, picking the given option index at each choice.</summary>
        private static void DrainConversations(GameFlow flow, int option = 0)
        {
            var guard = 0;
            while (flow.Conversation.IsActive && guard++ < 2000)
            {
                if (flow.Conversation.State == ConversationState.Options)
                {
                    var i = Enumerable.Range(0, flow.Conversation.Options.Count)
                        .Where(x => flow.Conversation.Options[x].Available).DefaultIfEmpty(0)
                        .Select(x => System.Math.Min(option, flow.Conversation.Options.Count - 1)).First();
                    flow.Conversation.Choose(i);
                }
                else flow.Conversation.Advance();
            }
            Assert.IsFalse(flow.Conversation.IsActive, "conversation did not finish");
        }

        private static HotspotInfo Info(GameFlow flow, string sceneId, string hotspotId)
        {
            var h = flow.Content.Scenes.FindHotspot(sceneId, hotspotId);
            Assert.IsNotNull(h, $"{hotspotId} not in {sceneId}");
            Assert.IsTrue(h.IsVisible(flow.Session.State), $"{hotspotId} hidden when needed");
            var kind = h.Type == HotspotType.Npc ? HotspotKind.Npc : h.Type == HotspotType.Exit ? HotspotKind.Exit : HotspotKind.Prop;
            return new HotspotInfo(h.Id, kind, exitTo: h.ExitTo, startUnlocked: h.StartUnlocked);
        }

        /// <summary>Walks through open exits (BFS) to reach the puzzle's scene, like a player would.</summary>
        private static void WalkTo(GameFlow flow, string target)
        {
            var guard = 0;
            while (flow.Session.State.CurrentSceneId != target && guard++ < 10)
            {
                var path = ShortestExitPath(flow, flow.Session.State.CurrentSceneId, target);
                Assert.IsNotNull(path, $"no open path from {flow.Session.State.CurrentSceneId} to {target}");
                flow.Controller.Tap(Info(flow, flow.Session.State.CurrentSceneId, path));
                DrainConversations(flow);
            }
            Assert.AreEqual(target, flow.Session.State.CurrentSceneId);
        }

        private static string ShortestExitPath(GameFlow flow, string from, string to)
        {
            var queue = new Queue<(string scene, string firstExit)>();
            var seen = new HashSet<string> { from };
            queue.Enqueue((from, null));
            while (queue.Count > 0)
            {
                var (scene, first) = queue.Dequeue();
                foreach (var h in flow.Content.Scenes.Scene(scene).Hotspots)
                {
                    if (!h.IsExitOpen(flow.Session.State) || !seen.Add(h.ExitTo)) continue;
                    var firstExit = first ?? h.Id;
                    if (h.ExitTo == to) return firstExit;
                    queue.Enqueue((h.ExitTo, firstExit));
                }
            }
            return null;
        }

        private static void PlayStep(GameFlow flow, string scene, string verb, string target, string source)
        {
            InteractionOutcome outcome;
            if (verb == "UseItem")
            {
                Assert.IsTrue(flow.Session.State.HasItem(source), $"{source} not held for {target}");
                Assert.IsTrue(flow.Controller.ToggleItem(source));
                outcome = flow.Controller.Tap(Info(flow, scene, target));
            }
            else
            {
                var info = Info(flow, scene, target);
                Assert.AreEqual(verb, InteractionController.DefaultVerb(info.Kind).ToString(),
                    $"{target}: tapping a {info.Kind} cannot produce {verb}");
                outcome = flow.Controller.Tap(info);
            }
            Assert.AreEqual(OutcomeStatus.Committed, outcome.Status, $"{verb} {target}: {outcome.Status} {outcome.ErrorMessage}");
            DrainConversations(flow);
        }

        private static GameFlow PlayWholeGame(string route, string door, int option)
        {
            var flow = NewFlow();
            flow.NewGame();
            DrainConversations(flow, option);
            var puzzles = flow.Content.Rules;
            foreach (var chapter in flow.Content.Chapters)
            {
                if (chapter.Id != flow.CurrentChapter.Id)
                {
                    Assert.AreEqual(chapter.Id, flow.NextChapter(flow.CurrentChapter.Id).Id);
                    flow.StartChapter(chapter.Id);
                    DrainConversations(flow, option);
                }
                foreach (var pid in chapter.PuzzleIds)
                {
                    Assert.IsTrue(puzzles.TryGetPuzzle(pid, out var puzzle));
                    WalkTo(flow, puzzle.SceneId);
                    if (pid == "ch05.p09")
                    {
                        flow.Controller.Tap(Info(flow, puzzle.SceneId, "prop.ch05.door_" + door));
                        DrainConversations(flow, option);
                    }
                    var solution = Solutions(pid).First(s => s.Item1 == route);
                    foreach (var (verb, target, source) in solution.Item2) PlayStep(flow, puzzle.SceneId, verb, target, source);
                    Assert.IsTrue(flow.Session.State.HasFlag(puzzle.CompletionFlag), pid);
                    Assert.IsTrue(flow.Session.State.HasEvent($"evt.{pid}.solved.{route}"), pid);
                }
                Assert.AreEqual(chapter.Id, flow.ConsumeChapterComplete(), $"{chapter.Id} completion not signalled");
            }
            Assert.IsTrue(flow.ConsumeGameComplete());
            return flow;
        }

        private static readonly Dictionary<string, List<(string, List<(string, string, string)>)>> SolutionCache =
            new Dictionary<string, List<(string, List<(string, string, string)>)>>();

        private static List<(string, List<(string, string, string)>)> Solutions(string pid)
        {
            if (SolutionCache.Count == 0)
            {
                var json = Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText(TestContent.ContentPath("puzzles/puzzles.json")));
                foreach (var p in json)
                    SolutionCache[(string)p["id"]] = p["solutions"].Select(s => (((string)s["id"]).Split('.').Last(),
                        s["steps"].Select(st => ((string)st["verb"], (string)st["targetId"], (string)st["sourceId"])).ToList())).ToList();
            }
            return SolutionCache[pid];
        }

        [Test]
        public void AllRouteAThroughTheWholeGamePatchEnding()
        {
            var flow = PlayWholeGame("a", "patch", 0);
            Assert.AreEqual(Ending.Patch, flow.CurrentEnding);
            Assert.IsTrue(flow.GameComplete);
        }

        [Test]
        public void AllRouteBThroughTheWholeGameWalkAwayEnding()
        {
            var flow = PlayWholeGame("b", "walk", 1);
            Assert.AreEqual(Ending.WalkAway, flow.CurrentEnding);
        }

        [TestCase("rewrite", "npc.mai_ch05", "Talk", Ending.Rewrite)]
        [TestCase("reveal", "prop.ch05.audit_terminal", "Inspect", Ending.Reveal)]
        public void GatedEndingsReachableFromWorstMetersViaCorrective(string door, string corrective, string verb, Ending expected)
        {
            // TC-008: reachable from a legal low-meter fixture
            var flow = PlayWholeGame("a", "patch", 0);
            var c = TestContent.Load();
            var state = WorldState.Restore(c.World, flow.Session.Snapshot());
            var engine = new InteractionEngine(c.Rules);
            var session = new GameSession(c.World, engine, new InMemoryEventStore(), state);
            // rewind the finale: un-complete p09 and drop meters to the floor
            session.SetFact("flag.ch05.p09.complete", FactValue.Of(false), "fx1", 9);
            session.SetFact("flag.ending", FactValue.Of(""), "fx2", 9);
            var low = new Dictionary<string, string> { [PayloadKeys.Key] = "public_trust", [PayloadKeys.Value] = "-3" };
            WorldStateProjector.Apply(state, new WorldEvent("fx3", WorldEventTypes.MetricSet, low, 9, "fx"));
            WorldStateProjector.Apply(state, new WorldEvent("fx4", WorldEventTypes.MetricSet,
                new Dictionary<string, string> { [PayloadKeys.Key] = "evidence_count", [PayloadKeys.Value] = "0" }, 9, "fx"));
            var scene = state.CurrentSceneId;
            InteractionOutcome Try(string id, InteractionVerb v, string target) =>
                session.Interact(new InteractionRequest(id, v, null, target, scene, 10));

            Try("d1", InteractionVerb.Inspect, "prop.ch05.door_" + door);
            Assert.AreNotEqual(expected.ToString().ToUpperInvariant(), state.GetFlag("flag.ending")?.Serialize(), "gate holds at low meters");
            Try("c1", verb == "Talk" ? InteractionVerb.Talk : InteractionVerb.Inspect, corrective);
            Try("d2", InteractionVerb.Inspect, "prop.ch05.door_" + door);
            Assert.AreEqual(expected.ToString().ToUpperInvariant(), state.GetFlag("flag.ending")?.Serialize());
        }

        [Test]
        public void ReplayBranchLeavesMainSaveUntouched()
        {
            // TC-010
            var files = new InMemoryFileStore();
            var flow = NewFlow(files);
            flow.NewGame();
            DrainConversations(flow);
            foreach (var pid in flow.Content.Chapters[0].PuzzleIds)
            {
                flow.Content.Rules.TryGetPuzzle(pid, out var p);
                WalkTo(flow, p.SceneId);
                foreach (var (verb, target, source) in Solutions(pid).First(s => s.Item1 == "a").Item2) PlayStep(flow, p.SceneId, verb, target, source);
            }
            flow.SaveNow();
            var mainBefore = files.Read("main.0.sav").Concat(files.Exists("main.1.sav") ? files.Read("main.1.sav") : new byte[0]).ToArray();

            Assert.IsTrue(flow.CanReplay("ch01"));
            Assert.IsTrue(flow.StartReplay("ch01"));
            DrainConversations(flow);
            Assert.IsTrue(flow.IsReplay);
            Assert.IsFalse(flow.Session.State.HasFlag("flag.ch01.p01.complete"), "replay starts at chapter start");
            var p01 = Solutions("ch01.p01").First(s => s.Item1 == "b").Item2;
            foreach (var (verb, target, source) in p01) PlayStep(flow, "scene.ch01.office", verb, target, source);

            var mainAfter = files.Read("main.0.sav").Concat(files.Exists("main.1.sav") ? files.Read("main.1.sav") : new byte[0]).ToArray();
            CollectionAssert.AreEqual(mainBefore, mainAfter, "main slot untouched during replay");
            Assert.IsTrue(flow.ExitReplay());
            Assert.IsTrue(flow.Session.State.HasEvent("evt.ch01.p01.solved.a"));
            Assert.IsFalse(flow.Session.State.HasEvent("evt.ch01.p01.solved.b"));
        }

        [Test]
        public void ContinueResumesMidChapterIdempotently()
        {
            var files = new InMemoryFileStore();
            var flow = NewFlow(files);
            flow.NewGame();
            DrainConversations(flow);
            foreach (var (verb, target, source) in Solutions("ch01.p01").First(s => s.Item1 == "a").Item2)
                PlayStep(flow, "scene.ch01.office", verb, target, source);

            var resumed = NewFlow(files);
            Assert.IsTrue(resumed.Continue());
            Assert.AreEqual("scene.ch01.office", resumed.Session.State.CurrentSceneId);
            Assert.IsTrue(resumed.Session.State.HasFlag("flag.ch01.p01.complete"));
            Assert.IsTrue(resumed.Session.State.HasItem("item.ch01.dismissal_notice"));
            Assert.AreEqual("ch01.p02", resumed.Hints.ActivePuzzle.Id);
        }

        [Test]
        public void EveryYarnNodeCompilesAndPlaysToTheEnd()
        {
            var flow = NewFlow();
            flow.NewGame();
            DrainConversations(flow);
            foreach (var node in flow.Narrative.Nodes)
            {
                flow.Conversation.Enqueue(node);
                DrainConversations(flow);
            }
            Assert.Greater(flow.Narrative.Nodes.Count, 15);
        }

        [Test]
        public void EveryDialogueKeyShownIsLocalizedInBothLanguages()
        {
            var c = TestContent.Load();
            foreach (var rule in c.Rules.OrderedRules)
            foreach (var e in rule.Effects.OfType<QueueDialogueEffect>())
            foreach (var lang in new[] { "en", "vi" })
            {
                Assert.IsTrue(c.Localization[lang].ContainsKey(e.Key), $"{lang}:{e.Key}");
                if (e.Speaker != null) Assert.IsTrue(c.Localization[lang].ContainsKey("ui.speaker." + e.Speaker), e.Speaker);
            }
            foreach (var kv in c.DialogueSpeakers)
                Assert.IsTrue(c.Localization["vi"].ContainsKey(kv.Key), kv.Key);
        }
    }
}
