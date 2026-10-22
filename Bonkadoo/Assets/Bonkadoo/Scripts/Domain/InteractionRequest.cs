using System;
using System.Collections.Generic;

namespace Bonkadoo.Domain
{
    /// <summary>Enter is sent by navigation when a scene is entered (ADR-0003); players never produce it.</summary>
    public enum InteractionVerb { Tap, Drag, UseItem, CombineItems, Talk, Inspect, Enter }

    public sealed class InteractionRequest
    {
        private static readonly IReadOnlyDictionary<string, FactValue> NoInput =
            new Dictionary<string, FactValue>(StringComparer.Ordinal);

        public string RequestId { get; }
        public InteractionVerb Verb { get; }
        public string SourceId { get; }
        public string TargetId { get; }
        public string SceneId { get; }
        public long Tick { get; }

        /// <summary>Transient <c>input.*</c> context; never persisted.</summary>
        public IReadOnlyDictionary<string, FactValue> Input { get; }

        public InteractionRequest(string requestId, InteractionVerb verb, string sourceId, string targetId,
            string sceneId, long tick, IReadOnlyDictionary<string, FactValue> input = null)
        {
            if (string.IsNullOrEmpty(requestId)) throw new ArgumentException("requestId required", nameof(requestId));
            if (string.IsNullOrEmpty(targetId)) throw new ArgumentException("targetId required", nameof(targetId));
            if (input != null)
            {
                foreach (var key in input.Keys)
                {
                    if (!key.StartsWith(WorldKeys.InputPrefix, StringComparison.Ordinal))
                        throw new ArgumentException($"Input key '{key}' must start with '{WorldKeys.InputPrefix}'");
                }
            }

            RequestId = requestId;
            Verb = verb;
            SourceId = sourceId;
            TargetId = targetId;
            SceneId = sceneId;
            Tick = tick;
            Input = input ?? NoInput;
        }
    }

    public static class WorldKeys
    {
        public const string InputPrefix = "input.";
        public static bool IsTransient(string key) => key.StartsWith(InputPrefix, StringComparison.Ordinal);
    }
}
