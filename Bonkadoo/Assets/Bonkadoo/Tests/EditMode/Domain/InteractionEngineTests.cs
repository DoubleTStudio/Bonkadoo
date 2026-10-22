using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Tests.Support;
using NUnit.Framework;

namespace Bonkadoo.Tests.Domain
{
    public class InteractionEngineTests
    {
        private WorldDefinition _def;

        [SetUp]
        public void SetUp() => _def = TestContent.World();

        private GameSession Session(params Rule[] rules) =>
            new GameSession(_def, new InteractionEngine(new RuleSet(rules)), new InMemoryEventStore(), T.StateIn(_def, "scene.test"));

        [Test]
        public void HigherPriorityRuleWins()
        {
            var s = Session(
                T.Rule("rule.low", 10, T.Target("door"), T.Dlg("dlg.low")),
                T.Rule("rule.high", 50, T.Target("door"), T.Dlg("dlg.high")));
            Assert.AreEqual("rule.high", s.Interact(T.Req("door")).MatchedRuleId);
        }

        [Test]
        public void EqualPriorityResolvesByOrdinalRuleId()
        {
            // declared out of order on purpose
            var s = Session(
                T.Rule("rule.b", 10, T.Target("door"), T.Dlg("b")),
                T.Rule("rule.B", 10, T.Target("door"), T.Dlg("B")),
                T.Rule("rule.a", 10, T.Target("door"), T.Dlg("a")));
            Assert.AreEqual("rule.B", s.Interact(T.Req("door")).MatchedRuleId, "ordinal: uppercase sorts first");
        }

        [Test]
        public void ResolutionIsIndependentOfDeclarationOrder()
        {
            var rules = new[]
            {
                T.Rule("r1", 5, T.Target("x"), T.Dlg("1")),
                T.Rule("r2", 7, T.Target("x"), T.Dlg("2")),
                T.Rule("r3", 7, T.Target("x"), T.Dlg("3")),
                T.Rule("r4", 1, TruePredicate.Instance, T.Dlg("4")),
            };
            var rng = new System.Random(1234);
            for (var i = 0; i < 25; i++)
            {
                var shuffled = rules.OrderBy(_ => rng.Next()).ToArray();
                Assert.AreEqual("r2", Session(shuffled).Interact(T.Req("x")).MatchedRuleId);
            }
        }

        [Test]
        public void UnmatchedRequestReturnsNonSilentFallback()
        {
            var s = Session(T.Rule("r", 1, T.Target("door"), T.Dlg("ok")));
            var outcome = s.Interact(T.Req("window"));
            Assert.AreEqual(OutcomeStatus.Fallback, outcome.Status);
            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(RuleSet.DefaultFallbackKey, outcome.FeedbackKey);
            Assert.AreEqual(0, s.Journal.Count);
        }

        [Test]
        public void FallbackUsesOwningPuzzleFailureLine()
        {
            var puzzle = new PuzzleDefinition("ch99.p01", "scene.test", new[] { "door" }, new string[0], "dlg.ch99.p01.fail",
                new[] { "h1", "h2", "h3" });
            var engine = new InteractionEngine(new RuleSet(new Rule[0], new[] { puzzle }));
            var outcome = engine.Evaluate(T.Req("door"), T.StateIn(_def, "scene.test"));
            Assert.AreEqual("dlg.ch99.p01.fail", outcome.FeedbackKey);
        }

        [Test]
        public void DuplicateRequestIdCommitsOnce()
        {
            // TC-001
            var s = Session(T.Rule("r", 1, T.Target("door"), new AdjustMetricEffect("evidence_count", 1), T.Emit("evt.test")));
            var first = s.Interact(T.Req("door", id: "same"));
            var second = s.Interact(T.Req("door", id: "same"));
            Assert.AreEqual(OutcomeStatus.Committed, first.Status);
            Assert.AreEqual(OutcomeStatus.Duplicate, second.Status);
            Assert.AreEqual(1, s.State.GetMetric("evidence_count"));
            Assert.AreEqual(first.Events.Count, s.Journal.Count);
        }

        [Test]
        public void InvalidEffectRejectsWholeRuleAtomically()
        {
            var s = Session(T.Rule("r", 1, T.Target("door"),
                T.Flag("flag.opened"),
                new AdjustMetricEffect("evidence_count", 2),
                new RemoveItemEffect("item.not_held")));
            var outcome = s.Interact(T.Req("door"));
            Assert.AreEqual(OutcomeStatus.Rejected, outcome.Status);
            Assert.AreEqual("item_not_held", outcome.ErrorCode);
            Assert.IsFalse(s.State.HasFlag("flag.opened"), "no partial commit");
            Assert.AreEqual(0, s.State.GetMetric("evidence_count"));
            Assert.AreEqual(0, s.Journal.Count);
            Assert.IsNotNull(outcome.FeedbackKey, "rejection still gives feedback");
        }

