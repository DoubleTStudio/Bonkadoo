using System.Collections;
using System.Collections.Generic;
using Bonkadoo.Application;
using Bonkadoo.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Bonkadoo.Tests.PlayMode
{
    public class PointerInteractorPlayModeTests : InputTestFixture
    {
        private sealed class RecordingSink : IHotspotSink
        {
            public readonly List<string> Calls = new List<string>();
            public void Tap(HotspotInfo h) => Calls.Add("tap:" + h.Id);
            public void DragItemOnto(string item, HotspotInfo h) => Calls.Add($"drag:{item}->{h.Id}");
        }

        private Mouse _mouse;
        private Camera _cam;
        private PointerInteractor _pointer;
        private RecordingSink _sink;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        public override void Setup()
        {
            base.Setup();
            _mouse = InputSystem.AddDevice<Mouse>();

            var camGo = Spawn("TestCamera");
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 5;
            camGo.transform.position = new Vector3(0, 0, -10);

            var hs = Spawn("Desk");
            hs.AddComponent<BoxCollider2D>().size = new Vector2(2, 2);
            hs.AddComponent<Hotspot>().Configure("prop.desk", HotspotKind.Prop);

            _sink = new RecordingSink();
            _pointer = Spawn("Pointer").AddComponent<PointerInteractor>();
            _pointer.SetCamera(_cam);
            _pointer.Sink = _sink;
        }

        public override void TearDown()
        {
            foreach (var go in _spawned) Object.Destroy(go);
            _spawned.Clear();
            base.TearDown();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        private Vector2 ScreenOf(Vector3 world) => _cam.WorldToScreenPoint(world);

        private IEnumerator Click(Vector2 at)
        {
            Set(_mouse.position, at);
            yield return null;
            Press(_mouse.leftButton);
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TapOnHotspotColliderSendsTap()
        {
            yield return null;
            Physics2D.SyncTransforms();
            yield return Click(ScreenOf(Vector3.zero));
            CollectionAssert.AreEqual(new[] { "tap:prop.desk" }, _sink.Calls);
        }

        [UnityTest]
        public IEnumerator TapOnEmptySpaceSendsNothing()
        {
            yield return null;
            yield return Click(ScreenOf(new Vector3(4, 4, 0)));
            CollectionAssert.IsEmpty(_sink.Calls);
        }

        [UnityTest]
        public IEnumerator DraggingInventoryItemOntoHotspotSendsUseItem()
        {
            yield return null;
            Physics2D.SyncTransforms();
            var start = ScreenOf(new Vector3(-4, -4, 0));
            Set(_mouse.position, start);
            yield return null;
            Press(_mouse.leftButton);
            _pointer.BeginItemDrag("item.stamp");
            yield return null;
            Set(_mouse.position, ScreenOf(new Vector3(-2, -2, 0)));
            yield return null;
            Set(_mouse.position, ScreenOf(Vector3.zero));
            yield return null;
            Release(_mouse.leftButton);
            yield return null;
            CollectionAssert.AreEqual(new[] { "drag:item.stamp->prop.desk" }, _sink.Calls);
        }
    }
}
