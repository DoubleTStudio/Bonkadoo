using Bonkadoo.Infrastructure;
using Bonkadoo.Presentation.Ui;
using UnityEngine;

namespace Bonkadoo.Presentation.Screens
{
    public sealed class CreditsScreen : ScreenBase
    {
        protected override Color BackgroundColor => UiKit.Ink;

        protected override void Build()
        {
            App.Audio?.PlayMusic("calm");
            var ending = App.Flow.Session != null && App.Flow.GameComplete ? App.Flow.CurrentEnding : Ending.None;
            if (ending != Ending.None)
            {
                var key = "ui.ending." + ending.ToString().ToLowerInvariant();
                UiKit.Label("EndingTitle", Root, key + ".title", 80, UiKit.Accent).GetComponent<RectTransform>()
                    .Anchor(new Vector2(0.1f, 0.78f), new Vector2(0.9f, 0.95f));
                UiKit.Label("EndingBody", Root, key, 40, Color.white).GetComponent<RectTransform>()
                    .Anchor(new Vector2(0.15f, 0.62f), new Vector2(0.85f, 0.78f));
            }
            UiKit.Label("CreditsTitle", Root, "ui.credits.title", 56, Color.white).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.1f, 0.5f), new Vector2(0.9f, 0.6f));
            UiKit.Label("CreditsBody", Root, "ui.credits.body", 34, UiKit.Muted).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.15f, 0.16f), new Vector2(0.85f, 0.5f));
            UiKit.Button("Credits:Back", Root, "ui.back", () => Screens.Go(Screens.Menu), UiKit.Accent).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.4f, 0.03f), new Vector2(0.6f, 0.13f));
        }
    }
}
