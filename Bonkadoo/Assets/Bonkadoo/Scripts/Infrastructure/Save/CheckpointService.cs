using System;
using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;

namespace Bonkadoo.Infrastructure.Save
{
    public sealed class SaveMetadata
    {
        public string LocalProfileId = "local";
        public string ChapterId;
        public string CheckpointId;
    }

    /// <summary>Maps a <see cref="GameSession"/> to and from save slots (snapshot + full journal).</summary>
    public sealed class CheckpointService
    {
        public const string PrimarySlot = "main";

        private readonly SaveService _saves;
        private readonly WorldDefinition _world;
        private readonly IInteractionEngine _engine;

        public CheckpointService(SaveService saves, WorldDefinition world, IInteractionEngine engine)
        {
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        }

        public void SaveCheckpoint(string slotId, GameSession session, SaveMetadata meta)
        {
            var journal = session.Journal.ReadFrom(0);
            _saves.Save(slotId, new SaveGame
            {
                ContentVersion = _world.ContentVersion,
                LocalProfileId = meta?.LocalProfileId ?? "local",
                ChapterId = meta?.ChapterId,
                SceneId = session.State.CurrentSceneId,
                CheckpointId = meta?.CheckpointId,
                WorldState = session.Snapshot(),
                EventJournal = journal.ToList(),
                JournalCursor = journal.Count,
            });
        }

        public bool TryRestore(string slotId, out GameSession session, out LoadResult result)
        {
            session = null;
            result = _saves.Load(slotId);
            if (result.Game == null) return false;
            var g = result.Game;
            session = GameSession.Restore(_world, _engine, g.WorldState, g.EventJournal, g.JournalCursor);
            return true;
        }
    }
}
