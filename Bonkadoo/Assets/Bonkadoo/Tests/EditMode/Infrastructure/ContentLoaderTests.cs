using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Bonkadoo.Tests.Infrastructure
{
    public class ContentLoaderTests
    {
        [Test]
        public void KitContentLoads()
        {
            var c = TestContent.Load();
            Assert.AreEqual(45, c.Rules.Puzzles.Count);
            Assert.AreEqual(90, c.Rules.OrderedRules.Count);
            Assert.AreEqual(5, c.Chapters.Count);
            Assert.AreEqual(90, c.EventTypes.Count);
            Assert.AreEqual(5, c.World.Metrics.Count);
            CollectionAssert.AreEquivalent(new[] { "en", "vi" }, c.Localization.Keys);
        }

        [Test]
        public void PrerequisitesGateRules()
        {
            var c = TestContent.Load();
            var session = NewSession(c);
            session.EnterScene("scene.ch01.01", "boot", 0);
            // p02 needs p01 complete; choosing p02's option first must not solve it
            var outcome = session.Interact(Choose("ch01.p02", "a", "scene.ch01.01", "r1"));
            Assert.AreEqual(OutcomeStatus.Fallback, outcome.Status);
            Assert.AreEqual("dlg.ch01.p02.fail", outcome.FeedbackKey);
        }

        // engine-executable seed: proves every authored route resolves in order, not that the puzzle is fun or final
        [Test]
        public void EverySeedRouteIsReachableInChapterOrder([Values("a", "b")] string route)
        {
            var c = TestContent.Load();
            foreach (var chapter in c.Chapters)
            {
                var session = NewSession(c);
                var n = 0;
                foreach (var pid in chapter.PuzzleIds)
                {
                    Assert.IsTrue(c.Rules.TryGetPuzzle(pid, out var puzzle), pid);
                    session.EnterScene(puzzle.SceneId, $"{chapter.Id}-scene-{n}", n);

                    var miss = session.Interact(new InteractionRequest($"{pid}-miss", InteractionVerb.Tap, null, "nothing.here",
                        puzzle.SceneId, n));
                    Assert.AreEqual(OutcomeStatus.Fallback, miss.Status, pid);

                    var outcome = session.Interact(Choose(pid, route, puzzle.SceneId, $"{pid}-{route}"));
                    Assert.AreEqual(OutcomeStatus.Committed, outcome.Status, $"{pid} route {route}: {outcome.ErrorMessage}");
                    Assert.AreEqual($"rule.{pid}.{route}", outcome.MatchedRuleId);
                    Assert.IsTrue(session.State.HasEvent($"evt.{pid}.solved.{route}"), pid);
                    Assert.IsTrue(session.State.HasFlag(puzzle.CompletionFlag), pid);
                    Assert.AreEqual($"dlg.{pid}.success.{route}", outcome.FeedbackKey);

                    var again = session.Interact(Choose(pid, route == "a" ? "b" : "a", puzzle.SceneId, $"{pid}-again"));
                    Assert.AreNotEqual(OutcomeStatus.Committed, again.Status, $"{pid} solvable twice");
                    n++;
                }
            }
        }

        [TestCase("{\"eval\": \"System.IO.File.Delete('x')\"}")]
        [TestCase("{\"flagEquals\": {\"key\": \"f\"}}")]
        [TestCase("{\"all\": [], \"any\": []}")]
        [TestCase("{\"requestVerb\": \"Explode\"}")]
        [TestCase("[]")]
        public void RejectsNonAllowlistedOrMalformedPredicates(string json)
        {
            Assert.Throws<ContentException>(() => ContentLoader.ParsePredicate(JToken.Parse(json), "test"));
        }

        [TestCase("{\"setFlag\": {\"key\": \"input.x\", \"value\": true}}")]
        [TestCase("{\"runScript\": {\"code\": \"x\"}}")]
        [TestCase("{\"adjustMetric\": {\"key\": \"trust_bop\", \"delta\": \"1\"}}")]
        public void RejectsNonAllowlistedOrMalformedEffects(string json)
        {
            Assert.Throws<ContentException>(() => ContentLoader.ParseEffect(JToken.Parse(json), "test"));
        }

        [Test]
        public void ParsesEveryAllowlistedOperator()
        {
            var predicate = JToken.Parse(@"{""all"": [
                {""any"": [{""hasItem"": {""itemId"": ""i""}}, {""not"": {""eventOccurred"": {""type"": ""evt.x""}}}]},
                {""metricAtLeast"": {""key"": ""trust_bop"", ""value"": 1}},
                {""npcDispositionAtLeast"": {""npcId"": ""npc.a"", ""value"": -1}},
                {""currentScene"": ""scene.x""}, {""requestVerb"": ""Talk""},
                {""requestTarget"": ""t""}, {""requestSource"": ""s""}, {""flagEquals"": {""key"": ""f"", ""value"": 3}}]}");
            Assert.IsInstanceOf<AllPredicate>(ContentLoader.ParsePredicate(predicate, "p"));

            foreach (var e in new[]
                     {
                         @"{""setFlag"": {""key"": ""f"", ""value"": ""x""}}", @"{""adjustMetric"": {""key"": ""k"", ""delta"": -1}}",
                         @"{""addItem"": {""itemId"": ""i""}}", @"{""removeItem"": {""itemId"": ""i""}}",
                         @"{""emitEvent"": {""type"": ""evt.x""}}", @"{""queueDialogue"": {""key"": ""d""}}",
                         @"{""queueAnimation"": {""cue"": ""c""}}", @"{""unlockExit"": {""exitId"": ""x""}}",
                         @"{""setNpcDisposition"": {""npcId"": ""n"", ""value"": 1}}",
                     })
                Assert.IsNotNull(ContentLoader.ParseEffect(JToken.Parse(e), "e"), e);
        }

        [Test]
        public void CrossCheckCatchesUnknownEventAndMissingDialogue()
        {
            var src = TestContent.Sources();
            var puzzles = JArray.Parse(src.PuzzlesJson);
            var effects = (JArray)puzzles[0]["rules"][0]["effects"];
            effects[1]["emitEvent"]["type"] = "evt.does.not.exist";
            effects[2]["queueDialogue"]["key"] = "dlg.missing";
            src.PuzzlesJson = puzzles.ToString();
            var ex = Assert.Throws<ContentException>(() => ContentLoader.Load(src));
            StringAssert.Contains("evt.does.not.exist", ex.Message);
            StringAssert.Contains("dlg.missing", ex.Message);
        }

        private static GameSession NewSession(GameContent c) =>
            new GameSession(c.World, new InteractionEngine(c.Rules), new InMemoryEventStore());

        private static InteractionRequest Choose(string puzzleId, string route, string scene, string requestId) =>
            new InteractionRequest(requestId, InteractionVerb.UseItem, null, $"option.{puzzleId}.{route}", scene, 1,
                new Dictionary<string, FactValue> { [$"input.{puzzleId}.choice"] = FactValue.Of(route) });
    }
}
