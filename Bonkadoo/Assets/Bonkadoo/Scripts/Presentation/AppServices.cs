using System;
using Bonkadoo.Application;
using Bonkadoo.Infrastructure;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Infrastructure.Localization;
using Bonkadoo.Infrastructure.Settings;
using Bonkadoo.Presentation.Audio;
using UnityEngine.SceneManagement;

namespace Bonkadoo.Presentation
{
    public static class Nav
    {
        public const string Bootstrap = "Bootstrap";
        public const string Menu = "Menu";
        public const string ChapterHub = "ChapterHub";
        public const string Gameplay = "Gameplay";
        public const string Credits = "Credits";

        public static void Go(string scene) => SceneManager.LoadScene(scene);
    }

    /// <summary>Composition root output; one per app run. Everything here works offline.</summary>
    public sealed class AppServices
    {
        public static AppServices Current { get; internal set; }

        /// <summary>Raised when language or text scale changes so every label re-renders without restart.</summary>
        public static event Action LanguageOrScaleChanged;

        public GameContent Content { get; }
        public SettingsStore Settings { get; }
        public Localizer Localizer { get; }
        public GameFlow Flow { get; }
        public AdCoordinator Ads { get; }
        public IStoreService Store { get; }
        public ActivePlayClock Clock { get; }
        public AudioService Audio { get; internal set; }
        public string LastSaveProblem { get; internal set; }

        public AppServices(GameContent content, SettingsStore settings, Localizer localizer, GameFlow flow, AdCoordinator ads,
            IStoreService store, ActivePlayClock clock)
        {
            Content = content;
            Settings = settings;
            Localizer = localizer;
            Flow = flow;
            Ads = ads;
            Store = store;
            Clock = clock;
            Localizer.LanguageChanged += _ => LanguageOrScaleChanged?.Invoke();
            Settings.Changed += _ => LanguageOrScaleChanged?.Invoke();
        }

        public static void Reset()
        {
            Current = null;
        }

        public void SetLanguage(string language)
        {
            Settings.Update(s => s.Language = language);
            Localizer.SetLanguage(language);
        }

        public string T(string key) => Localizer.Get(key);

        public string Speaker(string speakerId) =>
            string.IsNullOrEmpty(speakerId) || speakerId == "narrator" ? string.Empty : Localizer.Get("ui.speaker." + speakerId);
    }
}
