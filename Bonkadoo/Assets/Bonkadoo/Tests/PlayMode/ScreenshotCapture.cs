using System.Collections;
using System.IO;
using System.Linq;
using Bonkadoo.Infrastructure.Unity;
using Bonkadoo.Presentation;
using Bonkadoo.Presentation.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Bonkadoo.Tests.PlayMode
{
    /// <summary>Visual review aid, not a pass/fail test: set BONKADOO_SCREENSHOTS=dir to write PNGs.</summary>
    public class ScreenshotCapture
    {
        [UnityTest]
        public IEnumerator CaptureKeyScreens()
        {
            var dir = System.Environment.GetEnvironmentVariable("BONKADOO_SCREENSHOTS");
            if (string.IsNullOrEmpty(dir)) { Assert.Ignore("set BONKADOO_SCREENSHOTS to capture"); yield break; }
            Directory.CreateDirectory(dir);
            UnityPaths.OverrideRoot = Path.Combine(Path.GetTempPath(), "bonkadoo-shots-" + System.Guid.NewGuid().ToString("N"));
            foreach (var b in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            AppServices.Reset();
            yield return null;
            var lang = System.Environment.GetEnvironmentVariable("BONKADOO_LANG") ?? "en";
            SceneManager.LoadScene(Nav.Bootstrap);
            for (var i = 0; i < 120 && Object.FindAnyObjectByType<MenuScreen>() == null; i++) yield return null;
            AppServices.Current.SetLanguage(lang);
            yield return Shot(dir, $"01_menu_{lang}");
            AppServices.Current.Flow.NewGame();
            SceneManager.LoadScene(Nav.Gameplay);
            for (var i = 0; i < 60; i++) yield return null;
            yield return Shot(dir, $"02_intro_dialogue_{lang}");
            var g = Object.FindAnyObjectByType<GameplayScreen>();
            for (var i = 0; i < 40 && g.Dialogue.IsBusy; i++) { g.Dialogue.Tap(); g.Dialogue.Tap(); yield return null; }
            yield return Shot(dir, $"03_office_{lang}");
            AppServices.Current.Flow.Controller.Tap(g.Stage.Find("prop.ch01.desk_stamp").Info);
            for (var i = 0; i < 10; i++) yield return null;
            for (var i = 0; i < 40 && g.Dialogue.IsBusy; i++) { g.Dialogue.Tap(); g.Dialogue.Tap(); yield return null; }
            AppServices.Current.Flow.Controller.ToggleItem("item.ch01.stamp");
            g.SendMessage("RefreshInventory", SendMessageOptions.DontRequireReceiver);
            for (var i = 0; i < 10; i++) yield return null;
            yield return Shot(dir, $"04_item_selected_{lang}");
            UnityPaths.OverrideRoot = null;
        }

        private static IEnumerator Shot(string dir, string name)
        {
            yield return null;
            // batch mode has no end-of-frame; render canvases through a camera into a texture instead
            var rt = new RenderTexture(2340, 1080, 24);
            var camGo = new GameObject("ShotCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture = rt;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas).ToArray();
            foreach (var c in canvases)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1 + c.sortingOrder * 0.01f;
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Debug.Log($"[Bonkadoo] screenshot {name} device={SystemInfo.graphicsDeviceType}");
            foreach (var c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.Destroy(camGo);
            Object.Destroy(tex);
            rt.Release();
        }
    }
}
