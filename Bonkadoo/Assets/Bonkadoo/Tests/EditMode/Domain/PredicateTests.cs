using System.Collections.Generic;
using Bonkadoo.Domain;
using Bonkadoo.Tests.Support;
using NUnit.Framework;

namespace Bonkadoo.Tests.Domain
{
    public class PredicateTests
    {
        private WorldState _state;

        [SetUp]
        public void SetUp()
        {
            _state = T.StateIn(TestContent.World(), "scene.office");
            Apply(WorldEventTypes.FlagSet, PayloadKeys.Key, "flag.door", PayloadKeys.Kind, "Bool", PayloadKeys.Value, "true");
            Apply(WorldEventTypes.FlagSet, PayloadKeys.Key, "flag.mood", PayloadKeys.Kind, "String", PayloadKeys.Value, "grumpy");
            Apply(WorldEventTypes.ItemAdded, PayloadKeys.ItemId, "item.stamp");
            Apply(WorldEventTypes.MetricSet, PayloadKeys.Key, "evidence_count", PayloadKeys.Value, "4");
            Apply(WorldEventTypes.NpcDisposition, PayloadKeys.NpcId, "npc.coc", PayloadKeys.Value, "-1");
            Apply("evt.ch01.p01.solved.a");
        }

        private int _n;

        private void Apply(string type, params string[] kv)
        {
            var p = new Dictionary<string, string>();
            for (var i = 0; i < kv.Length; i += 2) p[kv[i]] = kv[i + 1];
            WorldStateProjector.Apply(_state, new WorldEvent("e" + (++_n), type, p, 1, "test"));
        }

        private bool Eval(Predicate p, InteractionRequest r = null) => p.Evaluate(new EvaluationContext(_state, r));

        [Test]
        public void FlagEqualsMatchesTypedValues()
        {
            Assert.IsTrue(Eval(new FlagEqualsPredicate("flag.door", FactValue.Of(true))));
            Assert.IsTrue(Eval(new FlagEqualsPredicate("flag.mood", FactValue.Of("grumpy"))));
            Assert.IsFalse(Eval(new FlagEqualsPredicate("flag.mood", FactValue.Of(true))), "kinds must match");
        }

        [Test]
        public void AbsentFlagEqualsFalseButNotString()
        {
            Assert.IsTrue(Eval(new FlagEqualsPredicate("flag.unset", FactValue.Of(false))));
            Assert.IsFalse(Eval(new FlagEqualsPredicate("flag.unset", FactValue.Of(true))));
            Assert.IsFalse(Eval(new FlagEqualsPredicate("flag.unset", FactValue.Of("a"))));
        }

        [Test]
        public void StateQueries()
        {
            Assert.IsTrue(Eval(new HasItemPredicate("item.stamp")));
            Assert.IsFalse(Eval(new HasItemPredicate("item.badge")));
            Assert.IsTrue(Eval(new MetricAtLeastPredicate("evidence_count", 4)));
            Assert.IsFalse(Eval(new MetricAtLeastPredicate("evidence_count", 5)));
            Assert.IsTrue(Eval(new MetricAtLeastPredicate("trust_bop", 0)), "unset metric uses initial");
            Assert.IsTrue(Eval(new NpcDispositionAtLeastPredicate("npc.coc", -1)));
            Assert.IsFalse(Eval(new NpcDispositionAtLeastPredicate("npc.coc", 0)));
            Assert.IsTrue(Eval(new EventOccurredPredicate("evt.ch01.p01.solved.a")));
            Assert.IsFalse(Eval(new EventOccurredPredicate("evt.ch01.p01.solved.b")));
            Assert.IsTrue(Eval(new CurrentScenePredicate("scene.office")));
            Assert.IsFalse(Eval(new CurrentScenePredicate("scene.hallway")));
        }

        [Test]
        public void Combinators()
        {
            var t = TruePredicate.Instance;
            var f = new NotPredicate(t);
            Assert.IsFalse(Eval(f));
            Assert.IsTrue(Eval(new AllPredicate(new Predicate[] { t, t })));
            Assert.IsFalse(Eval(new AllPredicate(new Predicate[] { t, f })));
            Assert.IsTrue(Eval(new AllPredicate(new Predicate[0])), "empty all is vacuously true");
            Assert.IsTrue(Eval(new AnyPredicate(new Predicate[] { f, t })));
            Assert.IsFalse(Eval(new AnyPredicate(new Predicate[0])));
        }

        [Test]
        public void RequestPredicates()
        {
            var r = T.Req("npc.vuong", scene: "scene.office", verb: InteractionVerb.UseItem, source: "item.stamp");
            Assert.IsTrue(Eval(new RequestVerbPredicate(InteractionVerb.UseItem), r));
            Assert.IsFalse(Eval(new RequestVerbPredicate(InteractionVerb.Talk), r));
            Assert.IsTrue(Eval(new RequestTargetPredicate("npc.vuong"), r));
            Assert.IsTrue(Eval(new RequestSourcePredicate("item.stamp"), r));
            Assert.IsFalse(Eval(new RequestSourcePredicate("item.stamp")), "no request, no match");
        }
    }
}
