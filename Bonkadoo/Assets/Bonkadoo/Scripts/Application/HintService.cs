using System;
using System.Globalization;
using Bonkadoo.Domain;

namespace Bonkadoo.Application
{
    /// <summary>
    /// Three-tier hint ladder per active puzzle (PROGRESSION_AND_HINTS). Hints are always available on request;
    /// idle time or repeated failures only raise a nudge, never an automatic spoiler.
    /// </summary>
    public sealed class HintService
    {
        public const double NudgeIdleSeconds = 45;
        public const int NudgeFailedAttempts = 3;
        public const int MaxLevel = 3;

        private readonly GameSession _session;
        private readonly RuleSet _rules;
        private double _idleSeconds;
        private int _failures;
        private string _trackedPuzzle;

        public bool NudgeAvailable { get; private set; }
        public event Action NudgeRaised;

        public HintService(GameSession session, RuleSet rules, InteractionController controller = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (controller != null) controller.Resolved += (r, o) => OnResolved(r, o);
        }

        public PuzzleDefinition ActivePuzzle => _rules.ActivePuzzle(_session.State);

        public static string LevelKey(string puzzleId) => "hint." + puzzleId;

        public int LevelFor(string puzzleId)
        {
            var v = _session.State.GetFlag(LevelKey(puzzleId));
            return v.HasValue && v.Value.Kind == FactKind.Int ? v.Value.AsInt : 0;
        }

        /// <summary>Reveals the next hint for the active puzzle; returns its localization key or null if none.</summary>
        public string RevealNext(string requestId, long tick)
        {
            var puzzle = ActivePuzzle;
            if (puzzle == null || puzzle.HintKeys.Count == 0) return null;
            var level = Math.Min(LevelFor(puzzle.Id) + 1, Math.Min(MaxLevel, puzzle.HintKeys.Count));
            if (level != LevelFor(puzzle.Id)) _session.SetFact(LevelKey(puzzle.Id), FactValue.Of(level), requestId, tick);
            ResetNudge();
            return puzzle.HintKeys[level - 1];
        }

        /// <summary>The most recent hint already revealed (for re-reading), or null.</summary>
        public string CurrentHintKey()
        {
            var puzzle = ActivePuzzle;
            if (puzzle == null) return null;
            var level = LevelFor(puzzle.Id);
            return level == 0 ? null : puzzle.HintKeys[Math.Min(level, puzzle.HintKeys.Count) - 1];
        }

        public void Tick(double deltaSeconds)
        {
            if (deltaSeconds <= 0 || ActivePuzzle == null) return;
            _idleSeconds += deltaSeconds;
            if (_idleSeconds >= NudgeIdleSeconds) RaiseNudge();
        }

        private void OnResolved(InteractionRequest request, InteractionOutcome outcome)
        {
            var active = ActivePuzzle?.Id;
            if (!string.Equals(active, _trackedPuzzle, StringComparison.Ordinal))
            {
                _trackedPuzzle = active;
                ResetNudge();
            }
            _idleSeconds = 0;
            if (request.Verb == InteractionVerb.Enter) return;
            if (outcome.Status == OutcomeStatus.Fallback || outcome.Status == OutcomeStatus.Rejected)
            {
                if (++_failures >= NudgeFailedAttempts) RaiseNudge();
            }
        }

        private void RaiseNudge()
        {
            if (NudgeAvailable) return;
            NudgeAvailable = true;
            NudgeRaised?.Invoke();
        }

        private void ResetNudge()
        {
            NudgeAvailable = false;
            _failures = 0;
            _idleSeconds = 0;
        }
    }
}
