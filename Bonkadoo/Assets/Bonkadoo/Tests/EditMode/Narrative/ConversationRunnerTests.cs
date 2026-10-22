using System.Collections.Generic;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Narrative;
using Bonkadoo.Tests.Support;
using NUnit.Framework;

namespace Bonkadoo.Tests.Narrative
{
    public class ConversationRunnerTests
    {
        private const string Script = @"title: beat
---
bop: Hello. #line:beat.l001
<<if metric(""trust_bop"") >= 1>>
bop: I trust you. #line:beat.l002
<<else>>
bop: I am watching you. #line:beat.l003
<<endif>>
-> Be nice #line:beat.o004
    <<act choice.test.nice>>
    bop: Nice. #line:beat.l005
-> Be rude #line:beat.o006
    bop: Rude. #line:beat.l007
ti: Bye. #line:beat.l008
===
title: second
---
Narration only. #line:second.l001
===
";

        private GameSession _session;
        private InteractionController _controller;
        private ConversationRunner _runner;
        private readonly List<string> _finished = new List<string>();

        private sealed class Ticks : ITickSource { public long Now => 1; }

        [SetUp]
        public void SetUp()
        {
            var def = TestContent.World();
            var rules = new RuleSet(new[]
            {
                T.Once("rule.choice.test.nice", 200, new AllPredicate(new Predicate[]
                {
                    new RequestVerbPredicate(InteractionVerb.Talk), T.Target("choice.test.nice"),
                }), new AdjustMetricEffect("trust_bop", 1)),
            });
            _session = new GameSession(def, new InteractionEngine(rules), new InMemoryEventStore(), T.StateIn(def, "scene.test"));
            _controller = new InteractionController(_session, new RequestIdFactory("n"), new Ticks());
            var narrative = new YarnNarrative(new[] { Script }, () => _session.State);
            var speakers = new Dictionary<string, string>
            {
                ["yarn.beat.l001"] = "bop", ["yarn.beat.l008"] = "ti", ["yarn.second.l001"] = "narrator",
            };
            _runner = new ConversationRunner(narrative, speakers, act => _controller.Choose(act));
            _finished.Clear();
            _runner.Finished += _finished.Add;
        }

        [Test]
        public void PlaysLinesBranchesAndChoices()
        {
            _runner.Enqueue("beat");
            Assert.AreEqual(ConversationState.Line, _runner.State);
            Assert.AreEqual("yarn.beat.l001", _runner.CurrentLine.Key);
            Assert.AreEqual("bop", _runner.CurrentLine.Speaker);
            _runner.Advance();
            Assert.AreEqual("yarn.beat.l003", _runner.CurrentLine.Key, "trust 0 takes the else branch");
            _runner.Advance();
            Assert.AreEqual(ConversationState.Options, _runner.State);
            CollectionAssert.AreEqual(new[] { "yarn.beat.o004", "yarn.beat.o006" }, new[] { _runner.Options[0].Key, _runner.Options[1].Key });
            _runner.Choose(0);
            Assert.AreEqual(1, _session.State.GetMetric("trust_bop"), "act went through the engine");
            Assert.AreEqual("yarn.beat.l005", _runner.CurrentLine.Key);
            _runner.Advance();
            Assert.AreEqual("yarn.beat.l008", _runner.CurrentLine.Key);
            _runner.Advance();
            Assert.AreEqual(ConversationState.Done, _runner.State);
            CollectionAssert.AreEqual(new[] { "beat" }, _finished);
        }

        [Test]
        public void ConditionsReadLiveState()
        {
            _session.Interact(new InteractionRequest("x", InteractionVerb.Talk, null, "choice.test.nice", "scene.test", 1));
            _runner.Enqueue("beat");
            _runner.Advance();
            Assert.AreEqual("yarn.beat.l002", _runner.CurrentLine.Key);
        }

        [Test]
        public void QueuedNodesPlayInOrderAndNarratorHasNoSpeaker()
        {
            _runner.Enqueue("second");
            _runner.Enqueue("beat");
            Assert.AreEqual("yarn.second.l001", _runner.CurrentLine.Key);
            Assert.IsNull(_runner.CurrentLine.Speaker);
            _runner.Advance();
            Assert.AreEqual("yarn.beat.l001", _runner.CurrentLine.Key);
            CollectionAssert.AreEqual(new[] { "second" }, _finished);
        }

        [Test]
        public void UnknownNodeThrows()
        {
            Assert.Throws<NarrativeException>(() => _runner.Enqueue("nope"));
        }

        [Test]
        public void BadScriptFailsToCompile()
        {
            Assert.Throws<NarrativeException>(() => new YarnNarrative(new[] { "title: x\n---\n<<if nonsense(>>\n===\n" }, () => _session.State));
        }
    }
}
