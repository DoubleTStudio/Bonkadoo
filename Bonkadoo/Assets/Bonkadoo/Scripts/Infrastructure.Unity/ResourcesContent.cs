using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Infrastructure.Content;
using UnityEngine;

namespace Bonkadoo.Infrastructure.Unity
{
    /// <summary>Reads content synced by Tools/sync_content.py from Resources/BonkadooContent.</summary>
    public static class ResourcesContent
    {
        public const string Root = "BonkadooContent/";
        public static readonly string[] Languages = { "en", "vi" };
        public static readonly string[] ChapterIds = { "ch01", "ch02", "ch03", "ch04", "ch05" };

        public static ContentSources Load()
        {
            return new ContentSources
            {
                WorldJson = Text("world/world"),
                PuzzlesJson = Text("puzzles/puzzles"),
                EventsJson = Text("events/catalog"),
                AmbientJson = Text("scenes/ambient"),
                ScenesJson = Text("scenes/scenes"),
                ItemsJson = Text("items/items"),
                DialogueLinesJson = Text("dialogue/lines"),
                YarnSources = ChapterIds.ToDictionary(c => c, c => Text($"dialogue/{c}")),
                ChapterJsons = ChapterIds.Select(c => Text($"chapters/{c}")).ToList(),
                LocalizationJsons = Languages.ToDictionary(l => l, l => Text("localization/" + l)),
            };
        }

        private static string Text(string path)
        {
            var asset = Resources.Load<TextAsset>(Root + path);
            if (asset == null) throw new ContentException($"Resources/{Root}{path} missing; run Tools/sync_content.py");
            return asset.text;
        }
    }
}
