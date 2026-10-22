using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Tests.Support;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Bonkadoo.Tests.Domain
{
    /// <summary>Property-style tests: random interaction sequences with fixed seeds.</summary>
    public class ReplayDeterminismTests
    {
        private static readonly string[] Targets = { "a", "b", "c", "d", "e" };

        private static RuleSet Rules() => new RuleSet(new[]
        {
            T.Rule("r.a", 10, T.Target("a"), new AdjustMetricEffect("evidence_count", 1), T.Emit("evt.a")),
            T.Rule("r.b", 10, T.Target("b"), new AddItemEffect("item.b")),
            T.Rule("r.c", 10, new AllPredicate(new Predicate[] { T.Target("c"), new HasItemPredicate("item.b") }),
                new RemoveItemEffect("item.b"), new AdjustMetricEffect("trust_bop", -1)),
            T.Rule("r.c.fail", 5, T.Target("c"), new RemoveItemEffect("item.never")),
            T.Once("r.d", 10, T.Target("d"), T.Flag("flag.d"), new SetNpcDispositionEffect("npc.x", 2)),
        });

        private static string Fingerprint(IReadOnlyWorldState s) =>
            JsonConvert.SerializeObject(SaveGameJson.ToJson(new SaveGame { WorldState = ((WorldState)s).ToSnapshot() })["worldState"]);

        private static GameSession Play(int seed, int steps, out List<InteractionRequest> requests)
        {
            var def = TestContent.World();
            var session = new GameSession(def, new InteractionEngine(Rules()), new InMemoryEventStore(), T.StateIn(def, "scene.test"));
            var rng = new System.Random(seed);
            requests = new List<InteractionRequest>();
            for (var i = 0; i < steps; i++)
            {
                // ~20% resends an earlier request ID to exercise idempotency
                var reuse = requests.Count > 0 && rng.NextDouble() < 0.2;
                var r = reuse ? requests[rng.Next(requests.Count)] : T.Req(Targets[rng.Next(Targets.Length)], id: $"s{seed}-{i}", tick: i);
                requests.Add(r);
                session.Interact(r);
            }
            return session;
        }

        [Test]
        public void ReplayingJournalReproducesState([Values(1, 2, 3, 42, 1337)] int seed)
        {
            var live = Play(seed, 200, out _);
            var replayed = T.StateIn(live.Definition, "scene.test");
            WorldStateProjector.ApplyAll(replayed, live.Journal.ReadFrom(0));
            Assert.AreEqual(Fingerprint(live.State), Fingerprint(replayed));
        }

        [Test]
        public void SameInputsGiveIdenticalJournals([Values(7, 99)] int seed)
        {
            var a = Play(seed, 150, out _).Journal.ReadFrom(0).Select(e => e.Id + e.Type).ToArray();
            var b = Play(seed, 150, out _).Journal.ReadFrom(0).Select(e => e.Id + e.Type).ToArray();
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void ReapplyingJournalTwiceIsNoOp([Values(5, 11)] int seed)
        {
            var live = Play(seed, 120, out _);
            var before = Fingerprint(live.State);
            var applied = WorldStateProjector.ApplyAll((WorldState)live.State, live.Journal.ReadFrom(0));
            Assert.AreEqual(0, applied);
            Assert.AreEqual(before, Fingerprint(live.State));
        }

        [Test]
        public void ResendingEveryRequestChangesNothing([Values(3, 8)] int seed)
        {
            var live = Play(seed, 100, out var requests);
            var before = Fingerprint(live.State);
            var count = live.Journal.Count;
            foreach (var r in requests.Where(r => live.State.IsRequestCommitted(r.RequestId)))
                Assert.AreEqual(OutcomeStatus.Duplicate, live.Interact(r).Status);
            Assert.AreEqual(before, Fingerprint(live.State));
            Assert.AreEqual(count, live.Journal.Count);
        }

        [Test]
        public void InvariantsHoldThroughoutRandomPlay([Values(21, 22, 23)] int seed)
        {
            var live = Play(seed, 300, out _);
            var s = (WorldState)live.State;
            foreach (var kv in s.AllMetrics)
            {
                Assert.IsTrue(live.Definition.TryGetMetric(kv.Key, out var def), kv.Key);
                Assert.That(kv.Value, Is.InRange(def.Min, def.Max), kv.Key);
            }
            Assert.AreEqual(s.AllItems.Count, s.AllItems.Distinct().Count());
            Assert.AreEqual(s.AllAppliedEventIds.Count, live.Journal.Count + 1, "+1 for the init scene event");
        }
    }
}
