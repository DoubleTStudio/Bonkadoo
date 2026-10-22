using System;
using System.Collections.Generic;
using System.Globalization;

namespace Bonkadoo.Domain
{
    public sealed class EffectValidationException : Exception
    {
        public string Code { get; }
        public EffectValidationException(string code, string message) : base(message) => Code = code;
    }

    /// <summary>
    /// Dry-run context: every emitted event is applied to a scratch copy immediately,
    /// so later effects see earlier ones and nothing touches the real state until commit.
    /// </summary>
    public sealed class EffectPlan
    {
        private readonly List<WorldEvent> _events = new List<WorldEvent>();
        private readonly List<string> _dialogue = new List<string>();
        private readonly List<string> _animations = new List<string>();

        public WorldState Scratch { get; }
        public InteractionRequest Request { get; }
        public string CausationId { get; }
        public IReadOnlyList<WorldEvent> Events => _events;
        public IReadOnlyList<string> DialogueKeys => _dialogue;
        public IReadOnlyList<string> AnimationCues => _animations;

        public EffectPlan(WorldState committed, InteractionRequest request, string causationId)
        {
            Scratch = committed.Clone();
            Request = request;
            CausationId = causationId;
        }

        public void Emit(string type, IDictionary<string, string> payload)
        {
            var id = Request.RequestId + ":" + _events.Count.ToString("D3", CultureInfo.InvariantCulture);
            var evt = new WorldEvent(id, type, payload, Request.Tick, CausationId);
            WorldStateProjector.Apply(Scratch, evt);
            _events.Add(evt);
        }

        public void QueueDialogue(string key) => _dialogue.Add(key);
        public void QueueAnimation(string cue) => _animations.Add(cue);
    }

    /// <summary>Allowlisted effect DSL (ARCHITECTURE.md).</summary>
    public abstract class Effect
    {
        public abstract void Plan(EffectPlan plan);

        protected static Dictionary<string, string> P(params string[] kv)
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
            return d;
        }
    }

    public sealed class SetFlagEffect : Effect
    {
        public string Key { get; }
        public FactValue Value { get; }
        public SetFlagEffect(string key, FactValue value) { Key = key; Value = value; }

        public override void Plan(EffectPlan plan)
        {
            if (WorldKeys.IsTransient(Key))
                throw new EffectValidationException("transient_write", $"setFlag cannot write transient key '{Key}'");
            plan.Emit(WorldEventTypes.FlagSet,
                P(PayloadKeys.Key, Key, PayloadKeys.Kind, Value.Kind.ToString(), PayloadKeys.Value, Value.Serialize()));
        }
    }

    public sealed class AdjustMetricEffect : Effect
    {
        public string Key { get; }
        public int Delta { get; }
        public AdjustMetricEffect(string key, int delta) { Key = key; Delta = delta; }

        public override void Plan(EffectPlan plan)
        {
            if (!plan.Scratch.Definition.TryGetMetric(Key, out var def))
                throw new EffectValidationException("unknown_metric", $"adjustMetric: unknown metric '{Key}'");
            var current = plan.Scratch.GetMetric(Key);
            var next = def.Clamp(current + Delta);
            if (next == current) return;
            plan.Emit(WorldEventTypes.MetricSet,
                P(PayloadKeys.Key, Key, PayloadKeys.Value, next.ToString(CultureInfo.InvariantCulture)));
        }
    }

    public sealed class AddItemEffect : Effect
    {
        public string ItemId { get; }
        public AddItemEffect(string itemId) => ItemId = itemId;

        public override void Plan(EffectPlan plan)
        {
            // inventory holds unique items; re-adding is a no-op
            if (plan.Scratch.HasItem(ItemId)) return;
            plan.Emit(WorldEventTypes.ItemAdded, P(PayloadKeys.ItemId, ItemId));
        }
    }

    public sealed class RemoveItemEffect : Effect
    {
        public string ItemId { get; }
        public RemoveItemEffect(string itemId) => ItemId = itemId;

        public override void Plan(EffectPlan plan)
        {
            if (!plan.Scratch.HasItem(ItemId))
                throw new EffectValidationException("item_not_held", $"removeItem: '{ItemId}' not in inventory");
            plan.Emit(WorldEventTypes.ItemRemoved, P(PayloadKeys.ItemId, ItemId));
        }
    }

    public sealed class EmitEventEffect : Effect
    {
        public string EventType { get; }
        public EmitEventEffect(string eventType) => EventType = eventType;

        public override void Plan(EffectPlan plan)
        {
            if (WorldEventTypes.IsSystem(EventType))
                throw new EffectValidationException("reserved_event", $"emitEvent cannot emit system type '{EventType}'");
            plan.Emit(EventType, null);
        }
    }

    public sealed class QueueDialogueEffect : Effect
    {
        public string Key { get; }
        public QueueDialogueEffect(string key) => Key = key;
        public override void Plan(EffectPlan plan) => plan.QueueDialogue(Key);
    }

    public sealed class QueueAnimationEffect : Effect
    {
        public string Cue { get; }
        public QueueAnimationEffect(string cue) => Cue = cue;
        public override void Plan(EffectPlan plan) => plan.QueueAnimation(Cue);
    }

    public sealed class UnlockExitEffect : Effect
    {
        public string ExitId { get; }
        public UnlockExitEffect(string exitId) => ExitId = exitId;

        public override void Plan(EffectPlan plan)
        {
            if (plan.Scratch.IsExitUnlocked(ExitId)) return;
            plan.Emit(WorldEventTypes.ExitUnlocked, P(PayloadKeys.ExitId, ExitId));
        }
    }

    public sealed class SetNpcDispositionEffect : Effect
    {
        public string NpcId { get; }
        public int Value { get; }
        public SetNpcDispositionEffect(string npcId, int value) { NpcId = npcId; Value = value; }

        public override void Plan(EffectPlan plan)
        {
            var range = plan.Scratch.Definition.NpcDisposition;
            if (Value < range.Min || Value > range.Max)
                throw new EffectValidationException("out_of_range",
                    $"setNpcDisposition: {NpcId}={Value} outside [{range.Min},{range.Max}]");
            plan.Emit(WorldEventTypes.NpcDisposition,
                P(PayloadKeys.NpcId, NpcId, PayloadKeys.Value, Value.ToString(CultureInfo.InvariantCulture)));
        }
    }
}
