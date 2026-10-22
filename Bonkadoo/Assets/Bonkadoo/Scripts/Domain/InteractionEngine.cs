using System;
using System.Collections.Generic;

namespace Bonkadoo.Domain
{
    public enum OutcomeStatus
    {
        /// <summary>A rule matched; Events must be committed.</summary>
        Committed,
        /// <summary>No rule matched; localized non-silent feedback only.</summary>
        Fallback,
        /// <summary>Request ID already committed; nothing to do.</summary>
        Duplicate,
        /// <summary>A rule matched but its effects failed validation, or the request was stale. Nothing commits.</summary>
        Rejected,
    }

    public sealed class InteractionOutcome
    {
        private static readonly IReadOnlyList<WorldEvent> NoEvents = Array.Empty<WorldEvent>();
        private static readonly IReadOnlyList<string> NoStrings = Array.Empty<string>();

        public OutcomeStatus Status { get; }
        public bool Success => Status == OutcomeStatus.Committed;
        public string MatchedRuleId { get; }
        public string FeedbackKey { get; }
        public IReadOnlyList<WorldEvent> Events { get; }
        public IReadOnlyList<DialogueLine> DialogueLines { get; }
        public IReadOnlyList<string> DialogueKeys { get; }
        public IReadOnlyList<string> Conversations { get; }
        public IReadOnlyList<string> AnimationCues { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        public InteractionOutcome(OutcomeStatus status, string matchedRuleId, string feedbackKey,
            IReadOnlyList<WorldEvent> events = null, IReadOnlyList<DialogueLine> dialogueLines = null,
            IReadOnlyList<string> animationCues = null, string errorCode = null, string errorMessage = null,
            IReadOnlyList<string> conversations = null)
        {
            Status = status;
            MatchedRuleId = matchedRuleId;
            FeedbackKey = feedbackKey;
            Events = events ?? NoEvents;
            DialogueLines = dialogueLines ?? Array.Empty<DialogueLine>();
            var keys = new string[DialogueLines.Count];
            for (var i = 0; i < keys.Length; i++) keys[i] = DialogueLines[i].Key;
            DialogueKeys = keys;
            Conversations = conversations ?? NoStrings;
            AnimationCues = animationCues ?? NoStrings;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }

    public interface IInteractionEngine
    {
        /// <summary>Pure: never mutates <paramref name="state"/>. The caller commits <see cref="InteractionOutcome.Events"/>.</summary>
        InteractionOutcome Evaluate(InteractionRequest request, WorldState state);
    }

    public sealed class InteractionEngine : IInteractionEngine
    {
        private readonly RuleSet _rules;

        public InteractionEngine(RuleSet rules) => _rules = rules ?? throw new ArgumentNullException(nameof(rules));

        public InteractionOutcome Evaluate(InteractionRequest request, WorldState state)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (state.IsRequestCommitted(request.RequestId))
                return new InteractionOutcome(OutcomeStatus.Duplicate, null, null);

            if (request.SceneId != null && !string.Equals(request.SceneId, state.CurrentSceneId, StringComparison.Ordinal))
                return new InteractionOutcome(OutcomeStatus.Rejected, null, _rules.ResolveFallbackKey(state, request),
                    errorCode: "stale_scene",
                    errorMessage: $"Request scene '{request.SceneId}' != current '{state.CurrentSceneId}'");

            var ctx = new EvaluationContext(state, request);
            foreach (var rule in _rules.OrderedRules)
            {
                if (rule.Once && state.HasRuleFired(rule.Id)) continue;
                if (!rule.When.Evaluate(ctx)) continue;
                return Plan(rule, request, state);
            }

            return new InteractionOutcome(OutcomeStatus.Fallback, null, _rules.ResolveFallbackKey(state, request));
        }

        private InteractionOutcome Plan(Rule rule, InteractionRequest request, WorldState state)
        {
            var plan = new EffectPlan(state, request, request.RequestId);
            try
            {
                foreach (var effect in rule.Effects) effect.Plan(plan);
                if (rule.Once) plan.Emit(WorldEventTypes.RuleFired, new Dictionary<string, string> { [PayloadKeys.RuleId] = rule.Id });
                plan.Emit(WorldEventTypes.RequestCommitted,
                    new Dictionary<string, string> { [PayloadKeys.RequestId] = request.RequestId });
            }
            catch (EffectValidationException ex)
            {
                return new InteractionOutcome(OutcomeStatus.Rejected, rule.Id, _rules.ResolveFallbackKey(state, request),
                    errorCode: ex.Code, errorMessage: $"{rule.Id}: {ex.Message}");
            }

            var feedback = plan.DialogueLines.Count > 0 ? plan.DialogueLines[0].Key : null;
            return new InteractionOutcome(OutcomeStatus.Committed, rule.Id, feedback, plan.Events, plan.DialogueLines,
                plan.AnimationCues, conversations: plan.Conversations);
        }
    }
}
