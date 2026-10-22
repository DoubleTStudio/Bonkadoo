using System;
using System.Collections.Generic;
using Bonkadoo.Application;
using Bonkadoo.Domain;

namespace Bonkadoo.Infrastructure.Save
{
    /// <summary>
    /// Saves on every committed event batch (which includes scene changes) and on app background.
    /// Save failures are reported, never thrown into gameplay.
    /// </summary>
    public sealed class AutosaveCoordinator : IDisposable
    {
        private readonly GameSession _session;
        private readonly CheckpointService _checkpoints;
        private readonly Func<SaveMetadata> _metadata;
        private readonly string _slotId;

        public int SaveCount { get; private set; }
        public Exception LastError { get; private set; }
        public event Action<Exception> SaveFailed;

        public AutosaveCoordinator(GameSession session, CheckpointService checkpoints, Func<SaveMetadata> metadata,
            string slotId = CheckpointService.PrimarySlot)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _checkpoints = checkpoints ?? throw new ArgumentNullException(nameof(checkpoints));
            _metadata = metadata ?? (() => new SaveMetadata());
            _slotId = slotId;
            _session.Committed += OnCommitted;
        }

        public void OnApplicationBackground() => SaveNow();

        public bool SaveNow()
        {
            try
            {
                _checkpoints.SaveCheckpoint(_slotId, _session, _metadata());
                SaveCount++;
                LastError = null;
                return true;
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException ||
                                       ex is InvalidOperationException)
            {
                LastError = ex;
                SaveFailed?.Invoke(ex);
                return false;
            }
        }

        private void OnCommitted(IReadOnlyList<WorldEvent> events) => SaveNow();

        public void Dispose() => _session.Committed -= OnCommitted;
    }
}
