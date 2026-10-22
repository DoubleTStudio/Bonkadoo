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
#if BONKADOO_ADMOB
            return new AdMobAdService();
#else
            // release builds without the SDK integrated show no ads at all (D12, see PROGRESS)
            return Debug.isDebugBuild ? new SimulatedAdService() : (IAdService)new NullAdService();
#endif
        }

        public static IStoreService CreateStore(Settings.SettingsStore settings) => new UnityIapStore(settings);
    }
}
