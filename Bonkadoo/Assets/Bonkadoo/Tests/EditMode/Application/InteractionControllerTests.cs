using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Tests.Support;
using NUnit.Framework;

namespace Bonkadoo.Tests.Application
{
    public class InteractionControllerTests
    {
        private sealed class FixedTicks : ITickSource
        {
            public long Now { get; set; } = 10;
        }

        private GameSession _session;
        private InteractionController _controller;
        private readonly List<InteractionRequest> _sent = new List<InteractionRequest>();

        private static readonly HotspotInfo Desk = new HotspotInfo("prop.desk", HotspotKind.Prop);
        private static readonly HotspotInfo Boss = new HotspotInfo("npc.vuong", HotspotKind.Npc);

        [SetUp]
        public void SetUp()
        {
            var def = TestContent.World();
            var rules = new RuleSet(new[]
            {
                T.Rule("r.take", 10, new AllPredicate(new Predicate[] { T.Target("prop.desk"), new RequestVerbPredicate(InteractionVerb.Inspect) }),
                    new AddItemEffect("item.stamp")),
                T.Rule("r.stamp", 20, new AllPredicate(new Predicate[]
                {
                    new RequestVerbPredicate(InteractionVerb.UseItem), new RequestSourcePredicate("item.stamp"), T.Target("npc.vuong"),
                }), T.Flag("flag.stamped")),
                T.Rule("r.choice", 5, new FlagEqualsPredicate("input.test.choice", FactValue.Of("b")), T.Flag("flag.chose_b")),
            });
            _session = new GameSession(def, new InteractionEngine(rules), new InMemoryEventStore(), T.StateIn(def, "scene.test"));
            _controller = new InteractionController(_session, new RequestIdFactory("run1"), new FixedTicks());
            _sent.Clear();
            _controller.Resolved += (r, o) => _sent.Add(r);
        }

        [Test]
        public void TapUsesDefaultVerbPerHotspotKind()
        {
            _controller.Tap(Desk);
            _controller.Tap(Boss);
            CollectionAssert.AreEqual(new[] { InteractionVerb.Inspect, InteractionVerb.Talk }, _sent.Select(r => r.Verb).ToArray());
            Assert.IsTrue(_sent.All(r => r.SceneId == "scene.test" && r.Tick == 10));
        }

        [Test]
        public void SelectThenTapEqualsDrag()
        {
            _controller.Tap(Desk); // picks up the stamp
            Assert.IsTrue(_controller.ToggleItem("item.stamp"));
            var viaTap = _controller.Tap(Boss);
            Assert.AreEqual("r.stamp", viaTap.MatchedRuleId);
            Assert.IsNull(_controller.SelectedItemId, "selection consumed");

            var tapReq = _sent.Last();
            _controller.DragItemOnto("item.stamp", Boss);
            var dragReq = _sent.Last();
            Assert.AreEqual(tapReq.Verb, dragReq.Verb);
            Assert.AreEqual(tapReq.SourceId, dragReq.SourceId);
            Assert.AreEqual(tapReq.TargetId, dragReq.TargetId);
        }

        [Test]
        public void CannotSelectOrDragUnheldItem()
        {
            Assert.IsFalse(_controller.ToggleItem("item.stamp"));
            Assert.IsNull(_controller.DragItemOnto("item.stamp", Boss));
            Assert.IsEmpty(_sent);
        }

        [Test]
        public void SelectionIsReversible()
        {
            _controller.Tap(Desk);
            var changes = new List<string>();
            _controller.SelectionChanged += changes.Add;
            _controller.ToggleItem("item.stamp");
            _controller.ToggleItem("item.stamp");
            CollectionAssert.AreEqual(new[] { "item.stamp", null }, changes);
        }

        [Test]
        public void OptionHotspotSendsTransientInput()
        {
            var option = new HotspotInfo("option.test.b", HotspotKind.Option, "input.test.choice", "b");
            Assert.AreEqual("r.choice", _controller.Tap(option).MatchedRuleId);
            Assert.IsNull(_session.State.GetFlag("input.test.choice"));
        }

        [Test]
        public void RequestIdsAreUniqueAndOrdered()
        {
            for (var i = 0; i < 5; i++) _controller.Tap(Boss);
            CollectionAssert.AreEqual(new[] { "run1-1", "run1-2", "run1-3", "run1-4", "run1-5" }, _sent.Select(r => r.RequestId).ToArray());
        }

        [Test]
        public void HotspotInputKeyMustBeTransient()
        {
            Assert.Throws<System.ArgumentException>(() => new HotspotInfo("x", HotspotKind.Option, "flag.persistent", "a"));
        }

        [Test]
        public void AutosaveRunsOnCommitAndSceneChangeButNotOnFallback()
        {
            var files = new InMemoryFileStore();
            var checkpoints = new CheckpointService(new SaveService(files), _session.Definition,
                new InteractionEngine(new RuleSet(new Rule[0])));
            using (var autosave = new AutosaveCoordinator(_session, checkpoints, null))
            {
                _controller.Tap(Desk);                             // commits
                _controller.Tap(new HotspotInfo("nothing", HotspotKind.Prop)); // fallback
                _session.EnterScene("scene.next", "move-1", 11);   // scene change
                Assert.AreEqual(2, autosave.SaveCount);
                autosave.OnApplicationBackground();
                Assert.AreEqual(3, autosave.SaveCount);
            }
            Assert.IsTrue(checkpoints.TryRestore("main", out var restored, out _));
            Assert.AreEqual("scene.next", restored.State.CurrentSceneId);
            Assert.IsTrue(restored.State.HasItem("item.stamp"));
        }

        [Test]
        public void AutosaveFailureIsReportedNotThrown()
        {
            var files = new InMemoryFileStore { FailNextWriteAfterBytes = 3 };
            var checkpoints = new CheckpointService(new SaveService(files), _session.Definition,
                new InteractionEngine(new RuleSet(new Rule[0])));
            using (var autosave = new AutosaveCoordinator(_session, checkpoints, null))
            {
                System.Exception reported = null;
                autosave.SaveFailed += e => reported = e;
                Assert.DoesNotThrow(() => _controller.Tap(Desk));
                Assert.IsNotNull(reported);
                Assert.IsTrue(_session.State.HasItem("item.stamp"), "gameplay state unaffected by save failure");
            }
        }
    }
}
