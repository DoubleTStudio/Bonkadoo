using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bonkadoo.Infrastructure.Save
{
    public enum LoadStatus
    {
        Ok,
        /// <summary>Newest slot file was unreadable; the older valid one was used.</summary>
        Recovered,
        Empty,
        /// <summary>No readable slot. Files are left untouched for support/diagnosis.</summary>
        Corrupt,
        /// <summary>Written by a newer build; refuse to load and refuse to overwrite.</summary>
        FutureVersion,
    }

    public sealed class LoadResult
    {
        public LoadStatus Status;
        public SaveGame Game;
        public List<string> Problems = new List<string>();
    }

    /// <summary>
    /// Two rotating files per slot (<c>slot.0.sav</c>/<c>slot.1.sav</c>). Each write goes to the
    /// older file through temp-then-replace, so an interrupted write never destroys the newest good save.
    /// </summary>
    public sealed class SaveService
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private readonly IFileStore _files;
        private readonly SaveMigrator _migrator;

        public SaveService(IFileStore files, SaveMigrator migrator = null)
        {
            _files = files ?? throw new ArgumentNullException(nameof(files));
            _migrator = migrator ?? SaveMigrator.Default;
        }

        public static string FileName(string slotId, int index) => $"{slotId}.{index}.sav";

        public void Save(string slotId, SaveGame game)
        {
            ValidateSlot(slotId);
            if (game == null) throw new ArgumentNullException(nameof(game));

            var a = ReadSlotFile(slotId, 0);
            var b = ReadSlotFile(slotId, 1);
            if (a.Status == LoadStatus.FutureVersion || b.Status == LoadStatus.FutureVersion)
                throw new InvalidOperationException($"Slot '{slotId}' holds a save from a newer build; not overwriting");

            var seqA = a.Game?.Sequence ?? -1;
            var seqB = b.Game?.Sequence ?? -1;
            // overwrite whichever file is older or invalid
            var target = seqA <= seqB ? 0 : 1;
            game.Sequence = Math.Max(seqA, seqB) + 1;
            game.SaveVersion = SaveGame.CurrentVersion;

            var bytes = Utf8.GetBytes(Serialize(game));
            var name = FileName(slotId, target);
            var temp = name + ".tmp";
            _files.Write(temp, bytes);
            _files.Replace(temp, name);
        }

        public LoadResult Load(string slotId)
        {
            ValidateSlot(slotId);
            var a = ReadSlotFile(slotId, 0);
            var b = ReadSlotFile(slotId, 1);
            var result = new LoadResult();
            result.Problems.AddRange(a.Problems);
            result.Problems.AddRange(b.Problems);

            if (a.Status == LoadStatus.FutureVersion || b.Status == LoadStatus.FutureVersion)
            {
                result.Status = LoadStatus.FutureVersion;
                return result;
            }

            var best = a.Game == null ? b.Game : b.Game == null ? a.Game : (a.Game.Sequence >= b.Game.Sequence ? a.Game : b.Game);
            if (best == null)
            {
                result.Status = a.Status == LoadStatus.Empty && b.Status == LoadStatus.Empty ? LoadStatus.Empty : LoadStatus.Corrupt;
                return result;
            }

            result.Game = best;
            // a damaged file means we may have fallen back to the older save
            result.Status = a.Status == LoadStatus.Corrupt || b.Status == LoadStatus.Corrupt ? LoadStatus.Recovered : LoadStatus.Ok;
            return result;
        }

        public void DeleteSlot(string slotId)
        {
            ValidateSlot(slotId);
            for (var i = 0; i < 2; i++)
            {
                _files.Delete(FileName(slotId, i));
                _files.Delete(FileName(slotId, i) + ".tmp");
            }
        }

        public static string Serialize(SaveGame game)
        {
            var json = SaveGameJson.ToJson(game);
            json["checksum"] = Checksum(json);
            return json.ToString(Formatting.None);
        }

        /// <summary>SHA-256 of the canonical document without its checksum field.</summary>
        public static string Checksum(JObject doc)
        {
            var copy = (JObject)doc.DeepClone();
            copy.Remove("checksum");
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Utf8.GetBytes(copy.ToString(Formatting.None)));
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var x in hash) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        private LoadResult ReadSlotFile(string slotId, int index)
        {
            var name = FileName(slotId, index);
            var r = new LoadResult();
            if (!_files.Exists(name))
            {
                r.Status = LoadStatus.Empty;
                return r;
            }
            try
            {
                var text = Utf8.GetString(_files.Read(name));
                var doc = JsonConvert.DeserializeObject<JObject>(text,
                    new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                if (doc == null) throw new JsonException("empty document");
                var stored = (string)doc["checksum"];
                if (stored == null || !string.Equals(stored, Checksum(doc), StringComparison.Ordinal))
                    throw new InvalidOperationException("checksum mismatch");
                var upgraded = _migrator.Upgrade(doc);
                r.Game = SaveGameJson.FromJson(upgraded);
                r.Status = LoadStatus.Ok;
            }
            catch (SaveMigrationException ex) when (ex.IsFutureVersion)
            {
                r.Status = LoadStatus.FutureVersion;
                r.Problems.Add($"{name}: {ex.Message}");
            }
            catch (Exception ex) when (ex is JsonException || ex is InvalidOperationException || ex is InvalidCastException
                                       || ex is NullReferenceException || ex is FormatException || ex is ArgumentException
                                       || ex is SaveMigrationException || ex is System.IO.IOException)
            {
                r.Status = LoadStatus.Corrupt;
                r.Problems.Add($"{name}: {ex.GetType().Name}: {ex.Message}");
            }
            return r;
        }

        private static void ValidateSlot(string slotId)
        {
            if (string.IsNullOrEmpty(slotId)) throw new ArgumentException("slotId required");
            foreach (var c in slotId)
                if (!(char.IsLetterOrDigit(c) && c < 128) && c != '_' && c != '-')
                    throw new ArgumentException($"Invalid slot id '{slotId}'");
        }
    }
}
