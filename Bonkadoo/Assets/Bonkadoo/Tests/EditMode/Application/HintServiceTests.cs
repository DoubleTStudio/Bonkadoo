using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Tests.Support;
using NUnit.Framework;

namespace Bonkadoo.Tests.Application
{
    public class HintServiceTests
    {
        private sealed class Ticks : ITickSource { public long Now => 5; }

        private GameSession _session;
        private InteractionController _controller;
        private HintService _hints;

        [SetUp]
        public void SetUp()
        {
            var def = TestContent.World();
            var p1 = new PuzzleDefinition("ch99.p01", "scene.test", new[] { "door" }, new string[0], "dlg.p1.fail", new[] { "h1", "h2", "h3" });
            var p2 = new PuzzleDefinition("ch99.p02", "scene.test", new[] { "door" }, new[] { "flag.ch99.p01.complete" }, "dlg.p2.fail", new[] { "k1", "k2", "k3" });
            var rules = new RuleSet(new[] { T.Rule("solve", 10, T.Target("door"), T.Flag("flag.ch99.p01.complete")) }, new[] { p1, p2 });
            _session = new GameSession(def, new InteractionEngine(rules), new InMemoryEventStore(), T.StateIn(def, "scene.test"));
            _controller = new InteractionController(_session, new RequestIdFactory("h"), new Ticks());
            _hints = new HintService(_session, rules, _controller);
        }

        [Test]
        public void LadderClimbsToThreeAndStays()
        {
            Assert.AreEqual("h1", _hints.RevealNext("r1", 1));
            Assert.AreEqual("h2", _hints.RevealNext("r2", 1));
            Assert.AreEqual("h3", _hints.RevealNext("r3", 1));
            Assert.AreEqual("h3", _hints.RevealNext("r4", 1));
            Assert.AreEqual(3, _hints.LevelFor("ch99.p01"));
        }

        [Test]
        public void HintLevelIsJournaledAndActivePuzzleAdvances()
        {
            _hints.RevealNext("r1", 1);
            Assert.AreEqual(FactValue.Of(1), _session.State.GetFlag("hint.ch99.p01"));
            _controller.Tap(new HotspotInfo("door", HotspotKind.Prop));
            Assert.AreEqual("ch99.p02", _hints.ActivePuzzle.Id);
            Assert.AreEqual("k1", _hints.RevealNext("r2", 1));
        }

        [Test]
        public void NudgeAfterThreeFailuresOrIdleButNoAutoReveal()
        {
            for (var i = 0; i < 3; i++) _controller.Tap(new HotspotInfo("wall", HotspotKind.Prop));
            Assert.IsTrue(_hints.NudgeAvailable);
            Assert.AreEqual(0, _hints.LevelFor("ch99.p01"), "nudge never reveals by itself");
            _hints.RevealNext("r", 1);
            Assert.IsFalse(_hints.NudgeAvailable);
            _hints.Tick(HintService.NudgeIdleSeconds + 1);
            Assert.IsTrue(_hints.NudgeAvailable);
        }

        [Test]
        public void NoHintWhenNoActivePuzzle()
        {
            _session.EnterScene("scene.empty", "e1", 2);
            Assert.IsNull(_hints.RevealNext("r", 1));
        }
    }
}
