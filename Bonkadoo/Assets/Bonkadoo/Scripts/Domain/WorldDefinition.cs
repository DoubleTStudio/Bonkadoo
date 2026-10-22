using System;
using System.Collections.Generic;

namespace Bonkadoo.Domain
{
    public sealed class MetricDefinition
    {
        public string Key { get; }
        public int Min { get; }
        public int Max { get; }
        public int Initial { get; }

        public MetricDefinition(string key, int min, int max, int initial)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("key required", nameof(key));
            if (min > max) throw new ArgumentException($"Metric {key}: min > max");
            if (initial < min || initial > max) throw new ArgumentException($"Metric {key}: initial out of range");
            Key = key;
            Min = min;
            Max = max;
            Initial = initial;
        }

        public int Clamp(int value) => value < Min ? Min : value > Max ? Max : value;
    }

    /// <summary>Static world configuration (content/world/world.json).</summary>
    public sealed class WorldDefinition
    {
        private readonly Dictionary<string, MetricDefinition> _metrics;

        public IReadOnlyDictionary<string, MetricDefinition> Metrics => _metrics;
        public MetricDefinition NpcDisposition { get; }
        public string StartSceneId { get; }
        public string ContentVersion { get; }

        public WorldDefinition(IEnumerable<MetricDefinition> metrics, MetricDefinition npcDisposition, string startSceneId,
            string contentVersion)
        {
            _metrics = new Dictionary<string, MetricDefinition>(StringComparer.Ordinal);
            foreach (var m in metrics)
            {
                if (_metrics.ContainsKey(m.Key)) throw new ArgumentException($"Duplicate metric {m.Key}");
                _metrics.Add(m.Key, m);
            }
            NpcDisposition = npcDisposition ?? throw new ArgumentNullException(nameof(npcDisposition));
            StartSceneId = startSceneId;
            ContentVersion = contentVersion ?? "0";
        }

        public bool TryGetMetric(string key, out MetricDefinition def) => _metrics.TryGetValue(key, out def);
    }
}
