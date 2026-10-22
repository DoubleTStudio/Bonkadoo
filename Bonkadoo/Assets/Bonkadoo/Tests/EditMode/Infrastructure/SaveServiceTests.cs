using System;
using System.IO;
using System.Linq;
using System.Text;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Tests.Support;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Bonkadoo.Tests.Infrastructure
{
    public class SaveServiceTests
    {
        private InMemoryFileStore _files;
        private SaveService _saves;
        private WorldDefinition _def;
        private InteractionEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _files = new InMemoryFileStore();
            _saves = new SaveService(_files);
            _def = TestContent.World();
            _engine = new InteractionEngine(new RuleSet(new[]
            {
                T.Rule("r.x", 1, T.Target("x"), new AdjustMetricEffect("evidence_count", 1), new AddItemEffect("item.x"),
                    new SetFlagEffect("flag.mood", FactValue.Of("tạm ổn"))),
            }));
        }

        private GameSession NewSession() =>
            new GameSession(_def, _engine, new InMemoryEventStore(), T.StateIn(_def, "scene.test"));

        private static SaveGame Game(string checkpoint) => new SaveGame { ContentVersion = "1", CheckpointId = checkpoint, SceneId = "s" };

        [Test]
        public void RoundTripsSessionThroughCheckpoint()
        {
            var session = NewSession();
            session.Interact(T.Req("x", id: "r1"));
            session.Interact(T.Req("x", id: "r2"));
            var checkpoints = new CheckpointService(_saves, _def, _engine);
            checkpoints.SaveCheckpoint("main", session, new SaveMetadata { ChapterId = "ch01", CheckpointId = "cp1" });

            Assert.IsTrue(checkpoints.TryRestore("main", out var restored, out var result));
            Assert.AreEqual(LoadStatus.Ok, result.Status);
            Assert.AreEqual(2, restored.State.GetMetric("evidence_count"));
            Assert.IsTrue(restored.State.HasItem("item.x"));
            Assert.AreEqual(FactValue.Of("tạm ổn"), restored.State.GetFlag("flag.mood"), "Vietnamese text survives");
            Assert.AreEqual(session.Journal.Count, restored.Journal.Count);
            Assert.AreEqual(OutcomeStatus.Duplicate, restored.Interact(T.Req("x", id: "r1")).Status, "idempotent after resume");
        }

        [Test]
        public void RestoreFromJournalAloneMatchesSnapshot()
        {
            var session = NewSession();
            for (var i = 0; i < 5; i++) session.Interact(T.Req("x", id: "r" + i));
            var fromJournal = GameSession.Restore(_def, _engine, null, session.Journal.ReadFrom(0).ToList(), 0);
            // init scene event lives outside the journal in this fixture
            Assert.AreEqual(session.State.GetMetric("evidence_count"), fromJournal.State.GetMetric("evidence_count"));
            Assert.AreEqual(session.State.HasItem("item.x"), fromJournal.State.HasItem("item.x"));
        }

        [Test]
        public void SnapshotPlusTailReplaysOnlyNewEvents()
        {
            var session = NewSession();
            session.Interact(T.Req("x", id: "r1"));
            var snapshot = session.Snapshot();
            var cursor = session.Journal.Count;
            session.Interact(T.Req("x", id: "r2"));
            var restored = GameSession.Restore(_def, _engine, snapshot, session.Journal.ReadFrom(0).ToList(), cursor);
            Assert.AreEqual(2, restored.State.GetMetric("evidence_count"));
        }

        [Test]
        public void WritesAlternateBetweenSlotFiles()
        {
            _saves.Save("main", Game("cp1"));
            _saves.Save("main", Game("cp2"));
            _saves.Save("main", Game("cp3"));
            Assert.IsTrue(_files.Exists("main.0.sav"));
            Assert.IsTrue(_files.Exists("main.1.sav"));
            Assert.IsFalse(_files.Files.Keys.Any(k => k.EndsWith(".tmp")));
            var load = _saves.Load("main");
            Assert.AreEqual("cp3", load.Game.CheckpointId);
            Assert.AreEqual(2, load.Game.Sequence);
        }

        [Test]
        public void InterruptedWriteKeepsPreviousCheckpoint()
        {
            // TC-002
            _saves.Save("main", Game("cp1"));
            _saves.Save("main", Game("cp2"));
            _files.FailNextWriteAfterBytes = 40;
            Assert.Throws<IOException>(() => _saves.Save("main", Game("cp3")));
            var load = _saves.Load("main");
            Assert.AreEqual(LoadStatus.Ok, load.Status, "temp file never replaced a slot");
            Assert.AreEqual("cp2", load.Game.CheckpointId);

            _saves.Save("main", Game("cp4"));
            Assert.AreEqual("cp4", _saves.Load("main").Game.CheckpointId, "recovers and keeps saving");
        }

        [Test]
        public void InterruptedReplaceKeepsPreviousCheckpoint()
        {
            _saves.Save("main", Game("cp1"));
            _files.FailNextReplace = true;
            Assert.Throws<IOException>(() => _saves.Save("main", Game("cp2")));
            Assert.AreEqual("cp1", _saves.Load("main").Game.CheckpointId);
        }

        [Test]
        public void CorruptNewestSlotFallsBackToOlder()
        {
            _saves.Save("main", Game("cp1"));
            _saves.Save("main", Game("cp2")); // lands in main.1.sav
            _files.Corrupt("main.1.sav", 30, (byte)'#');
            var load = _saves.Load("main");
            Assert.AreEqual(LoadStatus.Recovered, load.Status);
            Assert.AreEqual("cp1", load.Game.CheckpointId);
            Assert.IsNotEmpty(load.Problems);
        }

        [Test]
        public void TamperedButValidJsonFailsChecksum()
        {
            _saves.Save("main", Game("cp1"));
            var doc = JObject.Parse(Encoding.UTF8.GetString(_files.Read("main.0.sav")));
            doc["checkpointId"] = "cheat";
            _files.Write("main.0.sav", Encoding.UTF8.GetBytes(doc.ToString(Formatting.None)));
            Assert.AreEqual(LoadStatus.Corrupt, _saves.Load("main").Status);
        }

        [Test]
        public void BothSlotsCorruptReportsCorruptWithoutThrowing()
        {
            _files.Write("main.0.sav", Encoding.UTF8.GetBytes("{not json"));
            _files.Write("main.1.sav", new byte[0]);
            var load = _saves.Load("main");
            Assert.AreEqual(LoadStatus.Corrupt, load.Status);
            Assert.IsNull(load.Game);
        }

        [Test]
        public void EmptySlot()
        {
            Assert.AreEqual(LoadStatus.Empty, _saves.Load("main").Status);
        }

        [Test]
        public void SlotsAreIndependent()
        {
            // TC-010 groundwork: a branch slot never touches the primary files
            _saves.Save("main", Game("primary"));
            _saves.Save("replay_ch01", Game("branch"));
            _saves.Save("replay_ch01", Game("branch2"));
            Assert.AreEqual("primary", _saves.Load("main").Game.CheckpointId);
            Assert.AreEqual(0, _saves.Load("main").Game.Sequence);
        }

        [Test]
        public void FutureVersionIsNeitherLoadedNorOverwritten()
        {
            var json = SaveGameJson.ToJson(Game("future"));
            json["saveVersion"] = SaveGame.CurrentVersion + 1;
            json["checksum"] = SaveService.Checksum(json);
            var bytes = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
            _files.Write("main.0.sav", bytes);

            Assert.AreEqual(LoadStatus.FutureVersion, _saves.Load("main").Status);
            Assert.Throws<InvalidOperationException>(() => _saves.Save("main", Game("old build")));
            CollectionAssert.AreEqual(bytes, _files.Read("main.0.sav"));
        }

        [TestCase("main/../x")]
        [TestCase("")]
        [TestCase("slot vượng")]
        public void RejectsUnsafeSlotIds(string slot)
        {
            Assert.Throws<ArgumentException>(() => _saves.Save(slot, Game("x")));
        }

        [Test]
        public void ChecksumIsStableAcrossSerializations()
        {
            var a = SaveService.Serialize(Game("cp"));
            var b = SaveService.Serialize(Game("cp"));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void DiskStoreRoundTripsAndReplacesAtomically()
        {
            var dir = Path.Combine(Path.GetTempPath(), "bonkadoo-save-" + Guid.NewGuid().ToString("N"));
            try
            {
                var saves = new SaveService(new DiskFileStore(dir));
                saves.Save("main", Game("cp1"));
                saves.Save("main", Game("cp2"));
                saves.Save("main", Game("cp3"));
                Assert.AreEqual("cp3", saves.Load("main").Game.CheckpointId);
                Assert.IsEmpty(Directory.GetFiles(dir, "*.tmp"));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }

    public class SaveMigrationTests
    {
        private sealed class RenameField : ISaveMigration
        {
            public int FromVersion { get; set; }
            public JObject Migrate(JObject save)
            {
                save["chapterId"] = "migrated-from-v" + FromVersion;
                save["saveVersion"] = FromVersion + 1;
                return save;
            }
        }

        private sealed class BrokenStep : ISaveMigration
        {
            public int FromVersion => 1;
            public JObject Migrate(JObject save) => save; // forgets to bump version
        }

        private static JObject V(int version) =>
            new JObject { ["saveVersion"] = version, ["chapterId"] = "ch01" };

        [Test]
        public void AppliesChainInOrder()
        {
            var m = new SaveMigrator(3, new[] { new RenameField { FromVersion = 2 }, new RenameField { FromVersion = 1 } });
            var result = m.Upgrade(V(1));
            Assert.AreEqual(3, (int)result["saveVersion"]);
            Assert.AreEqual("migrated-from-v2", (string)result["chapterId"]);
        }

        [Test]
        public void CurrentVersionPassesThroughUnchanged()
        {
            var input = V(SaveGame.CurrentVersion);
            Assert.IsTrue(JToken.DeepEquals(input, SaveMigrator.Default.Upgrade(input)));
        }

        [Test]
        public void MissingStepFails()
        {
            var m = new SaveMigrator(3, new[] { new RenameField { FromVersion = 1 } });
            Assert.Throws<SaveMigrationException>(() => m.Upgrade(V(1)));
        }

        [Test]
        public void StepThatDoesNotAdvanceVersionFails()
        {
            Assert.Throws<SaveMigrationException>(() => new SaveMigrator(2, new ISaveMigration[] { new BrokenStep() }).Upgrade(V(1)));
        }

        [Test]
        public void DoesNotMutateInput()
        {
            var input = V(1);
            new SaveMigrator(2, new[] { new RenameField { FromVersion = 1 } }).Upgrade(input);
            Assert.AreEqual(1, (int)input["saveVersion"]);
        }

        [Test]
        public void OldSaveOnDiskIsMigratedOnLoad()
        {
            var files = new InMemoryFileStore();
            var json = SaveGameJson.ToJson(new SaveGame { CheckpointId = "cp" });
            json["saveVersion"] = 0;
            json["checksum"] = SaveService.Checksum(json);
            files.Write("main.0.sav", Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));

            var saves = new SaveService(files, new SaveMigrator(1, new[] { new RenameField { FromVersion = 0 } }));
            var load = saves.Load("main");
            Assert.AreEqual(LoadStatus.Ok, load.Status);
            Assert.AreEqual("migrated-from-v0", load.Game.ChapterId);
        }
    }
}
