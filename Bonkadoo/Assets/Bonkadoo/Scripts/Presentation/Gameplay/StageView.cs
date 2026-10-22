using System;
using System.Collections.Generic;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Presentation.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Gameplay
{
    /// <summary>One tappable thing. Receives inventory drops (tap-select and drag produce the same request).</summary>
    public sealed class HotspotView : MonoBehaviour, IDropHandler
    {
        public HotspotDefinition Definition { get; private set; }
        public Action<HotspotView> Tapped;
        public Action<HotspotView, string> ItemDropped;
        private LocalizedText _lockLabel;
        private Image _body;

        public HotspotInfo Info
        {
            get
            {
                var d = Definition;
                var kind = d.Type == HotspotType.Npc ? HotspotKind.Npc : d.Type == HotspotType.Exit ? HotspotKind.Exit : HotspotKind.Prop;
                return new HotspotInfo(d.Id, kind, exitTo: d.ExitTo, startUnlocked: d.StartUnlocked);
            }
        }

        public void Init(HotspotDefinition def, Image body, LocalizedText lockLabel)
        {
            Definition = def;
            _body = body;
            _lockLabel = lockLabel;
            GetComponent<Button>().onClick.AddListener(() => Tapped?.Invoke(this));
        }

        public void Refresh(IReadOnlyWorldState state)
        {
            gameObject.SetActive(Definition.IsVisible(state));
            if (Definition.Type == HotspotType.Exit && _lockLabel != null)
            {
                var open = Definition.IsExitOpen(state);
                _lockLabel.gameObject.SetActive(!open);
                _body.color = open ? UiKit.Hex(Definition.Color, UiKit.Hex("#7cb518")) : UiKit.Muted;
            }
        }

        public void OnDrop(PointerEventData eventData)
        {
            var slot = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<ItemSlot>() : null;
            if (slot != null) ItemDropped?.Invoke(this, slot.ItemId);
        }
    }

    /// <summary>Lays out a scene's hotspots on a fixed-aspect stage so 16:9 and 19.5:9 screens show the same composition.</summary>
    public sealed class StageView : MonoBehaviour
    {
        public const float Aspect = 2.3f;
        private readonly List<HotspotView> _views = new List<HotspotView>();
        private RectTransform _stage;
        private Image _background;

        public IReadOnlyList<HotspotView> Views => _views;
        public event Action<HotspotView> HotspotTapped;
        public event Action<HotspotView, string> ItemDropped;

        public static StageView Create(RectTransform area)
        {
            var holder = UiKit.Rect("StageArea", area).Stretch();
            var stage = UiKit.Image("Stage", holder, Color.white);
            var fitter = stage.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = Aspect;
            var view = stage.gameObject.AddComponent<StageView>();
            view._stage = stage.rectTransform;
            view._background = stage;
            return view;
        }

        public void Show(SceneDefinition scene, IReadOnlyWorldState state)
        {
            UiKit.Clear(_stage);
            _views.Clear();
            _background.color = UiKit.Hex(scene.Background, UiKit.Paper);
            // floor band gives depth without detail behind hotspots (ART_BIBLE restraint)
            var floor = UiKit.Image("Floor", _stage, new Color(0, 0, 0, 0.08f));
            floor.rectTransform.Anchor(Vector2.zero, new Vector2(1, 0.22f));
            foreach (var def in scene.Hotspots) _views.Add(Build(def));
            Refresh(state);
        }

        public void Refresh(IReadOnlyWorldState state)
        {
            foreach (var v in _views) v.Refresh(state);
        }

        public HotspotView Find(string id) => _views.Find(v => v.Definition.Id == id);

        private HotspotView Build(HotspotDefinition def)
        {
            var root = UiKit.Image("Hotspot:" + def.Id, _stage, new Color(1, 1, 1, 0.001f), raycast: true);
            root.rectTransform.Anchor(new Vector2(def.X - def.W / 2, def.Y - def.H / 2), new Vector2(def.X + def.W / 2, def.Y + def.H / 2));
            root.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            var view = root.gameObject.AddComponent<HotspotView>();

            Image body;
            LocalizedText lockLabel = null;
            if (def.Type == HotspotType.Npc) body = BuildCharacter(root.rectTransform, def.Character);
            else
            {
                body = UiKit.Image("Body", root.transform, def.Type == HotspotType.Exit ? UiKit.Hex(def.Color, UiKit.Hex("#7cb518"))
                    : UiKit.Hex(def.Color, Sprites.PropColor(def.Id)));
                body.sprite = Sprites.Rounded;
                body.type = Image.Type.Sliced;
                body.rectTransform.Stretch(0, 24, 0, 0);
                var outline = body.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.13f, 0.13f, 0.23f, 0.6f);
                outline.effectDistance = new Vector2(3, -3);
                if (def.Type == HotspotType.Exit)
                {
                    lockLabel = UiKit.Label("Locked", body.transform, "ui.game.exit_locked", 26, Color.white);
                    lockLabel.GetComponent<RectTransform>().Stretch(4, 4, 4, 4);
                }
            }
            var label = UiKit.Label("Name", root.transform, def.LabelKey, 24, UiKit.Ink);
            label.GetComponent<RectTransform>().Anchor(new Vector2(-0.4f, 0), new Vector2(1.4f, 0), new Vector2(0, -6), new Vector2(0, 34));
            var shadow = label.gameObject.AddComponent<Outline>();
            shadow.effectColor = new Color(1, 1, 1, 0.8f);

            view.Init(def, body, lockLabel);
            view.Tapped = v => HotspotTapped?.Invoke(v);
            view.ItemDropped = (v, item) => ItemDropped?.Invoke(v, item);
            return view;
        }

        private static Image BuildCharacter(RectTransform root, string character)
        {
            var color = UiKit.Hex(character != null && Sprites.CharacterColors.TryGetValue(character, out var hex) ? hex : null, UiKit.Calm);
            if (character == "bop")
            {
                var cube = UiKit.Image("Cube", root, color);
                cube.sprite = Sprites.Rounded;
                cube.type = Image.Type.Sliced;
                cube.rectTransform.Anchor(new Vector2(0.1f, 0.3f), new Vector2(0.9f, 0.95f));
                for (var i = 0; i < 2; i++)
                    UiKit.Image("Eye" + i, cube.transform, UiKit.Ink).rectTransform
                        .Anchor(new Vector2(0.25f + i * 0.35f, 0.5f), new Vector2(0.4f + i * 0.35f, 0.7f));
                return cube;
            }
            var bodyImg = UiKit.Image("Body", root, color);
            bodyImg.sprite = Sprites.Rounded;
            bodyImg.type = Image.Type.Sliced;
            bodyImg.rectTransform.Anchor(new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.66f));
            var head = UiKit.Image("Head", root, UiKit.Hex("#f1d4b3"));
            head.sprite = Sprites.Circle;
            head.preserveAspect = true;
            head.rectTransform.Anchor(new Vector2(0.2f, 0.62f), new Vector2(0.8f, 0.98f));
            return bodyImg;
        }
    }
}
