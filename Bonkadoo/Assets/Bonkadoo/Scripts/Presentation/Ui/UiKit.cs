using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Ui
{
    /// <summary>Code-built uGUI. All visible text goes through <see cref="LocalizedText"/>; no strings live here.</summary>
    public static class UiKit
    {
        public static readonly Vector2 Reference = new Vector2(1920, 1080);
        public static readonly Color Ink = Hex("#22223b");
        public static readonly Color Paper = Hex("#f7f3e8");
        public static readonly Color Accent = Hex("#e07a5f");
        public static readonly Color Calm = Hex("#3d5a80");
        public static readonly Color Muted = Hex("#9a9aa8");
        public static readonly Color Panel = new Color(0.13f, 0.13f, 0.23f, 0.92f);

        private static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        public static Color Hex(string hex, Color? fallback = null)
        {
            return !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c) ? c : fallback ?? Muted;
        }

        public static Canvas Canvas(string name, int order = 0)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Reference;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            // drag threshold in points, so it feels the same on every screen density
            es.GetComponent<EventSystem>().pixelDragThreshold =
                Mathf.RoundToInt(10f / 72f * (Screen.dpi > 0 ? Screen.dpi : Bonkadoo.Application.GestureRecognizer.DefaultDpi));
            UnityEngine.Object.DontDestroyOnLoad(es);
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Anchor(this RectTransform rt, Vector2 min, Vector2 max, Vector2? offsetMin = null, Vector2? offsetMax = null)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = offsetMin ?? Vector2.zero;
            rt.offsetMax = offsetMax ?? Vector2.zero;
            return rt;
        }

        public static Image Image(string name, Transform parent, Color color, bool raycast = false)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        public static LocalizedText Label(string name, Transform parent, string key, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            var rt = Rect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = Font;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(10, size / 2);
            text.resizeTextMaxSize = size;
            var loc = rt.gameObject.AddComponent<LocalizedText>();
            loc.Init(text, size);
            loc.Key = key;
            return loc;
        }

        public static Button Button(string name, Transform parent, string key, Action onClick, Color? bg = null, int size = 40)
        {
            var img = Image(name, parent, bg ?? Calm, raycast: true);
            var button = img.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1, 1, 1, 0.95f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            Label("Label", img.transform, key, size, Color.white).GetComponent<RectTransform>().Stretch(12, 6, 12, 6);
            var min = img.gameObject.AddComponent<LayoutElement>();
            min.minHeight = 88; // 44pt at the 2x reference scale
            min.minWidth = 88;
            return button;
        }

        public static VerticalLayoutGroup Column(RectTransform rt, float spacing = 16, int padding = 0)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padding, padding, padding, padding);
            v.childAlignment = TextAnchor.MiddleCenter;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return v;
        }

        public static HorizontalLayoutGroup Row(RectTransform rt, float spacing = 16, int padding = 0)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padding, padding, padding, padding);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = false;
            return h;
        }

        public static LayoutElement Size(this Component c, float preferredWidth = -1, float preferredHeight = -1, float flexibleWidth = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (preferredWidth >= 0) le.preferredWidth = preferredWidth;
            if (preferredHeight >= 0) le.preferredHeight = preferredHeight;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        public static void SetLabel(this Button b, string key) => b.GetComponentInChildren<LocalizedText>().Key = key;

        public static void Clear(Transform t)
        {
            for (var i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
