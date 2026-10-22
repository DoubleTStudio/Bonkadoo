using System;

namespace Bonkadoo.Application
{
    public enum GestureKind { None, Tap, Drag }

    public readonly struct Gesture
    {
        public GestureKind Kind { get; }
        public float StartX { get; }
        public float StartY { get; }
        public float EndX { get; }
        public float EndY { get; }

        public Gesture(GestureKind kind, float sx, float sy, float ex, float ey)
        {
            Kind = kind;
            StartX = sx;
            StartY = sy;
            EndX = ex;
            EndY = ey;
        }
    }

    /// <summary>
    /// Classifies one press/release in screen space. Movement is measured in physical points
    /// (pixels / dpi * 72) so the threshold feels the same on every device.
    /// </summary>
    public sealed class GestureRecognizer
    {
        public const float DefaultDpi = 160f;
        private readonly float _dragThresholdPoints;
        private bool _down;
        private bool _dragging;
        private float _sx, _sy, _lastX, _lastY, _dpi;

        public bool IsPressed => _down;
        public bool IsDragging => _dragging;
        public float LastX => _lastX;
        public float LastY => _lastY;

        public GestureRecognizer(float dragThresholdPoints = 10f)
        {
            if (dragThresholdPoints <= 0) throw new ArgumentOutOfRangeException(nameof(dragThresholdPoints));
            _dragThresholdPoints = dragThresholdPoints;
        }

        public void Press(float x, float y, float dpi)
        {
            _down = true;
            _dragging = false;
            _sx = _lastX = x;
            _sy = _lastY = y;
            _dpi = dpi > 0 ? dpi : DefaultDpi;
        }

        public void Move(float x, float y)
        {
            if (!_down) return;
            _lastX = x;
            _lastY = y;
            if (!_dragging && DistancePoints(x, y) >= _dragThresholdPoints) _dragging = true;
        }

        public Gesture Release(float x, float y)
        {
            if (!_down) return new Gesture(GestureKind.None, x, y, x, y);
            Move(x, y);
            _down = false;
            return new Gesture(_dragging ? GestureKind.Drag : GestureKind.Tap, _sx, _sy, x, y);
        }

        public void Cancel()
        {
            _down = false;
            _dragging = false;
        }

        private float DistancePoints(float x, float y)
        {
            var dx = x - _sx;
            var dy = y - _sy;
            return (float)Math.Sqrt(dx * dx + dy * dy) / _dpi * 72f;
        }

        /// <summary>44pt minimum touch target (GDD) converted to pixels.</summary>
        public static float MinTouchTargetPixels(float dpi) => 44f / 72f * (dpi > 0 ? dpi : DefaultDpi);
    }
}
