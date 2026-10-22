using System;
using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Domain;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bonkadoo.Infrastructure.Content
{
    public sealed class ContentException : Exception
    {
        public ContentException(string message) : base(message) { }
    }

    /// <summary>Raw JSON texts; Unity loads them from Resources, tests from the kit folder.</summary>
    public sealed class ContentSources
    {
        public string WorldJson;
        public string PuzzlesJson;
        public string EventsJson;
        public IReadOnlyList<string> ChapterJsons = Array.Empty<string>();
        public IReadOnlyDictionary<string, string> LocalizationJsons = new Dictionary<string, string>();
    }

    public sealed class ChapterDefinition
    {
        public string Id;
        public string Title;
        public IReadOnlyList<string> PuzzleIds;
        public string CompleteFlag;
    }

    public sealed class GameContent
    {
        public WorldDefinition World;
        public RuleSet Rules;
        public IReadOnlyList<ChapterDefinition> Chapters;
        public ISet<string> EventTypes;
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Localization;
    }

    /// <summary>
    /// Parses content into domain objects. Only allowlisted DSL keys are accepted;
    /// anything else is a load error rather than silently ignored.
    /// </summary>
    public static class ContentLoader
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
        };

        public static GameContent Load(ContentSources src)
        {
            if (src == null) throw new ArgumentNullException(nameof(src));
            var world = ParseWorld(Parse(src.WorldJson, "world.json"));
            var eventTypes = ParseEventCatalog(Parse(src.EventsJson, "events/catalog.json"));
            var localization = ParseLocalization(src.LocalizationJsons);

            var rules = new List<Rule>();
            var puzzles = new List<PuzzleDefinition>();
            var puzzleArray = Parse(src.PuzzlesJson, "puzzles.json") as JArray
                              ?? throw new ContentException("puzzles.json: root must be an array");
            foreach (var token in puzzleArray)
            {
                var p = (JObject)token;
                var pid = Str(p, "id", "puzzle");
                var path = "puzzle " + pid;
                var prereqs = StrArray(p, "prerequisites", path, optional: true);
                var failure = (p["failureResponses"] as JArray)?.OfType<JObject>()
                    .Select(f => (string)f["dialogueKey"]).FirstOrDefault(k => k != null);
                puzzles.Add(new PuzzleDefinition(pid, Str(p, "scene", path), StrArray(p, "hotspots", path), prereqs,
                    failure, StrArray(p, "hints", path)));

                foreach (var r in (JArray)p["rules"] ?? throw new ContentException($"{path}: rules missing"))
                    rules.Add(ParseRule((JObject)r, pid, prereqs));
            }

            var chapters = src.ChapterJsons.Select((c, i) => ParseChapter(Parse(c, $"chapter[{i}]"))).ToList();
            var ruleSet = new RuleSet(rules, puzzles);
            var content = new GameContent
            {
                World = world, Rules = ruleSet, Chapters = chapters, EventTypes = eventTypes, Localization = localization,
            };
            CrossCheck(content, puzzleArray);
            return content;
        }

        private static JToken Parse(string json, string name)
        {
            if (string.IsNullOrEmpty(json)) throw new ContentException($"{name}: empty");
            try { return JsonConvert.DeserializeObject<JToken>(json, Settings); }
            catch (JsonException ex) { throw new ContentException($"{name}: invalid JSON: {ex.Message}"); }
        }

        public static WorldDefinition ParseWorld(JToken token)
        {
            var o = token as JObject ?? throw new ContentException("world.json: root must be an object");
            var metrics = ((JArray)o["metrics"] ?? new JArray()).OfType<JObject>().Select(m =>
                new MetricDefinition(Str(m, "key", "metric"), Int(m, "min", "metric"), Int(m, "max", "metric"),
                    Int(m, "initial", "metric")));
            var npc = o["npcDisposition"] as JObject ?? throw new ContentException("world.json: npcDisposition missing");
            return new WorldDefinition(metrics,
                new MetricDefinition("npcDisposition", Int(npc, "min", "npc"), Int(npc, "max", "npc"), Int(npc, "initial", "npc")),
                (string)o["startScene"], (string)o["contentVersion"]);
        }

        private static ISet<string> ParseEventCatalog(JToken token)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in token as JArray ?? throw new ContentException("catalog.json: root must be an array"))
            {
                var type = Str((JObject)e, "type", "event");
                if (!set.Add(type)) throw new ContentException($"catalog.json: duplicate event {type}");
            }
            return set;
        }

        private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ParseLocalization(
            IReadOnlyDictionary<string, string> sources)
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            foreach (var kv in sources)
            {
                var o = Parse(kv.Value, kv.Key + ".json") as JObject
                        ?? throw new ContentException($"{kv.Key}.json: root must be an object");
                var table = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var prop in o.Properties())
                {
                    if (prop.Value.Type != JTokenType.String)
                        throw new ContentException($"{kv.Key}.json: '{prop.Name}' must be a string");
                    table[prop.Name] = (string)prop.Value;
                }
                result[kv.Key] = table;
            }
            return result;
        }

        private static ChapterDefinition ParseChapter(JToken token)
        {
            var o = (JObject)token;
            var id = Str(o, "id", "chapter");
            return new ChapterDefinition
            {
                Id = id,
                Title = (string)o["title"],
                PuzzleIds = StrArray(o, "puzzles", "chapter " + id),
                CompleteFlag = Str(o, "chapterCompleteFlag", "chapter " + id),
            };
        }

        public static Rule ParseRule(JObject r, string puzzleId, IReadOnlyList<string> prerequisites)
        {
            var id = Str(r, "id", "rule in " + puzzleId);
            var path = "rule " + id;
            var when = ParsePredicate(r["when"] ?? throw new ContentException($"{path}: when missing"), path + ".when");
            if (prerequisites != null && prerequisites.Count > 0)
            {
                // ADR-0002: prerequisites gate rule eligibility
                var gates = prerequisites.Select(f => (Predicate)new FlagEqualsPredicate(f, FactValue.Of(true))).ToList();
                gates.Add(when);
                when = new AllPredicate(gates);
            }
            var effects = ((JArray)r["effects"] ?? throw new ContentException($"{path}: effects missing"))
                .Select((e, i) => ParseEffect(e, $"{path}.effects[{i}]")).ToList();
            var once = r["once"]?.Type == JTokenType.Boolean
                ? (bool)r["once"]
                : throw new ContentException($"{path}: once must be boolean");
            return new Rule(id, Int(r, "priority", path), when, effects, once, puzzleId);
        }

        public static Predicate ParsePredicate(JToken token, string path)
        {
            var (op, arg) = Single(token, path);
            switch (op)
            {
                case "all": return new AllPredicate(Array(arg, path).Select((c, i) => ParsePredicate(c, $"{path}.all[{i}]")));
                case "any": return new AnyPredicate(Array(arg, path).Select((c, i) => ParsePredicate(c, $"{path}.any[{i}]")));
                case "not": return new NotPredicate(ParsePredicate(arg, path + ".not"));
                case "flagEquals":
                {
                    var o = Obj(arg, path);
                    return new FlagEqualsPredicate(Str(o, "key", path), Fact(o["value"], path));
                }
                case "hasItem": return new HasItemPredicate(Str(Obj(arg, path), "itemId", path));
                case "metricAtLeast":
                {
                    var o = Obj(arg, path);
                    return new MetricAtLeastPredicate(Str(o, "key", path), Int(o, "value", path));
                }
                case "npcDispositionAtLeast":
                {
                    var o = Obj(arg, path);
                    return new NpcDispositionAtLeastPredicate(Str(o, "npcId", path), Int(o, "value", path));
                }
                case "eventOccurred": return new EventOccurredPredicate(Str(Obj(arg, path), "type", path));
                case "currentScene": return new CurrentScenePredicate(Text(arg, path));
                case "requestVerb":
                    if (!Enum.TryParse(Text(arg, path), false, out InteractionVerb verb) ||
                        !Enum.IsDefined(typeof(InteractionVerb), verb))
                        throw new ContentException($"{path}: unknown verb '{arg}'");
                    return new RequestVerbPredicate(verb);
                case "requestTarget": return new RequestTargetPredicate(Text(arg, path));
                case "requestSource": return new RequestSourcePredicate(Text(arg, path));
                default: throw new ContentException($"{path}: predicate '{op}' is not allowlisted");
            }
        }

        public static Effect ParseEffect(JToken token, string path)
        {
            var (op, arg) = Single(token, path);
            var o = Obj(arg, path);
            switch (op)
            {
                case "setFlag":
                    var key = Str(o, "key", path);
                    if (WorldKeys.IsTransient(key)) throw new ContentException($"{path}: setFlag cannot target '{key}'");
                    return new SetFlagEffect(key, Fact(o["value"], path));
                case "adjustMetric": return new AdjustMetricEffect(Str(o, "key", path), Int(o, "delta", path));
                case "addItem": return new AddItemEffect(Str(o, "itemId", path));
                case "removeItem": return new RemoveItemEffect(Str(o, "itemId", path));
                case "emitEvent": return new EmitEventEffect(Str(o, "type", path));
                case "queueDialogue": return new QueueDialogueEffect(Str(o, "key", path));
                case "queueAnimation": return new QueueAnimationEffect(Str(o, "cue", path));
                case "unlockExit": return new UnlockExitEffect(Str(o, "exitId", path));
                case "setNpcDisposition": return new SetNpcDispositionEffect(Str(o, "npcId", path), Int(o, "value", path));
                default: throw new ContentException($"{path}: effect '{op}' is not allowlisted");
            }
        }

        private static void CrossCheck(GameContent content, JArray puzzleArray)
        {
            var errors = new List<string>();
            foreach (var rule in content.Rules.OrderedRules)
            {
                foreach (var e in rule.Effects)
                {
                    if (e is EmitEventEffect emit && !content.EventTypes.Contains(emit.EventType))
                        errors.Add($"{rule.Id}: emits unknown event {emit.EventType}");
                    if (e is QueueDialogueEffect dlg)
                        foreach (var table in content.Localization)
                            if (!table.Value.ContainsKey(dlg.Key))
                                errors.Add($"{rule.Id}: dialogue {dlg.Key} missing in {table.Key}");
                    if (e is AdjustMetricEffect m && !content.World.TryGetMetric(m.Key, out _))
                        errors.Add($"{rule.Id}: unknown metric {m.Key}");
                }
            }
            var ids = new HashSet<string>(content.Rules.Puzzles.Select(p => p.Id), StringComparer.Ordinal);
            foreach (var ch in content.Chapters)
                foreach (var pid in ch.PuzzleIds)
                    if (!ids.Contains(pid)) errors.Add($"chapter {ch.Id}: unknown puzzle {pid}");
            if (errors.Count > 0) throw new ContentException(string.Join("\n", errors));
        }

        private static (string, JToken) Single(JToken token, string path)
        {
            var o = token as JObject ?? throw new ContentException($"{path}: expected object");
            var props = o.Properties().ToList();
            if (props.Count != 1) throw new ContentException($"{path}: expected exactly one operator, found {props.Count}");
            return (props[0].Name, props[0].Value);
        }

        private static FactValue Fact(JToken v, string path)
        {
            switch (v?.Type)
            {
                case JTokenType.Boolean: return FactValue.Of((bool)v);
                case JTokenType.Integer: return FactValue.Of((int)v);
                case JTokenType.String: return FactValue.Of((string)v);
                default: throw new ContentException($"{path}: value must be bool, int or string");
            }
        }

        private static JObject Obj(JToken t, string path) => t as JObject ?? throw new ContentException($"{path}: expected object");
        private static JArray Array(JToken t, string path) => t as JArray ?? throw new ContentException($"{path}: expected array");

        private static string Text(JToken t, string path) =>
            t?.Type == JTokenType.String && !string.IsNullOrEmpty((string)t)
                ? (string)t
                : throw new ContentException($"{path}: expected non-empty string");

        private static string Str(JObject o, string key, string path) => Text(o[key], $"{path}.{key}");

        private static int Int(JObject o, string key, string path) =>
            o[key]?.Type == JTokenType.Integer ? (int)o[key] : throw new ContentException($"{path}.{key}: expected integer");

        private static IReadOnlyList<string> StrArray(JObject o, string key, string path, bool optional = false)
        {
            var t = o[key];
            if (t == null && optional) return System.Array.Empty<string>();
            return Array(t, $"{path}.{key}").Select((x, i) => Text(x, $"{path}.{key}[{i}]")).ToArray();
        }
    }
}
