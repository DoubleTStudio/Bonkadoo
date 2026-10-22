using Bonkadoo.Presentation.Ui;
using UnityEngine;

namespace Bonkadoo.Presentation.Screens
{
    public sealed class ChapterHubScreen : ScreenBase
    {
        protected override Color BackgroundColor => UiKit.Hex("#e8e1d3");

        protected override void Build()
        {
            if (App.Flow.Session == null && !App.Flow.Continue())
            {
                Screens.Go(Screens.Menu);
                return;
            }
            UiKit.Label("Title", Root, "ui.hub.title", 72, UiKit.Ink).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.1f, 0.86f), new Vector2(0.9f, 0.98f));
            UiKit.Button("Hub:Back", Root, "ui.back", () => Screens.Go(Screens.Menu)).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.01f, 0.88f), new Vector2(0.12f, 0.98f));

            var list = UiKit.Rect("Chapters", Root).Anchor(new Vector2(0.08f, 0.04f), new Vector2(0.92f, 0.84f));
            UiKit.Column(list, 14);
            var current = App.Flow.CurrentChapter?.Id;
            foreach (var ch in App.Content.Chapters)
            {
                var row = UiKit.Image("Chapter:" + ch.Id, list, UiKit.Panel).rectTransform;
                row.Size(-1, 140);
                UiKit.Row(row, 16, 12);
                var unlocked = App.Flow.IsChapterUnlocked(ch.Id);
                var complete = App.Flow.IsChapterComplete(ch.Id);
                var name = UiKit.Label("Name", row, unlocked ? ch.TitleKey : "ui.hub.locked", 44, Color.white, TextAnchor.MiddleLeft);
                name.Size(-1, -1, 1);
                var status = UiKit.Label("Status", row, complete ? "ui.hub.complete" : ch.Id == current ? "ui.hub.current" : "", 32, UiKit.Muted);
                status.Size(260);
                if (!unlocked) continue;
                var id = ch.Id;
                if (ch.Id == current && !App.Flow.GameComplete)
                    UiKit.Button("Hub:Continue:" + id, row, "ui.hub.continue", () => Screens.Go(Screens.Gameplay), UiKit.Accent).Size(300);
                else if (!complete)
                    UiKit.Button("Hub:Play:" + id, row, "ui.hub.play", () => { App.Flow.StartChapter(id); Screens.Go(Screens.Gameplay); }, UiKit.Accent).Size(300);
                if (App.Flow.CanReplay(id))
                    UiKit.Button("Hub:Replay:" + id, row, "ui.hub.replay", () =>
                    {
                        if (App.Flow.StartReplay(id)) Screens.Go(Screens.Gameplay);
                    }).Size(300);
            }
        }
    }
}
