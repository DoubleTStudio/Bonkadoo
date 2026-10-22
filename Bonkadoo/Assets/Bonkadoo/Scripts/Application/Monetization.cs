using System;
using Bonkadoo.Domain;

namespace Bonkadoo.Application
{
    /// <summary>Counts active gameplay only; background and menus never count (ADS_AND_IAP).</summary>
    public sealed class ActivePlayClock
    {
        /// <summary>Unity reports a huge delta after resume; one frame never counts for more than this.</summary>
        public const double MaxFrameSeconds = 0.25;

        public double SecondsSinceInterstitial { get; private set; }

        public void Tick(double deltaSeconds, bool gameplayActive, bool appFocused)
        {
            if (!gameplayActive || !appFocused || deltaSeconds <= 0) return;
            SecondsSinceInterstitial += Math.Min(deltaSeconds, MaxFrameSeconds);
        }

        public void ResetAfterInterstitial() => SecondsSinceInterstitial = 0;
    }

    public interface IAdService
    {
        bool IsInterstitialReady { get; }
        void Initialize(Action<bool> consentResolved);
        void LoadInterstitial();
        /// <summary>Calls back exactly once with whether an impression happened. Must never throw or block.</summary>
        void ShowInterstitial(Action<bool> closed);
        void SetBannerVisible(bool visible);
        /// <summary>Screen pixels the banner occupies at the top edge (0 when hidden or unknown).</summary>
        float BannerHeightPixels { get; }
    }

    public interface IEntitlements
    {
        bool RemoveAdsOwned { get; }
        event Action<bool> Changed;
    }

    public interface IStoreService
    {
        string RemoveAdsProductId { get; }
        bool IsReady { get; }
        string LocalizedPrice { get; }
        void PurchaseRemoveAds(Action<PurchaseResult> done);
        void RestorePurchases(Action<bool> done);
    }

    public enum PurchaseResult { Purchased, Cancelled, Failed, Pending, Unavailable }

    /// <summary>Decides at natural breaks; the transition always proceeds whether or not an ad shows.</summary>
    public sealed class AdCoordinator
    {
        private readonly IAdPolicy _policy;
        private readonly IAdService _ads;
        private readonly IEntitlements _entitlements;
        private readonly ActivePlayClock _clock;
        private readonly Func<bool> _isFirstSession;

        public bool ConsentResolved { get; private set; }
        public bool DialogueActive { get; set; }
        public bool PuzzleActive { get; set; }
        public bool ShowingInterstitial { get; private set; }
        private bool _bannerAllowedHere;

        /// <summary>Banners appear only on surfaces that reserve space for them (gameplay), never menus.</summary>
        public bool BannerAllowedHere
        {
            get => _bannerAllowedHere;
            set { _bannerAllowedHere = value; RefreshBanner(); }
        }

        public float BannerHeightPixels
        {
            get
            {
                try { return BannerVisible ? _ads.BannerHeightPixels : 0f; }
                catch (Exception) { return 0f; }
            }
        }
        public int Impressions { get; private set; }
        public AdDecision LastDecision { get; private set; }
        /// <summary>Whether the banner should currently occupy its reserved strip.</summary>
        public bool BannerVisible { get; private set; }
        public event Action<bool> BannerChanged;
        /// <summary>SDK failures are contained, but reported so they can be logged.</summary>
        public event Action<Exception> SdkError;

        public AdCoordinator(IAdPolicy policy, IAdService ads, IEntitlements entitlements, ActivePlayClock clock,
            Func<bool> isFirstSession)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _entitlements = entitlements ?? throw new ArgumentNullException(nameof(entitlements));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _isFirstSession = isFirstSession ?? (() => false);
            _entitlements.Changed += _ => RefreshBanner();
        }

        public void Start()
        {
            if (_entitlements.RemoveAdsOwned)
            {
                ConsentResolved = true;
                RefreshBanner();
                return;
            }
            try
            {
                _ads.Initialize(_ =>
                {
                    ConsentResolved = true;
                    RefreshBanner();
                    SafeLoad();
                });
            }
            catch (Exception ex)
            {
                // ad SDK failure must never block play
                ConsentResolved = false;
                SdkError?.Invoke(ex);
            }
        }

        public AdContext Context(bool naturalBreak) => new AdContext
        {
            RemoveAdsOwned = _entitlements.RemoveAdsOwned,
            IsFirstSession = _isFirstSession(),
            ActivePlaySecondsSinceInterstitial = _clock.SecondsSinceInterstitial,
            NaturalBreak = naturalBreak,
            DialogueActive = DialogueActive,
            PuzzleActive = PuzzleActive,
            ConsentResolved = ConsentResolved,
        };

        /// <summary>Call at a scene/chapter break. Returns true if an interstitial was requested.</summary>
        public bool OnNaturalBreak()
        {
            LastDecision = _policy.EvaluateInterstitial(Context(true));
            if (LastDecision != AdDecision.Show || ShowingInterstitial) return false;
            if (!SafeReady())
            {
                SafeLoad();
                return false;
            }
            ShowingInterstitial = true;
            try
            {
                _ads.ShowInterstitial(shown =>
                {
                    ShowingInterstitial = false;
                    if (shown)
                    {
                        Impressions++;
                        _clock.ResetAfterInterstitial();
                    }
                    SafeLoad();
                });
            }
            catch (Exception ex)
            {
                ShowingInterstitial = false;
                SdkError?.Invoke(ex);
                return false;
            }
            return true;
        }

        public void RefreshBanner()
        {
            var visible = _bannerAllowedHere && _policy.CanShowBanner(Context(false));
            try { _ads.SetBannerVisible(visible); }
            catch (Exception ex)
            {
                visible = false;
                SdkError?.Invoke(ex);
            }
            if (visible == BannerVisible) return;
            BannerVisible = visible;
            BannerChanged?.Invoke(visible);
        }

        private bool SafeReady()
        {
            try { return _ads.IsInterstitialReady; }
            catch (Exception) { return false; }
        }

        private void SafeLoad()
        {
            if (_entitlements.RemoveAdsOwned) return;
            try { _ads.LoadInterstitial(); }
            catch (Exception ex) { SdkError?.Invoke(ex); }
        }
    }

    /// <summary>No ads at all: Remove Ads owned, unsupported platform, or SDK absent. Offline-safe.</summary>
    public sealed class NullAdService : IAdService
    {
        public bool IsInterstitialReady => false;
        public bool BannerVisible { get; private set; }
        public float BannerHeightPixels => 0f;
        public void Initialize(Action<bool> consentResolved) => consentResolved?.Invoke(false);
        public void LoadInterstitial() { }
        public void ShowInterstitial(Action<bool> closed) => closed?.Invoke(false);
        public void SetBannerVisible(bool visible) => BannerVisible = visible;
    }
}
