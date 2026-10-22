using Bonkadoo.Application;
using NUnit.Framework;

namespace Bonkadoo.Tests.Application
{
    public class GestureRecognizerTests
    {
        [Test]
        public void SmallMovementIsTap()
        {
            var g = new GestureRecognizer(10f);
            g.Press(100, 100, 326);
            g.Move(105, 102);
            Assert.AreEqual(GestureKind.Tap, g.Release(106, 103).Kind);
        }

        [Test]
        public void MovementBeyondThresholdIsDrag()
        {
            var g = new GestureRecognizer(10f);
            g.Press(100, 100, 160);
            g.Move(140, 100); // 40px at 160dpi = 18pt
            Assert.IsTrue(g.IsDragging);
            var gesture = g.Release(200, 100);
            Assert.AreEqual(GestureKind.Drag, gesture.Kind);
            Assert.AreEqual(100, gesture.StartX);
            Assert.AreEqual(200, gesture.EndX);
        }

        [Test]
        public void ThresholdScalesWithDpi()
        {
            // same 40px: drag on a 160dpi screen, tap on a 460dpi phone
            var low = new GestureRecognizer(10f);
            low.Press(0, 0, 160);
            Assert.AreEqual(GestureKind.Drag, low.Release(40, 0).Kind);
            var high = new GestureRecognizer(10f);
            high.Press(0, 0, 460);
            Assert.AreEqual(GestureKind.Tap, high.Release(40, 0).Kind);
        }

        [Test]
        public void DragOutAndBackStaysDrag()
        {
            var g = new GestureRecognizer(10f);
            g.Press(0, 0, 160);
            g.Move(100, 0);
            Assert.AreEqual(GestureKind.Drag, g.Release(0, 0).Kind);
        }

        [Test]
        public void ReleaseWithoutPressOrAfterCancelIsNone()
        {
            var g = new GestureRecognizer();
            Assert.AreEqual(GestureKind.None, g.Release(0, 0).Kind);
            g.Press(0, 0, 160);
            g.Cancel();
            Assert.AreEqual(GestureKind.None, g.Release(0, 0).Kind);
        }

        [Test]
        public void MinTouchTargetIs44Points()
        {
            Assert.AreEqual(44f / 72f * 326f, GestureRecognizer.MinTouchTargetPixels(326f), 0.001f);
            Assert.AreEqual(44f / 72f * 160f, GestureRecognizer.MinTouchTargetPixels(0f), 0.001f, "unknown dpi falls back");
        }
    }
}
