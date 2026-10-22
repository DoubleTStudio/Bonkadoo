// Reference API contracts; implementation belongs in Unity project.
namespace Bonkadoo.Domain {
  public enum InteractionVerb { Tap, Drag, UseItem, CombineItems, Talk, Inspect }
  public record InteractionRequest(string RequestId, InteractionVerb Verb, string? SourceId, string TargetId, string SceneId, long Tick);
  public record WorldEvent(string Id, string Type, string PayloadJson, long Tick, string CausationId, int SchemaVersion);
  public record InteractionOutcome(bool Success, string? MatchedRuleId, string FeedbackKey, IReadOnlyList<WorldEvent> Events, IReadOnlyList<string> AnimationCues);
  public interface IInteractionEngine { InteractionOutcome Evaluate(InteractionRequest request, IReadOnlyWorldState state); }
  public interface IReadOnlyWorldState { bool HasFlag(string key); bool HasItem(string id); int GetMetric(string key); bool HasEvent(string type); }
  public interface IEventStore { IReadOnlyList<WorldEvent> ReadFrom(long cursor); void AppendAtomic(IReadOnlyList<WorldEvent> events); }
  public interface ISaveService { void SaveCheckpoint(string slotId); bool TryRestore(string slotId); }
  public interface IAdPolicy { bool CanShowInterstitial(AdContext context); }
  public record AdContext(bool RemoveAdsOwned, bool IsFirstSession, double ActivePlaySecondsSinceInterstitial, bool NaturalBreak, bool DialogueActive, bool PuzzleActive);
}
