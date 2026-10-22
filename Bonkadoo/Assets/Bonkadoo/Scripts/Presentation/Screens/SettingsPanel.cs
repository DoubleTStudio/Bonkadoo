using Bonkadoo.Infrastructure.Settings;
using Bonkadoo.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Screens
{
    /// <summary>Overlay usable from menu and pause. Every option applies immediately, including language.</summary>
    public static class SettingsPanel
    {
        public static void Open(ScreenBase host)
        {
            var app = AppServices.Current;
            var blocker = UiKit.Image("Settings", host.GetComponentInChildren<Canvas>().transform, new Color(0, 0, 0, 0.7f), raycast: true);
            blocker.rectTransform.Stretch();
            var safe = UiKit.Rect("Safe", blocker.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var card = UiKit.Image("SettingsCard", safe, UiKit.Panel, raycast: true).rectTransform
                .Anchor(new Vector2(0.12f, 0.04f), new Vector2(0.88f, 0.96f));
            UiKit.Column(card, 10, 28);
            UiKit.Label("Title", card, "ui.settings.title", 56, Color.white).Size(-1, 90);

            Row(card, "ui.settings.language", r =>
            {
                foreach (var lang in app.Localizer.Languages)
                {
                    var l = lang;
                    UiKit.Button("Settings:Lang:" + l, r, "ui.lang." + l, () => app.SetLanguage(l),
                        app.Localizer.Language == l ? UiKit.Accent : UiKit.Calm).Size(220);
                }
            });
            Row(card, "ui.settings.text_size", r =>
            {
                for (var i = 0; i < SettingsStore.TextScales.Length; i++)
                {
                    var scale = SettingsStore.TextScales[i];
                    UiKit.Button("Settings:Text:" + i, r, "ui.settings.text_size_" + i, () => app.Settings.Update(s => s.TextScale = scale),
                        Mathf.Approximately(app.Settings.Current.TextScale, scale) ? UiKit.Accent : UiKit.Calm).Size(200);
                }
            });
            Slider(card, "ui.settings.music", app.Settings.Current.MusicVolume, v => app.Settings.Update(s => s.MusicVolume = v));
            Slider(card, "ui.settings.sfx", app.Settings.Current.SfxVolume, v => app.Settings.Update(s => s.SfxVolume = v));
            Slider(card, "ui.settings.speed", (app.Settings.Current.DialogueSpeed - 0.5f) / 1.5f,
                v => app.Settings.Update(s => s.DialogueSpeed = 0.5f + v * 1.5f));
            Toggle(card, "Settings:ReducedMotion", "ui.settings.reduced_motion", () => app.Settings.Current.ReducedMotion,
                v => app.Settings.Update(s => s.ReducedMotion = v));
            Toggle(card, "Settings:Captions", "ui.settings.captions", () => app.Settings.Current.Captions,
                v => app.Settings.Update(s => s.Captions = v));
            Toggle(card, "Settings:Haptics", "ui.settings.haptics", () => app.Settings.Current.Haptics,
                v => app.Settings.Update(s => s.Haptics = v));

            var actions = UiKit.Rect("Actions", card);
            UiKit.Row(actions, 20).Size(-1, 110);
            UiKit.Button("Settings:Restore", actions, "ui.menu.restore", () => app.Store.RestorePurchases(ok =>
                host.Toast(ok ? "ui.purchase.restored" : "ui.purchase.nothing"))).Size(420);
            UiKit.Button("Settings:Privacy", actions, "ui.settings.privacy", () => host.Toast("ui.settings.privacy_text")).Size(420);
            UiKit.Button("Settings:Close", actions, "ui.close", () => Object.Destroy(blocker.gameObject), UiKit.Accent).Size(300);
        }

        private static void Row(RectTransform card, string labelKey, System.Action<RectTransform> fill)
        {
            var row = UiKit.Rect("Row", card);
            UiKit.Row(row, 14).Size(-1, 96);
            UiKit.Label("Label", row, labelKey, 36, Color.white, TextAnchor.MiddleLeft).Size(420);
            fill(row);
        }

        private static void Slider(RectTransform card, string labelKey, float value, System.Action<float> changed)
        {
            Row(card, labelKey, row =>
            {
                var bg = UiKit.Image("Slider", row, UiKit.Muted, raycast: true);
                bg.Size(-1, 60, 1);
                var slider = bg.gameObject.AddComponent<UnityEngine.UI.Slider>();
                var fillArea = UiKit.Rect("Fill Area", bg.transform).Stretch(10, 20, 10, 20);
                var fill = UiKit.Image("Fill", fillArea, UiKit.Accent);
                fill.rectTransform.Stretch();
                var handleArea = UiKit.Rect("Handle Area", bg.transform).Stretch(20, 0, 20, 0);
                var handle = UiKit.Image("Handle", handleArea, Color.white, raycast: true);
                handle.rectTransform.sizeDelta = new Vector2(48, 0);
                slider.fillRect = fill.rectTransform;
                slider.handleRect = handle.rectTransform;
                slider.targetGraphic = handle;
                slider.value = Mathf.Clamp01(value);
                slider.onValueChanged.AddListener(v => changed(v));
            });
        }

        private static void Toggle(RectTransform card, string name, string labelKey, System.Func<bool> get, System.Action<bool> set)
        {
            Row(card, labelKey, row =>
            {
                Button button = null;
                button = UiKit.Button(name, row, get() ? "ui.settings.on" : "ui.settings.off", () =>
                {
                    set(!get());
                    button.SetLabel(get() ? "ui.settings.on" : "ui.settings.off");
                    button.image.color = get() ? UiKit.Accent : UiKit.Calm;
                }, get() ? UiKit.Accent : UiKit.Calm);
                button.Size(220);
            });
        }
    }
}
