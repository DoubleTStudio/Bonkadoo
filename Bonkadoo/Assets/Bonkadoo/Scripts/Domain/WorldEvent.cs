using System;
using System.Collections.Generic;

namespace Bonkadoo.Domain
{
    public sealed class WorldEvent
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; }
        public string Type { get; }
        public IReadOnlyDictionary<string, string> Payload { get; }
        public long Tick { get; }
        public string CausationId { get; }
        public int SchemaVersion { get; }

        public WorldEvent(string id, string type, IDictionary<string, string> payload, long tick, string causationId,
            int schemaVersion = CurrentSchemaVersion)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id required", nameof(id));
            if (string.IsNullOrEmpty(type)) throw new ArgumentException("type required", nameof(type));
            Id = id;
            Type = type;
            // sorted so serialization and checksums are stable
            Payload = new SortedDictionary<string, string>(payload ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            Tick = tick;
            CausationId = causationId ?? string.Empty;
            SchemaVersion = schemaVersion;
        }

        public string Get(string key) => Payload.TryGetValue(key, out var v) ? v : null;

        public override string ToString() => $"{Id} {Type}";
    }

    public static class WorldEventTypes
    {
        public const string FlagSet = "sys.flag.set";
        public const string MetricSet = "sys.metric.set";
        public const string ItemAdded = "sys.item.added";
        public const string ItemRemoved = "sys.item.removed";
        public const string ExitUnlocked = "sys.exit.unlocked";
        public const string NpcDisposition = "sys.npc.disposition";
        public const string RuleFired = "sys.rule.fired";
        public const string RequestCommitted = "sys.request.committed";
        public const string SceneEntered = "sys.scene.entered";

        public static bool IsSystem(string type) => type.StartsWith("sys.", StringComparison.Ordinal);
    }
}