        [Test]
        public void EffectsSeeEarlierEffectsWithinTheSameRule()
        {
            var s = Session(T.Rule("r", 1, T.Target("desk"), new AddItemEffect("item.key"), new RemoveItemEffect("item.key")));
            Assert.AreEqual(OutcomeStatus.Committed, s.Interact(T.Req("desk")).Status);
            Assert.IsFalse(s.State.HasItem("item.key"));
        }

        [Test]
        public void MetricsClampToDefinedRange()
        {
            var s = Session(T.Rule("r", 1, T.Target("x"), new AdjustMetricEffect("trust_bop", 10)));
            s.Interact(T.Req("x"));
            Assert.AreEqual(3, s.State.GetMetric("trust_bop"));
        }

        [Test]
        public void UnknownMetricIsRejected()
        {
            var outcome = Session(T.Rule("r", 1, T.Target("x"), new AdjustMetricEffect("nope", 1))).Interact(T.Req("x"));
            Assert.AreEqual("unknown_metric", outcome.ErrorCode);
        }

        [Test]
        public void NpcDispositionOutOfRangeIsRejected()
        {
            var outcome = Session(T.Rule("r", 1, T.Target("x"), new SetNpcDispositionEffect("npc.coc", 9))).Interact(T.Req("x"));
            Assert.AreEqual("out_of_range", outcome.ErrorCode);
        }

        [Test]
        public void OnceRuleFiresOnlyOnceThenNextRuleApplies()
        {
            var s = Session(
                T.Once("rule.first", 10, T.Target("bell"), T.Dlg("dlg.first")),
                T.Rule("rule.repeat", 1, T.Target("bell"), T.Dlg("dlg.repeat")));
            Assert.AreEqual("rule.first", s.Interact(T.Req("bell")).MatchedRuleId);
            Assert.AreEqual("rule.repeat", s.Interact(T.Req("bell")).MatchedRuleId);
        }

        [Test]
        public void TransientInputIsReadFromRequestAndNeverPersisted()
        {
            var when = new FlagEqualsPredicate("input.p.choice", FactValue.Of("a"));
            var s = Session(T.Rule("r", 1, when, T.Flag("flag.p.done")));
            var input = new Dictionary<string, FactValue> { ["input.p.choice"] = FactValue.Of("a") };

            Assert.AreEqual(OutcomeStatus.Fallback, s.Interact(T.Req("x")).Status, "no input, no match");
            Assert.AreEqual(OutcomeStatus.Committed, s.Interact(T.Req("x", input: input)).Status);
            Assert.IsNull(s.State.GetFlag("input.p.choice"));
        }

        [Test]
        public void WritingTransientKeyIsRejected()
        {
            var outcome = Session(T.Rule("r", 1, T.Target("x"), new SetFlagEffect("input.hack", FactValue.Of(true))))
                .Interact(T.Req("x"));
            Assert.AreEqual("transient_write", outcome.ErrorCode);
        }

        [Test]
        public void StaleSceneRequestIsRejected()
        {
            var outcome = Session(T.Rule("r", 1, T.Target("x"), T.Dlg("d"))).Interact(T.Req("x", scene: "scene.other"));
            Assert.AreEqual(OutcomeStatus.Rejected, outcome.Status);
            Assert.AreEqual("stale_scene", outcome.ErrorCode);
        }

        [Test]
        public void EmittingSystemEventTypeIsRejected()
        {
            var outcome = Session(T.Rule("r", 1, T.Target("x"), T.Emit("sys.flag.set"))).Interact(T.Req("x"));
            Assert.AreEqual("reserved_event", outcome.ErrorCode);
        }

        [Test]
        public void OutcomeCarriesDialogueAndAnimationCuesWithoutStateEvents()
        {
            var s = Session(T.Rule("r", 1, T.Target("x"), T.Dlg("dlg.a"), new QueueAnimationEffect("anim.bonk"), T.Dlg("dlg.b")));
            var o = s.Interact(T.Req("x"));
            CollectionAssert.AreEqual(new[] { "dlg.a", "dlg.b" }, o.DialogueKeys);
            CollectionAssert.AreEqual(new[] { "anim.bonk" }, o.AnimationCues);
            Assert.AreEqual("dlg.a", o.FeedbackKey);
            Assert.IsTrue(o.Events.All(e => e.Type == WorldEventTypes.RequestCommitted));
        }

        [Test]
        public void EventIdsAreDeterministicFromRequestId()
        {
            var s = Session(T.Rule("r", 1, T.Target("x"), T.Flag("f"), T.Emit("evt.x")));
            var o = s.Interact(T.Req("x", id: "abc"));
            CollectionAssert.AreEqual(new[] { "abc:000", "abc:001", "abc:002" }, o.Events.Select(e => e.Id).ToArray());
            Assert.IsTrue(o.Events.All(e => e.CausationId == "abc"));
        }
    }
}
