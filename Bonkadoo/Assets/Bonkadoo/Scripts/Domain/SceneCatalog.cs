using System;
using System.Collections.Generic;
using System.Linq;

namespace Bonkadoo.Domain
{
    public enum HotspotType { Prop, Npc, Exit }

    public sealed class HotspotDefinition
    {
        public string Id { get; }
        public HotspotType Type { get; }
        public string LabelKey { get; }
        /// <summary>Normalized stage rect centre and size; y grows upward.</summary>
        public float X { get; }
        public float Y { get; }
        public float W { get; }
        public float H { get; }
        public string Color { get; }
        public string Character { get; }
        public Predicate VisibleWhen { get; }
        public string ExitTo { get; }
        public bool StartUnlocked { get; }

        public HotspotDefinition(string id, HotspotType type, string labelKey, float x, float y, float w, float h,
            string color = null, string character = null, Predicate visibleWhen = null, string exitTo = null,
            bool startUnlocked = false)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            Type = type;
            LabelKey = labelKey;
            X = x;
            Y = y;
            W = w;
            H = h;
            Color = color;
            Character = character;
            VisibleWhen = visibleWhen;
            ExitTo = exitTo;
            StartUnlocked = startUnlocked;
        }

        public bool IsVisible(IReadOnlyWorldState state) =>
            VisibleWhen == null || VisibleWhen.Evaluate(new EvaluationContext(state, null));

        public bool IsExitOpen(IReadOnlyWorldState state) =>
            Type == HotspotType.Exit && (StartUnlocked || state.IsExitUnlocked(Id));
    }

    public sealed class SceneDefinition
    {
        public string Id { get; }
        public string ChapterId { get; }
        public string NameKey { get; }
        public string Background { get; }
        public string Music { get; }
        public IReadOnlyList<HotspotDefinition> Hotspots { get; }

        public SceneDefinition(string id, string chapterId, string nameKey, string background, string music,
            IEnumerable<HotspotDefinition> hotspots)
        {
            Id = id;
            ChapterId = chapterId;
            NameKey = nameKey;
            Background = background;
            Music = music;
            Hotspots = hotspots.ToArray();
        }

        public HotspotDefinition Find(string hotspotId) =>
            Hotspots.FirstOrDefault(h => string.Equals(h.Id, hotspotId, StringComparison.Ordinal));
    }

    public sealed class ItemDefinition
    {
        public string Id { get; }
        public string ChapterId { get; }
        public string LabelKey { get; }
        public string DescKey { get; }
        public string Color { get; }

        public ItemDefinition(string id, string chapterId, string labelKey, string descKey, string color)
        {
            Id = id;
            ChapterId = chapterId;
            LabelKey = labelKey;
            DescKey = descKey;
            Color = color;
        }
    }

    public sealed class SceneCatalog
    {
        private readonly Dictionary<string, SceneDefinition> _scenes;
        private readonly Dictionary<string, ItemDefinition> _items;

        public IReadOnlyList<SceneDefinition> Scenes { get; }
        public IReadOnlyList<ItemDefinition> Items { get; }

        public SceneCatalog(IEnumerable<SceneDefinition> scenes, IEnumerable<ItemDefinition> items)
        {
            Scenes = scenes.ToArray();
            Items = items.ToArray();
            _scenes = Scenes.ToDictionary(s => s.Id, StringComparer.Ordinal);
            _items = Items.ToDictionary(i => i.Id, StringComparer.Ordinal);
        }

        public SceneDefinition Scene(string id) =>
            id != null && _scenes.TryGetValue(id, out var s) ? s : null;

        public ItemDefinition Item(string id) =>
            id != null && _items.TryGetValue(id, out var i) ? i : null;

        public HotspotDefinition FindHotspot(string sceneId, string hotspotId) => Scene(sceneId)?.Find(hotspotId);
    }
}
