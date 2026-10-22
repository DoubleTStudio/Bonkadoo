using System;
using System.Collections.Generic;
using System.Linq;

namespace Bonkadoo.Domain
{
    public sealed class Rule
    {
        public string Id { get; }
        public int Priority { get; }
        public Predicate When { get; }
        public IReadOnlyList<Effect> Effects { get; }
        public bool Once { get; }
        public string PuzzleId { get; }

        public Rule(string id, int priority, Predicate when, IEnumerable<Effect> effects, bool once, string puzzleId = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("rule id required", nameof(id));
            Id = id;
            Priority = priority;
            When = when ?? throw new ArgumentNullException(nameof(when));
            Effects = effects?.ToArray() ?? throw new ArgumentNullException(nameof(effects));
            Once = once;
            PuzzleId = puzzleId;
        }

        /// <summary>Priority descending, then ID ordinal ascending (ADR-0002).</summary>
        public static readonly IComparer<Rule> ResolutionOrder = Comparer<Rule>.Create((a, b) =>
        {
            var byPriority = b.Priority.CompareTo(a.Priority);
            return byPriority != 0 ? byPriority : string.CompareOrdinal(a.Id, b.Id);
        });
    }

    public sealed class PuzzleDefinition
    {
        public string Id { get; }
        public string SceneId { get; }
        public IReadOnlyList<string> Hotspots { get; }
        public IReadOnlyList<string> Prerequisites { get; }
        public string FailureKey { get; }
        public IReadOnlyList<string> HintKeys { get; }
        public string CompletionFlag => "flag." + Id + ".complete";

        public PuzzleDefinition(string id, string sceneId, IEnumerable<string> hotspots, IEnumerable<string> prerequisites,
            string failureKey, IEnumerable<string> hintKeys)
        {
            Id = id;
            SceneId = sceneId;
            Hotspots = hotspots.ToArray();
            Prerequisites = prerequisites.ToArray();
            FailureKey = failureKey;
            HintKeys = hintKeys.ToArray();
        }

        public bool PrerequisitesMet(IReadOnlyWorldState state)
        {
            foreach (var p in Prerequisites) if (!state.HasFlag(p)) return false;
            return true;
        }
    }

    /// <summary>Immutable, pre-sorted rule catalog plus puzzle metadata for fallbacks.</summary>
    public sealed class RuleSet
    {
        public const string DefaultFallbackKey = "dlg.common.fail";

        private readonly Rule[] _ordered;
        private readonly PuzzleDefinition[] _puzzles;
        private readonly Dictionary<string, PuzzleDefinition> _puzzlesById;

        public IReadOnlyList<Rule> OrderedRules => _ordered;
        public IReadOnlyList<PuzzleDefinition> Puzzles => _puzzles;

        public RuleSet(IEnumerable<Rule> rules, IEnumerable<PuzzleDefinition> puzzles = null)
        {
            _ordered = rules.ToArray();
            var dup = _ordered.GroupBy(r => r.Id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
            if (dup != null) throw new ArgumentException($"Duplicate rule id {dup.Key}");
            Array.Sort(_ordered, Rule.ResolutionOrder);

            _puzzles = (puzzles ?? Enumerable.Empty<PuzzleDefinition>()).ToArray();
            _puzzlesById = new Dictionary<string, PuzzleDefinition>(StringComparer.Ordinal);
            foreach (var p in _puzzles)
            {
                if (_puzzlesById.ContainsKey(p.Id)) throw new ArgumentException($"Duplicate puzzle id {p.Id}");
                _puzzlesById.Add(p.Id, p);
            }
        }

        public bool TryGetPuzzle(string id, out PuzzleDefinition puzzle) => _puzzlesById.TryGetValue(id, out puzzle);

        /// <summary>
        /// Prefers the active puzzle (prereqs met, not complete) that owns the target in this scene,
        /// then any puzzle in the scene that owns it, then the generic line.
        /// </summary>
        public string ResolveFallbackKey(IReadOnlyWorldState state, InteractionRequest request)
        {
            PuzzleDefinition anyOwner = null;
            foreach (var p in _puzzles)
            {
                if (!string.Equals(p.SceneId, state.CurrentSceneId, StringComparison.Ordinal)) continue;
                if (!p.Hotspots.Contains(request.TargetId)) continue;
                if (p.PrerequisitesMet(state) && !state.HasFlag(p.CompletionFlag)) return p.FailureKey ?? DefaultFallbackKey;
                if (anyOwner == null) anyOwner = p;
            }
            return anyOwner?.FailureKey ?? DefaultFallbackKey;
        }
    }
}
