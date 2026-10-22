using System;
using System.Collections.Generic;
using System.Linq;

namespace Bonkadoo.Domain
{
    /// <summary>What a predicate may read: committed world state plus the transient request.</summary>
    public readonly struct EvaluationContext
    {
        public IReadOnlyWorldState State { get; }
        public InteractionRequest Request { get; }

        public EvaluationContext(IReadOnlyWorldState state, InteractionRequest request)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            Request = request;
        }

        /// <summary><c>input.*</c> keys come only from the request; everything else from saved state.</summary>
        public FactValue? ResolveFlag(string key)
        {
            if (WorldKeys.IsTransient(key))
            {
                if (Request != null && Request.Input.TryGetValue(key, out var v)) return v;
                return null;
            }
            return State.GetFlag(key);
        }
    }

    /// <summary>Allowlisted predicate AST (ARCHITECTURE.md Predicate DSL + ADR-0002 request predicates).</summary>
    public abstract class Predicate
    {
        public abstract bool Evaluate(in EvaluationContext ctx);
    }

    public sealed class AllPredicate : Predicate
    {
        public IReadOnlyList<Predicate> Children { get; }
        public AllPredicate(IEnumerable<Predicate> children) => Children = children.ToArray();
        public override bool Evaluate(in EvaluationContext ctx)
        {
            foreach (var c in Children) if (!c.Evaluate(ctx)) return false;
            return true;
        }
    }

    public sealed class AnyPredicate : Predicate
    {
        public IReadOnlyList<Predicate> Children { get; }
        public AnyPredicate(IEnumerable<Predicate> children) => Children = children.ToArray();
        public override bool Evaluate(in EvaluationContext ctx)
        {
            foreach (var c in Children) if (c.Evaluate(ctx)) return true;
            return false;
        }
    }

    public sealed class NotPredicate : Predicate
    {
        public Predicate Child { get; }
        public NotPredicate(Predicate child) => Child = child ?? throw new ArgumentNullException(nameof(child));
        public override bool Evaluate(in EvaluationContext ctx) => !Child.Evaluate(ctx);
    }

    public sealed class FlagEqualsPredicate : Predicate
    {
        public string Key { get; }
        public FactValue Value { get; }
        public FlagEqualsPredicate(string key, FactValue value) { Key = key; Value = value; }

        public override bool Evaluate(in EvaluationContext ctx)
        {
            var actual = ctx.ResolveFlag(Key);
            if (actual.HasValue) return actual.Value.Equals(Value);
            // absent boolean flag reads as false
            return Value.Kind == FactKind.Bool && !Value.AsBool;
        }
    }

    public sealed class HasItemPredicate : Predicate
    {
        public string ItemId { get; }
        public HasItemPredicate(string itemId) => ItemId = itemId;
        public override bool Evaluate(in EvaluationContext ctx) => ctx.State.HasItem(ItemId);
    }

    public sealed class MetricAtLeastPredicate : Predicate
    {
        public string Key { get; }
        public int Value { get; }
        public MetricAtLeastPredicate(string key, int value) { Key = key; Value = value; }
        public override bool Evaluate(in EvaluationContext ctx) => ctx.State.GetMetric(Key) >= Value;
    }

    public sealed class NpcDispositionAtLeastPredicate : Predicate
    {
        public string NpcId { get; }
        public int Value { get; }
        public NpcDispositionAtLeastPredicate(string npcId, int value) { NpcId = npcId; Value = value; }
        public override bool Evaluate(in EvaluationContext ctx) => ctx.State.GetNpcDisposition(NpcId) >= Value;
    }

    public sealed class EventOccurredPredicate : Predicate
    {
        public string EventType { get; }
        public EventOccurredPredicate(string eventType) => EventType = eventType;
        public override bool Evaluate(in EvaluationContext ctx) => ctx.State.HasEvent(EventType);
    }

    public sealed class CurrentScenePredicate : Predicate
    {
        public string SceneId { get; }
        public CurrentScenePredicate(string sceneId) => SceneId = sceneId;
        public override bool Evaluate(in EvaluationContext ctx) =>
            string.Equals(ctx.State.CurrentSceneId, SceneId, StringComparison.Ordinal);
    }

    public sealed class RequestVerbPredicate : Predicate
    {
        public InteractionVerb Verb { get; }
        public RequestVerbPredicate(InteractionVerb verb) => Verb = verb;
        public override bool Evaluate(in EvaluationContext ctx) => ctx.Request != null && ctx.Request.Verb == Verb;
    }

    public sealed class RequestTargetPredicate : Predicate
    {
        public string TargetId { get; }
        public RequestTargetPredicate(string targetId) => TargetId = targetId;
        public override bool Evaluate(in EvaluationContext ctx) =>
            ctx.Request != null && string.Equals(ctx.Request.TargetId, TargetId, StringComparison.Ordinal);
    }

    public sealed class RequestSourcePredicate : Predicate
    {
        public string SourceId { get; }
        public RequestSourcePredicate(string sourceId) => SourceId = sourceId;
        public override bool Evaluate(in EvaluationContext ctx) =>
            ctx.Request != null && string.Equals(ctx.Request.SourceId, SourceId, StringComparison.Ordinal);
    }

    public sealed class TruePredicate : Predicate
    {
        public static readonly TruePredicate Instance = new TruePredicate();
        private TruePredicate() { }
        public override bool Evaluate(in EvaluationContext ctx) => true;
    }
}
