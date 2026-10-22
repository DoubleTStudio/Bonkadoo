using System;
using System.Collections.Generic;

namespace Bonkadoo.Domain
{
    public interface IReadOnlyWorldState
    {
        string CurrentSceneId { get; }
        FactValue? GetFlag(string key);
        bool HasFlag(string key);
        bool HasItem(string id);
        int GetMetric(string key);
        int GetNpcDisposition(string npcId);
        bool HasEvent(string type);
        bool IsExitUnlocked(string exitId);
        bool HasRuleFired(string ruleId);
        bool IsRequestCommitted(string requestId);
        bool IsEventApplied(string eventId);
    }

    /// <summary>
    /// Projection of the event journal. Only <see cref="WorldStateProjector"/> mutates it;
    /// collections are ordinal-sorted so snapshots serialize deterministically.
    /// </summary>
    public sealed class WorldState : IReadOnlyWorldState
    {
        private readonly WorldDefinition _definition;

        internal readonly SortedDictionary<string, FactValue> Flags = new SortedDictionary<string, FactValue>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, int> Metrics = new SortedDictionary<string, int>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, int> NpcDispositions = new SortedDictionary<string, int>(StringComparer.Ordinal);
        internal readonly SortedSet<string> Inventory = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly SortedSet<string> UnlockedExits = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly SortedSet<string> OccurredEvents = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly SortedSet<string> FiredRules = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly SortedSet<string> CommittedRequests = new SortedSet<string>(StringComparer.Ordinal);
        internal readonly SortedSet<string> AppliedEventIds = new SortedSet<string>(StringComparer.Ordinal);

        public WorldDefinition Definition => _definition;
        public string CurrentSceneId { get; internal set; }
        public long LastTick { get; internal set; }

        public WorldState(WorldDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }

        public FactValue? GetFlag(string key) => Flags.TryGetValue(key, out var v) ? v : (FactValue?)null;

        /// <summary>True when the flag exists and is not boolean false.</summary>
        public bool HasFlag(string key) =>
            Flags.TryGetValue(key, out var v) && !(v.Kind == FactKind.Bool && !v.AsBool);

        public bool HasItem(string id) => Inventory.Contains(id);

        public int GetMetric(string key)
        {
            if (Metrics.TryGetValue(key, out var v)) return v;
            return _definition.TryGetMetric(key, out var def) ? def.Initial : 0;
        }

        public int GetNpcDisposition(string npcId) =>
            NpcDispositions.TryGetValue(npcId, out var v) ? v : _definition.NpcDisposition.Initial;

        public bool HasEvent(string type) => OccurredEvents.Contains(type);
        public bool IsExitUnlocked(string exitId) => UnlockedExits.Contains(exitId);
        public bool HasRuleFired(string ruleId) => FiredRules.Contains(ruleId);
        public bool IsRequestCommitted(string requestId) => CommittedRequests.Contains(requestId);
        public bool IsEventApplied(string eventId) => AppliedEventIds.Contains(eventId);

        public IReadOnlyDictionary<string, FactValue> AllFlags => Flags;
        public IReadOnlyDictionary<string, int> AllMetrics => Metrics;
        public IReadOnlyDictionary<string, int> AllNpcDispositions => NpcDispositions;
        public IReadOnlyCollection<string> AllItems => Inventory;
        public IReadOnlyCollection<string> AllUnlockedExits => UnlockedExits;
        public IReadOnlyCollection<string> AllOccurredEvents => OccurredEvents;
        public IReadOnlyCollection<string> AllFiredRules => FiredRules;
        public IReadOnlyCollection<string> AllCommittedRequests => CommittedRequests;
        public IReadOnlyCollection<string> AllAppliedEventIds => AppliedEventIds;

        public WorldState Clone()
        {
            var c = new WorldState(_definition) { CurrentSceneId = CurrentSceneId, LastTick = LastTick };
            foreach (var kv in Flags) c.Flags[kv.Key] = kv.Value;
            foreach (var kv in Metrics) c.Metrics[kv.Key] = kv.Value;
            foreach (var kv in NpcDispositions) c.NpcDispositions[kv.Key] = kv.Value;
            c.Inventory.UnionWith(Inventory);
            c.UnlockedExits.UnionWith(UnlockedExits);
            c.OccurredEvents.UnionWith(OccurredEvents);
            c.FiredRules.UnionWith(FiredRules);
            c.CommittedRequests.UnionWith(CommittedRequests);
            c.AppliedEventIds.UnionWith(AppliedEventIds);
            return c;
        }

        /// <summary>Rebuilds state from a snapshot; used by the save layer.</summary>
        public static WorldState Restore(WorldDefinition definition, WorldStateSnapshot s)
        {
            var w = new WorldState(definition) { CurrentSceneId = s.CurrentSceneId, LastTick = s.LastTick };
            foreach (var kv in s.Flags) w.Flags[kv.Key] = kv.Value;
            foreach (var kv in s.Metrics) w.Metrics[kv.Key] = kv.Value;
            foreach (var kv in s.NpcDispositions) w.NpcDispositions[kv.Key] = kv.Value;
            w.Inventory.UnionWith(s.Inventory);
            w.UnlockedExits.UnionWith(s.UnlockedExits);
            w.OccurredEvents.UnionWith(s.OccurredEvents);
            w.FiredRules.UnionWith(s.FiredRules);
            w.CommittedRequests.UnionWith(s.CommittedRequests);
            w.AppliedEventIds.UnionWith(s.AppliedEventIds);
            return w;
        }

        public WorldStateSnapshot ToSnapshot() => new WorldStateSnapshot
        {
            CurrentSceneId = CurrentSceneId,
            LastTick = LastTick,
            Flags = new SortedDictionary<string, FactValue>(Flags, StringComparer.Ordinal),
            Metrics = new SortedDictionary<string, int>(Metrics, StringComparer.Ordinal),
            NpcDispositions = new SortedDictionary<string, int>(NpcDispositions, StringComparer.Ordinal),
            Inventory = new List<string>(Inventory),
            UnlockedExits = new List<string>(UnlockedExits),
            OccurredEvents = new List<string>(OccurredEvents),
            FiredRules = new List<string>(FiredRules),
            CommittedRequests = new List<string>(CommittedRequests),
            AppliedEventIds = new List<string>(AppliedEventIds),
        };
    }

    /// <summary>Plain data copy of <see cref="WorldState"/> for persistence.</summary>
    public sealed class WorldStateSnapshot
    {
        public string CurrentSceneId;
        public long LastTick;
        public SortedDictionary<string, FactValue> Flags = new SortedDictionary<string, FactValue>(StringComparer.Ordinal);
        public SortedDictionary<string, int> Metrics = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public SortedDictionary<string, int> NpcDispositions = new SortedDictionary<string, int>(StringComparer.Ordinal);
        public List<string> Inventory = new List<string>();
        public List<string> UnlockedExits = new List<string>();
        public List<string> OccurredEvents = new List<string>();
        public List<string> FiredRules = new List<string>();
        public List<string> CommittedRequests = new List<string>();
        public List<string> AppliedEventIds = new List<string>();
    }
}
