using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Content;

namespace Bonkadoo.Tests.Support
{
    /// <summary>Locates the kit's authoritative content from Unity (cwd = project) or dotnet (cwd = bin).</summary>
    public static class TestContent
    {
        private const string KitRelative = "docs/BONKADOO_Complete_Game_Development_Kit";
        private static GameContent _cached;

        public static string KitRoot
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("BONKADOO_KIT");
                if (!string.IsNullOrEmpty(env)) return env;
                foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
                {
                    var dir = new DirectoryInfo(start);
                    while (dir != null)
                    {
                        var candidate = Path.Combine(dir.FullName, KitRelative);
                        if (Directory.Exists(Path.Combine(candidate, "content"))) return candidate;
                        dir = dir.Parent;
                    }
                }
                throw new DirectoryNotFoundException("Kit content not found; set BONKADOO_KIT");
            }
        }

        public static string ContentPath(string relative) => Path.Combine(KitRoot, "content", relative);

        public static ContentSources Sources()
        {
            var chapters = Directory.GetFiles(ContentPath("chapters"), "*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(File.ReadAllText).ToList();
            return new ContentSources
            {
                WorldJson = File.ReadAllText(ContentPath("world/world.json")),
                PuzzlesJson = File.ReadAllText(ContentPath("puzzles/puzzles.json")),
                EventsJson = File.ReadAllText(ContentPath("events/catalog.json")),
                ChapterJsons = chapters,
                LocalizationJsons = new Dictionary<string, string>
                {
                    ["en"] = File.ReadAllText(ContentPath("localization/en.json")),
                    ["vi"] = File.ReadAllText(ContentPath("localization/vi.json")),
                },
            };
        }

        public static GameContent Load() => _cached ?? (_cached = ContentLoader.Load(Sources()));

        public static WorldDefinition World() => new WorldDefinition(
            new[]
            {
                new MetricDefinition("trust_bop", -3, 3, 0),
                new MetricDefinition("evidence_count", 0, 8, 0),
            },
            new MetricDefinition("npcDisposition", -3, 3, 0), "scene.test", "test");
    }

    /// <summary>Small fluent helpers to keep test bodies readable.</summary>
    public static class T
    {
        private static int _seq;

        public static InteractionRequest Req(string target, string scene = "scene.test", InteractionVerb verb = InteractionVerb.Tap,
            string source = null, string id = null, long tick = 1, IDictionary<string, FactValue> input = null) =>
            new InteractionRequest(id ?? "req-" + (++_seq), verb, source, target, scene, tick,
                input == null ? null : new Dictionary<string, FactValue>(input, StringComparer.Ordinal));

        public static Rule Rule(string id, int priority, Predicate when, params Effect[] effects) =>
            new Rule(id, priority, when, effects, once: false);

        public static Rule Once(string id, int priority, Predicate when, params Effect[] effects) =>
            new Rule(id, priority, when, effects, once: true);

        public static Predicate Target(string id) => new RequestTargetPredicate(id);
        public static Effect Flag(string key, bool v = true) => new SetFlagEffect(key, FactValue.Of(v));
        public static Effect Dlg(string key) => new QueueDialogueEffect(key);
        public static Effect Emit(string type) => new EmitEventEffect(type);

        public static WorldState StateIn(WorldDefinition def, string scene)
        {
            var s = new WorldState(def);
            WorldStateProjector.Apply(s, new WorldEvent("init:scene", WorldEventTypes.SceneEntered,
                new Dictionary<string, string> { [PayloadKeys.SceneId] = scene }, 0, "init"));
            return s;
        }
    }
}
