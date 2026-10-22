using System;
using System.Collections.Generic;
using Bonkadoo.Domain;

namespace Bonkadoo.Application
{
    /// <summary>
    /// Owns authoritative world state for one play session: evaluates requests,
    /// appends events to the journal, then projects them. Presentation and dialogue only read.
    /// </summary>
    public sealed class GameSession
    {
        private readonly IInteractionEngine _engine;
        private readonly IEventStore _journal;
        private readonly WorldState _state;

        public WorldDefinition Definition { get; }
        public IReadOnlyWorldState State => _state;
        public IEventStore Journal => _journal;

        /// <summary>Raised after events are durably appended and projected (autosave hook).</summary>
        public event Action<IReadOnlyList<WorldEvent>> Committed;

        public GameSession(WorldDefinition definition, IInteractionEngine engine, IEventStore journal, WorldState state = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _journal = journal ?? throw new ArgumentNullException(nameof(journal));
            _state = state ?? new WorldState(definition);
        }

        /// <summary>Rebuilds state from an optional snapshot plus journal events after it.</summary>
        public static GameSession Restore(WorldDefinition definition, IInteractionEngine engine, WorldStateSnapshot snapshot,
            IReadOnlyList<WorldEvent> journal, long journalCursor)
        {
            if (journalCursor < 0 || journalCursor > journal.Count) throw new ArgumentOutOfRangeException(nameof(journalCursor));
            var state = snapshot != null ? WorldState.Restore(definition, snapshot) : new WorldState(definition);
            var store = new InMemoryEventStore(journal);
            // idempotent: events already in the snapshot are skipped by ID
            WorldStateProjector.ApplyAll(state, store.ReadFrom(snapshot != null ? journalCursor : 0));
            return new GameSession(definition, engine, store, state);
        }

        public WorldStateSnapshot Snapshot() => _state.ToSnapshot();

        public InteractionOutcome Interact(InteractionRequest request)
        {
            var outcome = _engine.Evaluate(request, _state);
            if (outcome.Status == OutcomeStatus.Committed) Commit(outcome.Events);
            return outcome;
        }

        /// <summary>Scene transitions are journaled so currentScene survives replay.</summary>
        public void EnterScene(string sceneId, string requestId, long tick)
        {
            if (string.IsNullOrEmpty(sceneId)) throw new ArgumentException("sceneId required", nameof(sceneId));
            if (string.Equals(_state.CurrentSceneId, sceneId, StringComparison.Ordinal)) return;
            var evt = new WorldEvent(requestId + ":scene", WorldEventTypes.SceneEntered,
                new Dictionary<string, string> { [PayloadKeys.SceneId] = sceneId }, tick, requestId);
            Commit(new[] { evt });
        }

        private void Commit(IReadOnlyList<WorldEvent> events)
        {
            if (events.Count == 0) return;
            _journal.AppendAtomic(events);
            WorldStateProjector.ApplyAll(_state, events);
            Committed?.Invoke(events);
        }
    }
}
