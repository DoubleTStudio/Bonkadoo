using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bonkadoo.Domain
{
    public static class PayloadKeys
    {
        public const string Key = "key";
        public const string Kind = "kind";
        public const string Value = "value";
        public const string ItemId = "itemId";
        public const string ExitId = "exitId";
        public const string NpcId = "npcId";
        public const string RuleId = "ruleId";
        public const string RequestId = "requestId";
        public const string SceneId = "sceneId";
    }

    /// <summary>Pure fold of events into <see cref="WorldState"/>. Replaying the same event ID is a no-op.</summary>
    public static class WorldStateProjector
    {
        /// <returns>false when the event ID was already applied.</returns>
        public static bool Apply(WorldState state, WorldEvent evt)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (evt == null) throw new ArgumentNullException(nameof(evt));
            if (state.AppliedEventIds.Contains(evt.Id)) return false;

            switch (evt.Type)
            {
                case WorldEventTypes.FlagSet:
                    var kind = (FactKind)Enum.Parse(typeof(FactKind), Require(evt, PayloadKeys.Kind));
                    state.Flags[Require(evt, PayloadKeys.Key)] = FactValue.Deserialize(kind, Require(evt, PayloadKeys.Value));
                    break;
                case WorldEventTypes.MetricSet:
                    state.Metrics[Require(evt, PayloadKeys.Key)] = ParseInt(Require(evt, PayloadKeys.Value));
                    break;
                case WorldEventTypes.ItemAdded:
                    state.Inventory.Add(Require(evt, PayloadKeys.ItemId));
                    break;
                case WorldEventTypes.ItemRemoved:
                    state.Inventory.Remove(Require(evt, PayloadKeys.ItemId));
                    break;
                case WorldEventTypes.ExitUnlocked:
                    state.UnlockedExits.Add(Require(evt, PayloadKeys.ExitId));
                    break;
                case WorldEventTypes.NpcDisposition:
                    state.NpcDispositions[Require(evt, PayloadKeys.NpcId)] = ParseInt(Require(evt, PayloadKeys.Value));
                    break;
                case WorldEventTypes.RuleFired:
                    state.FiredRules.Add(Require(evt, PayloadKeys.RuleId));
                    break;
                case WorldEventTypes.RequestCommitted:
                    state.CommittedRequests.Add(Require(evt, PayloadKeys.RequestId));
                    break;
                case WorldEventTypes.SceneEntered:
                    state.CurrentSceneId = Require(evt, PayloadKeys.SceneId);
                    break;
                default:
                    if (WorldEventTypes.IsSystem(evt.Type))
                        throw new InvalidOperationException($"Unknown system event type '{evt.Type}' ({evt.Id})");
                    // narrative event from the content catalog
                    state.OccurredEvents.Add(evt.Type);
                    break;
            }

            state.AppliedEventIds.Add(evt.Id);
            if (evt.Tick > state.LastTick) state.LastTick = evt.Tick;
            return true;
        }

        public static int ApplyAll(WorldState state, IEnumerable<WorldEvent> events)
        {
            var applied = 0;
            foreach (var e in events) if (Apply(state, e)) applied++;
            return applied;
        }

        private static string Require(WorldEvent evt, string key)
        {
            var v = evt.Get(key);
            if (v == null) throw new InvalidOperationException($"Event {evt.Id} ({evt.Type}) missing payload '{key}'");
            return v;
        }

        private static int ParseInt(string s) => int.Parse(s, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
}
