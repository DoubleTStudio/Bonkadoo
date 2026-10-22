using System;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Infrastructure.Settings;
using NUnit.Framework;

namespace Bonkadoo.Tests.Monetization
{
    public class AdPolicyTests
    {
        private static AdContext Eligible() => new AdContext
        {
            ConsentResolved = true, IsFirstSession = false, NaturalBreak = true, ActivePlaySecondsSinceInterstitial = 301,
        };

        private readonly AdPolicy _policy = new AdPolicy();

        [Test]
        public void At299SecondsNoInterstitial()
        {
            // TC-003
            var c = Eligible();
            c.ActivePlaySecondsSinceInterstitial = 299;
            Assert.AreEqual(AdDecision.TooSoon, _policy.EvaluateInterstitial(c));
        }

        [Test]
        public void At301SecondsDuringPuzzleNoInterstitial()
        {
            // TC-004
            var c = Eligible();
            c.NaturalBreak = false;
            c.PuzzleActive = true;
            Assert.IsFalse(_policy.CanShowInterstitial(c));
            c.NaturalBreak = true;
            Assert.AreEqual(AdDecision.InteractionActive, _policy.EvaluateInterstitial(c));
        }

        [Test]
        public void At301SecondsAtSceneBreakEligibleOnlyWithConsentAndAfterGrace()
        {
            // TC-005
            Assert.AreEqual(AdDecision.Show, _policy.EvaluateInterstitial(Eligible()));
            var noConsent = Eligible();
            noConsent.ConsentResolved = false;
            Assert.AreEqual(AdDecision.ConsentPending, _policy.EvaluateInterstitial(noConsent));
            var first = Eligible();
            first.IsFirstSession = true;
            Assert.AreEqual(AdDecision.FirstSessionGrace, _policy.EvaluateInterstitial(first));
        }

        [Test]
        public void DialogueBlocksInterstitial()
        {
            var c = Eligible();
            c.DialogueActive = true;
            Assert.AreEqual(AdDecision.InteractionActive, _policy.EvaluateInterstitial(c));
        }

        [Test]
        public void RemoveAdsMeansNoBannerAndNoInterstitial()
        {
            // TC-006
            var c = Eligible();
            c.RemoveAdsOwned = true;
            Assert.AreEqual(AdDecision.RemoveAdsOwned, _policy.EvaluateInterstitial(c));
            Assert.IsFalse(_policy.CanShowBanner(c));
        }
    }

    public class ActivePlayClockTests
    {
        [Test]
        public void BackgroundAndMenusDoNotCount()
        {
            var clock = new ActivePlayClock();
            clock.Tick(0.1, gameplayActive: true, appFocused: true);
            clock.Tick(600, gameplayActive: true, appFocused: false); // 10 min background
            clock.Tick(5, gameplayActive: false, appFocused: true);   // menu
            Assert.AreEqual(0.1, clock.SecondsSinceInterstitial, 1e-9);
        }

        [Test]
        public void ResumeSpikeIsClamped()
        {
            // Unity's first frame after resume reports the whole pause as delta
            var clock = new ActivePlayClock();
            clock.Tick(600, true, true);
            Assert.AreEqual(ActivePlayClock.MaxFrameSeconds, clock.SecondsSinceInterstitial, 1e-9);
        }
    }

    public class AdCoordinatorTests
    {
        private sealed class FakeAds : IAdService
        {
            public bool Ready = true, Throw, NeverCallsBack;
            public int Shows, Loads;
            public bool? Banner;
            public float BannerHeightPixels => 100f;
            public bool IsInterstitialReady => Throw ? throw new InvalidOperationException("sdk") : Ready;
            public void Initialize(Action<bool> consentResolved) { if (Throw) throw new InvalidOperationException("sdk"); consentResolved(true); }
            public void LoadInterstitial() { Loads++; if (Throw) throw new InvalidOperationException("sdk"); }
            public void ShowInterstitial(Action<bool> closed) { Shows++; if (!NeverCallsBack) closed(true); }
            public void SetBannerVisible(bool visible) { if (Throw) throw new InvalidOperationException("sdk"); Banner = visible; }
        }

        private sealed class Owned : IEntitlements
        {
            private bool _owned;
            public bool RemoveAdsOwned => _owned;
            public event Action<bool> Changed;
            public void Set(bool owned) { _owned = owned; Changed?.Invoke(owned); }
        }

        private FakeAds _ads;
        private Owned _owned;
        private ActivePlayClock _clock;
        private AdCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _ads = new FakeAds();
            _owned = new Owned();
            _clock = new ActivePlayClock();
            _coordinator = new AdCoordinator(new AdPolicy(), _ads, _owned, _clock, () => false) { BannerAllowedHere = true };
        }

