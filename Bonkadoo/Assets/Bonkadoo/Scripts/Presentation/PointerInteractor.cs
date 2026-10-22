using Bonkadoo.Application;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Bonkadoo.Presentation
{
    public interface IHotspotSink
    {
        void Tap(HotspotInfo hotspot);
        void DragItemOnto(string itemId, HotspotInfo hotspot);
    }

    /// <summary>Adapts <see cref="InteractionController"/> to the sink the pointer drives.</summary>
    public sealed class ControllerSink : IHotspotSink
    {
        private readonly InteractionController _controller;
        public ControllerSink(InteractionController controller) => _controller = controller;
        public void Tap(HotspotInfo hotspot) => _controller.Tap(hotspot);
        public void DragItemOnto(string itemId, HotspotInfo hotspot) => _controller.DragItemOnto(itemId, hotspot);
    }

    /// <summary>
    /// Reads the current pointer (touch or mouse) and turns gestures into hotspot taps or
    /// item drags. Inventory slots call <see cref="BeginItemDrag"/> when a drag starts on them.
    /// </summary>
    public sealed class PointerInteractor : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private float dragThresholdPoints = 10f;

        private GestureRecognizer _gesture;
        private string _dragItemId;

        public IHotspotSink Sink { get; set; }

        private void Awake() => _gesture = new GestureRecognizer(dragThresholdPoints);

        private void Start()
        {
            if (Sink == null && GameServices.Current?.Controller != null)
                Sink = new ControllerSink(GameServices.Current.Controller);
        }

        public void SetCamera(Camera cam) => worldCamera = cam;

        public void BeginItemDrag(string itemId) => _dragItemId = itemId;

        private void Update()
        {
            var pointer = Pointer.current;
            if (pointer == null || Sink == null) return;

            var pos = pointer.position.ReadValue();
            var press = pointer.press;
            if (press.wasPressedThisFrame) _gesture.Press(pos.x, pos.y, Screen.dpi);
            else if (press.isPressed) _gesture.Move(pos.x, pos.y);

            if (!press.wasReleasedThisFrame) return;
            var gesture = _gesture.Release(pos.x, pos.y);
            var item = _dragItemId;
            _dragItemId = null;

            var hotspot = HotspotAt(pos);
            if (hotspot == null) return;
            if (gesture.Kind == GestureKind.Drag && item != null) Sink.DragItemOnto(item, hotspot.Info);
            else if (gesture.Kind == GestureKind.Tap) Sink.Tap(hotspot.Info);
        }

        public Hotspot HotspotAt(Vector2 screenPos)
        {
            var cam = worldCamera != null ? worldCamera : Camera.main;
            if (cam == null) return null;
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
            var hit = Physics2D.OverlapPoint(world);
            return hit != null ? hit.GetComponent<Hotspot>() : null;
        }
    }
}
