using System;
using UnityEngine;

namespace Bonkadoo.Infrastructure.Unity
{
    /// <summary>
    /// Ad unit IDs. Real IDs are written to Resources/admob_ids.json by the build script from CI secrets
    /// (never committed); otherwise Google's public sample units are used, which only debug builds may show.
    /// </summary>
    [Serializable]
    public sealed class AdMobConfig
    {
        public const string ResourceName = "admob_ids";
        // https://developers.google.com/admob/android/test-ads and /ios/test-ads (public sample units)
        public const string SampleAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string SampleIosAppId = "ca-app-pub-3940256099942544~1458002511";
        private const string SamplePublisher = "ca-app-pub-3940256099942544";

        public string androidBanner = "ca-app-pub-3940256099942544/6300978111";
        public string androidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        public string iosBanner = "ca-app-pub-3940256099942544/2934735716";
        public string iosInterstitial = "ca-app-pub-3940256099942544/4411468910";

        public string BannerUnit => UnityEngine.Application.platform == RuntimePlatform.IPhonePlayer ? iosBanner : androidBanner;
        public string InterstitialUnit =>
            UnityEngine.Application.platform == RuntimePlatform.IPhonePlayer ? iosInterstitial : androidInterstitial;

        public bool UsesSampleIds => BannerUnit.StartsWith(SamplePublisher, StringComparison.Ordinal) ||
                                     InterstitialUnit.StartsWith(SamplePublisher, StringComparison.Ordinal);

        public static AdMobConfig Load()
        {
            var asset = Resources.Load<TextAsset>(ResourceName);
            if (asset == null) return new AdMobConfig();
            try { return JsonUtility.FromJson<AdMobConfig>(asset.text) ?? new AdMobConfig(); }
            catch (ArgumentException) { return new AdMobConfig(); }
        }
    }
}