        private void Play(double seconds)
        {
            for (var t = 0.0; t < seconds; t += 0.2) _clock.Tick(0.2, true, true);
        }

        [Test]
        public void ShowsAtNaturalBreakAfterFiveMinutesThenResetsClock()
        {
            _coordinator.Start();
            Play(310);
            Assert.IsTrue(_coordinator.OnNaturalBreak());
            Assert.AreEqual(1, _coordinator.Impressions);
            Assert.AreEqual(0, _clock.SecondsSinceInterstitial);
            Assert.IsFalse(_coordinator.OnNaturalBreak(), "frequency cap");
        }

        [Test]
        public void NoFillDoesNotBlockAndRequestsLoad()
        {
            _coordinator.Start();
            _ads.Ready = false;
            Play(310);
            var loads = _ads.Loads;
            Assert.IsFalse(_coordinator.OnNaturalBreak());
            Assert.Greater(_ads.Loads, loads);
            Assert.AreEqual(0, _ads.Shows);
        }

        [Test]
        public void SdkExceptionsNeverEscape()
        {
            _ads.Throw = true;
            Assert.DoesNotThrow(() => _coordinator.Start());
            Play(310);
            Assert.DoesNotThrow(() => _coordinator.OnNaturalBreak());
            Assert.DoesNotThrow(() => _coordinator.RefreshBanner());
        }

        [Test]
        public void RemoveAdsHidesBannerImmediatelyAndStopsInterstitials()
        {
            _coordinator.Start();
            Assert.AreEqual(true, _ads.Banner);
            _owned.Set(true);
            Assert.AreEqual(false, _ads.Banner);
            Play(400);
            Assert.IsFalse(_coordinator.OnNaturalBreak());
            Assert.AreEqual(AdDecision.RemoveAdsOwned, _coordinator.LastDecision);
        }

        [Test]
        public void BannerOnlyWhereSpaceIsReserved()
        {
            _coordinator.Start();
            _coordinator.BannerAllowedHere = false;
            Assert.AreEqual(false, _ads.Banner);
            Assert.AreEqual(0f, _coordinator.BannerHeightPixels);
            _coordinator.BannerAllowedHere = true;
            Assert.AreEqual(true, _ads.Banner);
            Assert.AreEqual(100f, _coordinator.BannerHeightPixels);
        }

        [Test]
        public void OwnedAtLaunchNeverInitializesAdSdk()
        {
            _owned.Set(true);
            _ads.Throw = true; // would throw if touched
            Assert.DoesNotThrow(() => _coordinator.Start());
            Assert.AreEqual(0, _ads.Loads);
        }

        [Test]
        public void NoQueuedAdWhileOneIsShowing()
        {
            _coordinator.Start();
            _ads.NeverCallsBack = true;
            Play(310);
            Assert.IsTrue(_coordinator.OnNaturalBreak());
            Play(310);
            Assert.IsFalse(_coordinator.OnNaturalBreak());
            Assert.AreEqual(1, _ads.Shows);
        }
    }

    public class SettingsStoreTests
    {
        [Test]
        public void PersistsAndClampsAndIsSeparateFromSaves()
        {
            var files = new InMemoryFileStore();
            var store = new SettingsStore(files);
            store.Update(s => { s.Language = "vi"; s.TextScale = 9f; s.MusicVolume = -1f; });
            var reloaded = new SettingsStore(files);
            Assert.AreEqual("vi", reloaded.Current.Language);
            Assert.AreEqual(1.6f, reloaded.Current.TextScale);
            Assert.AreEqual(0f, reloaded.Current.MusicVolume);
            Assert.IsTrue(files.Exists(SettingsStore.FileName));
            Assert.IsFalse(files.Exists("main.0.sav"));
        }

        [Test]
        public void FirstSessionGraceCountsLaunches()
        {
            var files = new InMemoryFileStore();
            var store = new SettingsStore(files);
            store.BeginSession();
            Assert.IsTrue(store.IsFirstSession);
            new SettingsStore(files).BeginSession();
            Assert.IsFalse(new SettingsStore(files).IsFirstSession);
        }

        [Test]
        public void EntitlementChangeNotifies()
        {
            var store = new SettingsStore(new InMemoryFileStore());
            bool? seen = null;
            ((IEntitlements)store).Changed += v => seen = v;
            store.SetRemoveAdsOwned(true);
            Assert.AreEqual(true, seen);
            Assert.IsTrue(store.RemoveAdsOwned);
        }

        [Test]
        public void CorruptSettingsFallBackToDefaults()
        {
            var files = new InMemoryFileStore();
            files.Write(SettingsStore.FileName, new byte[] { 0x7b, 0x7b });
            Assert.AreEqual(1f, new SettingsStore(files).Current.TextScale);
        }
    }
}
