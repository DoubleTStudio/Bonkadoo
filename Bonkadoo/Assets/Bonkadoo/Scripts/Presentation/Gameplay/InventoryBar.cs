using System;
using System.Linq;
using Bonkadoo.Domain;
using Bonkadoo.Presentation.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Gameplay
{
    public sealed class ItemSlot : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        public string ItemId { get; private set; }
        public Action<ItemSlot> Tapped;
        public Action<ItemSlot> DragStarted;
        public Action<ItemSlot, string> ItemDropped;
        private RectTransform _ghost;
        private Canvas _canvas;

        public void Init(string itemId, Canvas canvas)
        {
            ItemId = itemId;
            _canvas = canvas;
            GetComponent<Button>().onClick.AddListener(() => Tapped?.Invoke(this));
        }

        public void OnBeginDrag(PointerEventData e)
        {
            DragStarted?.Invoke(this);
            var src = transform.Find("Swatch").GetComponent<Image>();
            var ghost = UiKit.Image("DragGhost", _canvas.transform, src.color);
            ghost.sprite = Sprites.Rounded;
            ghost.type = Image.Type.Sliced;
            ghost.rectTransform.sizeDelta = new Vector2(110, 110);
            ghost.raycastTarget = false; // lets drop targets under the finger receive the drop
            _ghost = ghost.rectTransform;
            OnDrag(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (_ghost == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_canvas.transform, e.position, null, out var local);
            _ghost.anchoredPosition = local;
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (_ghost != null) Destroy(_ghost.gameObject);
            _ghost = null;
        }

        public void OnDrop(PointerEventData e)
        {
            var other = e.pointerDrag != null ? e.pointerDrag.GetComponent<ItemSlot>() : null;
            if (other != null && other != this) ItemDropped?.Invoke(this, other.ItemId);
        }
    }

    /// <summary>Compact bottom inventory (UX_LOCALIZATION). Shows the selected item's description as the usage hint.</summary>
    public sealed class InventoryBar : MonoBehaviour
    {
        private RectTransform _slots;
        private LocalizedText _info;
        private Canvas _canvas;
        private SceneCatalog _catalog;

        public event Action<string> ItemTapped;
        public event Action<string> DragStarted;
        public event Action<string, string> ItemOnItem;

        public static InventoryBar Create(RectTransform area, Canvas canvas, SceneCatalog catalog)
        {
            var panel = UiKit.Image("Inventory", area, new Color(0.13f, 0.13f, 0.23f, 0.85f));
            panel.rectTransform.Stretch();
            var bar = panel.gameObject.AddComponent<InventoryBar>();
            bar._canvas = canvas;
            bar._catalog = catalog;
            bar._info = UiKit.Label("Info", panel.transform, "ui.game.inventory_empty", 28, Color.white, TextAnchor.MiddleLeft);
            bar._info.GetComponent<RectTransform>().Anchor(new Vector2(0, 0.68f), Vector2.one, new Vector2(24, 0), new Vector2(-24, 0));
            var scroll = UiKit.Rect("Scroll", panel.transform).Anchor(Vector2.zero, new Vector2(1, 0.68f), new Vector2(16, 8), new Vector2(-16, 0));
            scroll.gameObject.AddComponent<RectMask2D>();
            bar._slots = UiKit.Rect("Slots", scroll).Anchor(Vector2.zero, new Vector2(0, 1));
            bar._slots.pivot = new Vector2(0, 0.5f);
            var row = UiKit.Row(bar._slots, 14);
            row.childForceExpandHeight = true;
            row.childAlignment = TextAnchor.MiddleLeft;
            var fit = bar._slots.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scroll.gameObject.AddComponent<ScrollRect>();
            sr.content = bar._slots;
            sr.vertical = false;
            return bar;
        }

        public void Refresh(IReadOnlyWorldState state, System.Collections.Generic.IReadOnlyCollection<string> items, string selected)
        {
            UiKit.Clear(_slots);
            foreach (var id in items.OrderBy(i => i, StringComparer.Ordinal))
            {
                var def = _catalog.Item(id);
                var slot = UiKit.Image("Item:" + id, _slots, id == selected ? UiKit.Accent : new Color(1, 1, 1, 0.12f), raycast: true);
                slot.sprite = Sprites.Rounded;
                slot.type = Image.Type.Sliced;
                slot.Size(260, 96);
                slot.gameObject.AddComponent<Button>();
                var swatch = UiKit.Image("Swatch", slot.transform, UiKit.Hex(def?.Color, UiKit.Hex("#d9b26f")));
                swatch.sprite = Sprites.Rounded;
                swatch.type = Image.Type.Sliced;
                swatch.rectTransform.Anchor(new Vector2(0, 0), new Vector2(0, 1), new Vector2(8, 8), new Vector2(80, -8));
                var name = UiKit.Label("Name", slot.transform, def?.LabelKey ?? id, 22, Color.white, TextAnchor.MiddleLeft);
                name.GetComponent<RectTransform>().Stretch(88, 4, 8, 4);
                var view = slot.gameObject.AddComponent<ItemSlot>();
                view.Init(id, _canvas);
                view.Tapped = s => ItemTapped?.Invoke(s.ItemId);
                view.DragStarted = s => DragStarted?.Invoke(s.ItemId);
                view.ItemDropped = (target, source) => ItemOnItem?.Invoke(source, target.ItemId);
            }
            if (selected != null && _catalog.Item(selected) != null)
            {
                _info.Key = "ui.game.selected";
                _info.SetArgs(AppServices.Current.T(_catalog.Item(selected).LabelKey) + " (" +
                              AppServices.Current.T(_catalog.Item(selected).DescKey) + ")");
            }
            else if (items.Count == 0) _info.Key = "ui.game.inventory_empty";
            else _info.SetLiteral(string.Empty);
        }
    }
}
