using System;
using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Domain;
using Newtonsoft.Json.Linq;

namespace Bonkadoo.Infrastructure.Save
{
    /// <summary>DATA_CONTRACTS save contract. <see cref="Sequence"/> orders the two rotating slot files.</summary>
    public sealed class SaveGame
    {
        public const int CurrentVersion = 1;

        public int SaveVersion = CurrentVersion;
        public string ContentVersion;
        public string LocalProfileId;
        public string ChapterId;
        public string SceneId;
        public string CheckpointId;
        public long Sequence;
        public WorldStateSnapshot WorldState = new WorldStateSnapshot();
        public List<WorldEvent> EventJournal = new List<WorldEvent>();
        public long JournalCursor;
    }

    /// <summary>Canonical JSON mapping. Field order is fixed so the checksum is reproducible.</summary>
    public static class SaveGameJson
    {
        public static JObject ToJson(SaveGame g)
        {
            return new JObject
            {
                ["saveVersion"] = g.SaveVersion,
                ["contentVersion"] = g.ContentVersion,
                ["localProfileId"] = g.LocalProfileId,
                ["chapterId"] = g.ChapterId,
                ["sceneId"] = g.SceneId,
                ["checkpointId"] = g.CheckpointId,
                ["sequence"] = g.Sequence,
                ["worldState"] = StateToJson(g.WorldState),
                ["eventJournal"] = new JArray(g.EventJournal.Select(EventToJson)),
                ["journalCursor"] = g.JournalCursor,
            };
        }

        public static SaveGame FromJson(JObject o)
        {
            return new SaveGame
            {
                SaveVersion = (int)o["saveVersion"],
                ContentVersion = (string)o["contentVersion"],
                LocalProfileId = (string)o["localProfileId"],
                ChapterId = (string)o["chapterId"],
                SceneId = (string)o["sceneId"],
                CheckpointId = (string)o["checkpointId"],
                Sequence = (long)o["sequence"],
                WorldState = StateFromJson((JObject)o["worldState"]),
                EventJournal = ((JArray)o["eventJournal"]).Select(e => EventFromJson((JObject)e)).ToList(),
                JournalCursor = (long)o["journalCursor"],
            };
        }

        public static JObject EventToJson(WorldEvent e)
        {
            var payload = new JObject();
            foreach (var kv in e.Payload) payload[kv.Key] = kv.Value;
            return new JObject
            {
                ["id"] = e.Id,
                ["type"] = e.Type,
                ["payload"] = payload,
                ["tick"] = e.Tick,
                ["causationId"] = e.CausationId,
                ["schemaVersion"] = e.SchemaVersion,
            };
        }

        public static WorldEvent EventFromJson(JObject o)
        {
            var payload = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var p in ((JObject)o["payload"]).Properties()) payload[p.Name] = (string)p.Value;
            return new WorldEvent((string)o["id"], (string)o["type"], payload, (long)o["tick"], (string)o["causationId"],
                (int)o["schemaVersion"]);
        }

        private static JObject StateToJson(WorldStateSnapshot s)
        {
            var flags = new JObject();
            foreach (var kv in s.Flags)
                flags[kv.Key] = new JObject { ["kind"] = kv.Value.Kind.ToString(), ["value"] = kv.Value.Serialize() };
            return new JObject
            {
                ["currentSceneId"] = s.CurrentSceneId,
                ["lastTick"] = s.LastTick,
                ["flags"] = flags,
                ["metrics"] = IntMap(s.Metrics),
                ["npcDispositions"] = IntMap(s.NpcDispositions),
                ["inventory"] = new JArray(s.Inventory),
                ["unlockedExits"] = new JArray(s.UnlockedExits),
                ["occurredEvents"] = new JArray(s.OccurredEvents),
                ["firedRules"] = new JArray(s.FiredRules),
                ["committedRequests"] = new JArray(s.CommittedRequests),
                ["appliedEventIds"] = new JArray(s.AppliedEventIds),
            };
        }

        private static WorldStateSnapshot StateFromJson(JObject o)
        {
            var s = new WorldStateSnapshot
            {
                CurrentSceneId = (string)o["currentSceneId"],
                LastTick = (long)o["lastTick"],
                Inventory = Strings(o["inventory"]),
                UnlockedExits = Strings(o["unlockedExits"]),
                OccurredEvents = Strings(o["occurredEvents"]),
                FiredRules = Strings(o["firedRules"]),
                CommittedRequests = Strings(o["committedRequests"]),
                AppliedEventIds = Strings(o["appliedEventIds"]),
            };
            foreach (var p in ((JObject)o["flags"]).Properties())
            {
                var kind = (FactKind)Enum.Parse(typeof(FactKind), (string)p.Value["kind"]);
                s.Flags[p.Name] = FactValue.Deserialize(kind, (string)p.Value["value"]);
            }
            foreach (var p in ((JObject)o["metrics"]).Properties()) s.Metrics[p.Name] = (int)p.Value;
            foreach (var p in ((JObject)o["npcDispositions"]).Properties()) s.NpcDispositions[p.Name] = (int)p.Value;
            return s;
        }

        private static JObject IntMap(IDictionary<string, int> map)
        {
            var o = new JObject();
            foreach (var kv in map) o[kv.Key] = kv.Value;
            return o;
        }

        private static List<string> Strings(JToken t) => ((JArray)t).Select(x => (string)x).ToList();
    }
}
