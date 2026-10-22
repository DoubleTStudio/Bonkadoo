using System;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Infrastructure.Localization;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Infrastructure.Settings;
using Bonkadoo.Infrastructure.Unity;
using Bonkadoo.Presentation.Audio;
using UnityEngine;

namespace Bonkadoo.Presentation
{
    /// <summary>Composition root in the Bootstrap scene. No network is required for anything here.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Load the menu after composing; tests turn this off.")]
        public bool loadMenu = true;

        public string LastError { get; private set; }

        private void Awake()
        {
            if (AppServices.Current != null)
            {
                if (loadMenu) Nav.Go(Nav.Menu);
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            try
            {
                AppServices.Current = Compose();
                AppServices.Current.Audio = gameObject.AddComponent<AudioService>();
                Debug.Log($"[Bonkadoo] content {AppServices.Current.Content.World.ContentVersion}: " +
                          $"{AppServices.Current.Content.Rules.Puzzles.Count} puzzles, lang {AppServices.Current.Localizer.Language}");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError("[Bonkadoo] bootstrap failed: " + ex);
                return;
            }
            AppServices.Current.Ads.Start();
            if (loadMenu) Nav.Go(Nav.Menu);
        }

        private static AppServices Compose()
        {
            var content = ContentLoader.Load(ResourcesContent.Load());
            var files = new DiskFileStore(UnityPaths.Saves);
            var settings = new SettingsStore(files);
            settings.BeginSession();
            var language = settings.Current.Language ?? DefaultLanguage();
            var localizer = new Localizer(content.Localization, language, Debug.isDebugBuild);
            var flow = new GameFlow(content, new SaveService(files), new UnityTickSource(0));
            var clock = new ActivePlayClock();
            var store = PlatformServices.CreateStore(settings);
            var ads = new AdCoordinator(new AdPolicy(), PlatformServices.CreateAds(), settings, clock, () => settings.IsFirstSession);
            var app = new AppServices(content, settings, localizer, flow, ads, store, clock);
            flow.SaveFailed += ex =>
            {
                app.LastSaveProblem = ex.Message;
                Debug.LogWarning("[Bonkadoo] autosave failed: " + ex.Message);
            };
            return app;
        }

        private void Update()
        {
            var app = AppServices.Current;
            if (app == null) return;
            var inGame = app.Flow.Session != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Nav.Gameplay;
            app.Clock.Tick(Time.unscaledDeltaTime, inGame, UnityEngine.Application.isFocused);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) AppServices.Current?.Flow.SaveNow();
        }

        private void OnApplicationQuit() => AppServices.Current?.Flow.SaveNow();

        private static string DefaultLanguage() =>
            UnityEngine.Application.systemLanguage == SystemLanguage.Vietnamese ? "vi" : "en";
    }
}
