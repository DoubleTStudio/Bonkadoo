using Bonkadoo.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Screens
{
    /// <summary>Builds a safe-area canvas; redirects to Bootstrap if the app was not composed (e.g. scene opened directly).</summary>
    public abstract class ScreenBase : MonoBehaviour
    {
        protected AppServices App => AppServices.Current;
        protected RectTransform Root { get; private set; }
        protected Canvas Canvas { get; private set; }

        protected virtual void Start()
        {
            if (App == null)
            {
                Nav.Go(Nav.Bootstrap);
                return;
            }
            Canvas = UiKit.Canvas(GetType().Name + "Canvas");
            Canvas.transform.SetParent(transform, false);
            UiKit.Image("Background", Canvas.transform, BackgroundColor).rectTransform.Stretch();
            Root = UiKit.Rect("SafeArea", Canvas.transform);
            Root.gameObject.AddComponent<SafeAreaFitter>();
            Build();
        }

        protected virtual Color BackgroundColor => UiKit.Paper;
        protected abstract void Build();

        protected RectTransform Overlay(string name, float dim = 0.6f)
        {
            var blocker = UiKit.Image(name, Canvas.transform, new Color(0, 0, 0, dim), raycast: true);
            blocker.rectTransform.Stretch();
            var safe = UiKit.Rect("Safe", blocker.transform);
            safe.gameObject.AddComponent<SafeAreaFitter>();
            return safe;
        }

        protected RectTransform Card(RectTransform parent, string name, Vector2 size)
        {
            var card = UiKit.Image(name, parent, UiKit.Panel, raycast: true);
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            return rt;
        }

        public void Confirm(string messageKey, System.Action yes)
        {
            var safe = Overlay("Confirm");
            var card = Card(safe, "ConfirmCard", new Vector2(1000, 460));
            UiKit.Column(card, 30, 40);
            UiKit.Label("Message", card, messageKey, 44, Color.white).Size(-1, 220);
            var row = UiKit.Rect("Buttons", card);
            UiKit.Row(row, 30).Size(-1, 110);
            var root = safe.parent.gameObject;
            UiKit.Button("Confirm:Yes", row, "ui.yes", () => { Destroy(root); yes(); }, UiKit.Accent).Size(320);
            UiKit.Button("Confirm:No", row, "ui.no", () => Destroy(root)).Size(320);
        }

        public void Toast(string key, params object[] args)
        {
            var img = UiKit.Image("Toast", Canvas.transform, UiKit.Panel);
            img.rectTransform.Anchor(new Vector2(0.25f, 0.82f), new Vector2(0.75f, 0.92f));
            var label = UiKit.Label("Text", img.transform, key, 36, Color.white);
            label.GetComponent<RectTransform>().Stretch(20, 4, 20, 4);
            if (args.Length > 0) label.SetArgs(args);
            Destroy(img.gameObject, 2.5f);
        }
    }
}
