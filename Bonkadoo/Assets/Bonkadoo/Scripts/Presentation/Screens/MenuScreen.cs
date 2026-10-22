using Bonkadoo.Application;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Presentation.Audio;
using Bonkadoo.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Screens
{
    public sealed class MenuScreen : ScreenBase
    {
        private Button _removeAds;

        protected override Color BackgroundColor => UiKit.Hex("#f2e3c6");

        protected override void Build()
        {
            App.Audio?.PlayMusic("calm");
            var title = UiKit.Label("Title", Root, "ui.title", 150, UiKit.Ink);
            title.GetComponent<RectTransform>().Anchor(new Vector2(0.05f, 0.7f), new Vector2(0.95f, 0.95f));
            UiKit.Label("Subtitle", Root, "ui.subtitle", 48, UiKit.Accent).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.1f, 0.62f), new Vector2(0.9f, 0.72f));

            var column = UiKit.Rect("Buttons", Root).Anchor(new Vector2(0.3f, 0.08f), new Vector2(0.7f, 0.6f));
            UiKit.Column(column, 18);
            var hasSave = App.Flow.HasMainSave;
            if (hasSave) UiKit.Button("Menu:Continue", column, "ui.menu.continue", Continue, UiKit.Accent).Size(-1, 100);
            UiKit.Button("Menu:New", column, "ui.menu.new", () =>
            {
                if (hasSave) Confirm("ui.confirm.new_game", NewGame);
                else NewGame();
            }, hasSave ? UiKit.Calm : UiKit.Accent).Size(-1, 100);
            if (hasSave) UiKit.Button("Menu:Chapters", column, "ui.menu.chapters", () =>
            {
                if (App.Flow.Session != null || App.Flow.Continue()) Screens.Go(Screens.ChapterHub);
            }).Size(-1, 100);
            UiKit.Button("Menu:Settings", column, "ui.menu.settings", () => SettingsPanel.Open(this)).Size(-1, 100);
            _removeAds = UiKit.Button("Menu:RemoveAds", column, "ui.menu.remove_ads", BuyRemoveAds);
            _removeAds.Size(-1, 100);
            UiKit.Button("Menu:Credits", column, "ui.menu.credits", () => Screens.Go(Screens.Credits)).Size(-1, 100);
            RefreshRemoveAds();
            App.Settings.Changed += OnSettingsChanged;

            if (App.Flow.MainSaveStatus == LoadStatus.Corrupt) Toast("ui.save.corrupt");
        }

        private void OnDestroy()
        {
            if (App != null) App.Settings.Changed -= OnSettingsChanged;
        }

        private void OnSettingsChanged(Infrastructure.Settings.GameSettings _) => RefreshRemoveAds();

        private void RefreshRemoveAds()
        {
            var owned = App.Settings.RemoveAdsOwned;
            _removeAds.interactable = !owned;
            var label = _removeAds.GetComponentInChildren<LocalizedText>();
            label.Key = owned ? "ui.menu.remove_ads_owned" : "ui.menu.remove_ads";
            if (!owned) label.SetArgs(App.Store.LocalizedPrice ?? App.T("ui.menu.price_fallback"));
        }

        private void Continue()
        {
            App.Audio?.Play(Cue.Tap);
            if (App.Flow.Continue()) Screens.Go(Screens.Gameplay);
            else Toast("ui.save.corrupt");
        }

        private void NewGame()
        {
            App.Audio?.Play(Cue.Tap);
            App.Flow.NewGame();
            Screens.Go(Screens.Gameplay);
        }

        private void BuyRemoveAds()
        {
            App.Store.PurchaseRemoveAds(result =>
            {
                switch (result)
                {
                    case PurchaseResult.Purchased: Toast("ui.purchase.success"); break;
                    case PurchaseResult.Cancelled: break;
                    case PurchaseResult.Pending: Toast("ui.purchase.pending"); break;
                    case PurchaseResult.Unavailable: Toast("ui.purchase.unavailable"); break;
                    default: Toast("ui.purchase.failed"); break;
                }
                RefreshRemoveAds();
            });
        }
    }
}
