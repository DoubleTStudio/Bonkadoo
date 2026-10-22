namespace Bonkadoo.Domain
{
    public sealed class AdContext
    {
        public bool RemoveAdsOwned;
        public bool IsFirstSession;
        public double ActivePlaySecondsSinceInterstitial;
        public bool NaturalBreak;
        public bool DialogueActive;
        public bool PuzzleActive;
        /// <summary>Consent flow finished (any outcome); ads are never requested before that.</summary>
        public bool ConsentResolved;
    }

    public enum AdDecision
    {
        Show,
        RemoveAdsOwned,
        ConsentPending,
        FirstSessionGrace,
        NotNaturalBreak,
        InteractionActive,
        TooSoon,
    }

    public interface IAdPolicy
    {
        AdDecision EvaluateInterstitial(AdContext context);
        bool CanShowInterstitial(AdContext context);
        bool CanShowBanner(AdContext context);
    }

    /// <summary>ADS_AND_IAP fixed rules: >=300 s active play, natural break only, never mid-dialogue/puzzle,
    /// first-session grace, Remove Ads overrides everything.</summary>
    public sealed class AdPolicy : IAdPolicy
    {
        public const double MinActiveSeconds = 300;

        public AdDecision EvaluateInterstitial(AdContext c)
        {
            if (c.RemoveAdsOwned) return AdDecision.RemoveAdsOwned;
            if (!c.ConsentResolved) return AdDecision.ConsentPending;
            if (c.IsFirstSession) return AdDecision.FirstSessionGrace;
            if (!c.NaturalBreak) return AdDecision.NotNaturalBreak;
            if (c.DialogueActive || c.PuzzleActive) return AdDecision.InteractionActive;
            if (c.ActivePlaySecondsSinceInterstitial < MinActiveSeconds) return AdDecision.TooSoon;
            return AdDecision.Show;
        }

        public bool CanShowInterstitial(AdContext context) => EvaluateInterstitial(context) == AdDecision.Show;

        public bool CanShowBanner(AdContext c) => !c.RemoveAdsOwned && c.ConsentResolved;
    }
}
