#if BONKADOO_ADMOB
using System;
using Bonkadoo.Application;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Bonkadoo.Infrastructure.Unity
{
    /// <summary>
    /// Google Mobile Ads adapter: UMP consent first, then a fixed 320x50 top banner (the UI reserves its
    /// height) and interstitials shown only when AdCoordinator says so. Every SDK failure degrades to "no ad".
    /// </summary>
    public sealed class AdMobAdService : IAdService
    {
        private readonly AdMobConfig _config;
        private BannerView _banner;
        private InterstitialAd _interstitial;
        private bool _ready, _wantBanner, _loading;

        public AdMobAdService(AdMobConfig config) => _config = config ?? new AdMobConfig();

        public bool IsInterstitialReady => _interstitial != null && _interstitial.CanShowAd();

        public float BannerHeightPixels => _banner != null && _wantBanner ? _banner.GetHeightInPixels() : 0f;

        public void Initialize(Action<bool> consentResolved)
        {
            MainThread.EnsureCreated();
            // GMA's UMP layer dispatches its callbacks through this executor; it must exist before consent
            GoogleMobileAds.Common.MobileAdsEventExecutor.Initialize();
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            Log("requesting consent info");
            // UMP and init callbacks can arrive off the main thread; Unity objects must be touched on it
            ConsentInformation.Update(new ConsentRequestParameters(), updateError => Main(() =>
            {
                Log("consent info: " + (updateError?.Message ?? "ok"));
                if (updateError != null)
                {
                    // consent info unavailable (e.g. offline): only proceed if a previous consent allows requests
                    Finish(consentResolved);
                    return;
                }
                ConsentForm.LoadAndShowConsentFormIfRequired(_ => Main(() => Finish(consentResolved)));
            }));
        }

        private void Finish(Action<bool> consentResolved)
        {
            Log("can request ads: " + ConsentInformation.CanRequestAds());
            if (!ConsentInformation.CanRequestAds())
            {
                consentResolved?.Invoke(false);
                return;
            }
            MobileAds.Initialize(_ => Main(() =>
            {
                _ready = true;
                Log("sdk initialized; banner wanted: " + _wantBanner);
                if (_wantBanner) EnsureBanner();
                consentResolved?.Invoke(true);
            }));
        }

        public void LoadInterstitial()
        {
            if (!_ready || _loading || IsInterstitialReady) return;
            _loading = true;
            InterstitialAd.Load(_config.InterstitialUnit, new AdRequest(), (ad, error) => Main(() =>
            {
                _loading = false;
                Log("interstitial " + (error == null ? "loaded" : "failed: " + error.GetMessage()));
                if (error != null || ad == null) return;
                _interstitial?.Destroy();
                _interstitial = ad;
            }));
        }

        public void ShowInterstitial(Action<bool> closed)
        {
            var ad = _interstitial;
            if (ad == null || !ad.CanShowAd())
            {
                closed?.Invoke(false);
                return;
            }
            var impression = false;
            var done = false;
            void Complete()
            {
                if (done) return;
                done = true;
                _interstitial = null;
                ad.Destroy();
                closed?.Invoke(impression);
            }
            ad.OnAdImpressionRecorded += () => impression = true;
            ad.OnAdFullScreenContentClosed += Complete;
            ad.OnAdFullScreenContentFailed += _ => Complete();
            ad.Show();
        }

        public void SetBannerVisible(bool visible)
        {
            _wantBanner = visible;
            if (!_ready) return;
            if (visible) EnsureBanner();
            else _banner?.Hide();
        }

        private static void Log(string message) => Debug.Log("[Bonkadoo] admob: " + message);

        // GMA's own executor only exists after MobileAds.Initialize, too late for the consent callbacks
        private static void Main(Action action) => MainThread.Post(action);

        private void EnsureBanner()
        {
            if (_banner == null)
            {
                _banner = new BannerView(_config.BannerUnit, AdSize.Banner, AdPosition.Top);
                _banner.OnBannerAdLoaded += () => Log("banner loaded");
                _banner.OnBannerAdLoadFailed += e => Log("banner failed: " + e.GetMessage());
                _banner.LoadAd(new AdRequest());
            }
            _banner.Show();
        }
    }
}
#endif
