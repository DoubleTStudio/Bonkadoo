using System;
using Bonkadoo.Application;
using UnityEngine;

namespace Bonkadoo.Infrastructure.Unity
{
    /// <summary>
    /// Editor/development stand-in for the ad SDK: consent resolves immediately, interstitials are
    /// "shown" by the UI's test card, banner visibility is tracked. Never used in release builds.
    /// </summary>
    public sealed class SimulatedAdService : IAdService
    {
        public bool BannerVisible { get; private set; }
        public bool IsInterstitialReady { get; private set; }
        // a standard 320x50dp banner
        public float BannerHeightPixels => BannerVisible ? 50f * (Screen.dpi > 0 ? Screen.dpi : 160f) / 160f : 0f;
        public static event Action<Action<bool>> InterstitialRequested;

        public void Initialize(Action<bool> consentResolved) => consentResolved?.Invoke(false);
        public void LoadInterstitial() => IsInterstitialReady = true;

        public void ShowInterstitial(Action<bool> closed)
        {
            IsInterstitialReady = false;
            if (InterstitialRequested != null) InterstitialRequested(closed);
            else closed?.Invoke(true);
        }

        public void SetBannerVisible(bool visible) => BannerVisible = visible;
    }

    public static class PlatformServices
    {
        public static IAdService CreateAds()
        {
#if UNITY_EDITOR
            return new SimulatedAdService();
#elif BONKADOO_ADMOB
            // Google's sample ad units must never reach players: a release build without real IDs shows no ads
            var config = AdMobConfig.Load();
            if (config.UsesSampleIds && !Debug.isDebugBuild) return new NullAdService();
            return new AdMobAdService(config);
#else
            return Debug.isDebugBuild ? new SimulatedAdService() : (IAdService)new NullAdService();
#endif
        }

        public static IStoreService CreateStore(Settings.SettingsStore settings) => new UnityIapStore(settings);
    }
}
