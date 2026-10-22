using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bonkadoo.Infrastructure.Unity;
using Bonkadoo.Presentation;
using Bonkadoo.Presentation.Gameplay;
using Bonkadoo.Presentation.Screens;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Bonkadoo.Tests.PlayMode
{
    /// <summary>End-to-end through the real UI: boot, menu, gameplay, dialogue, inventory, chapter card, resume.</summary>
    public class GameUiPlayModeTests
    {
        private string _saveDir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "bonkadoo-ui-" + System.Guid.NewGuid().ToString("N"));
            UnityPaths.OverrideRoot = _saveDir;
            foreach (var b in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(b.gameObject);
            AppServices.Reset();
            yield return null;
            SceneManager.LoadScene(Nav.Bootstrap);
            yield return WaitFor(() => Object.FindAnyObjectByType<MenuScreen>() != null && Find("Menu:New") != null, "menu");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityPaths.OverrideRoot = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
            yield return null;
        }

        private static GameObject Find(string name) =>
            Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None).Select(r => r.gameObject)
                .FirstOrDefault(g => g.name == name && g.activeInHierarchy);

        private static IEnumerator WaitFor(System.Func<bool> condition, string what, int frames = 600)
        {
            for (var i = 0; i < frames && !condition(); i++) yield return null;
            Assert.IsTrue(condition(), "timed out waiting for " + what);
        }

        private static IEnumerator Click(string name)
        {
            yield return WaitFor(() => Find(name) != null, name);
            var button = Find(name).GetComponent<Button>();
            Assert.IsTrue(button.interactable, name + " not interactable");
            button.onClick.Invoke();
            yield return null;
        }

        private static GameplayScreen Gameplay => Object.FindAnyObjectByType<GameplayScreen>();

        /// <summary>Taps through feedback lines and conversations, picking the first option at choices.</summary>
        private static IEnumerator DrainDialogue()
        {
            for (var guard = 0; guard < 400; guard++)
            {
                var g = Gameplay;
                if (g == null || g.Dialogue == null || !g.Dialogue.IsBusy) yield break;
                var option = Find("Option:0");
                if (option != null) option.GetComponent<Button>().onClick.Invoke();
                else g.Dialogue.Tap();
                yield return null;
            }
            Assert.Fail("dialogue never finished");
        }

        private static List<(string verb, string target, string source)> Route(string puzzleId, string route)
        {
            var json = JArray.Parse(Resources.Load<TextAsset>("BonkadooContent/puzzles/puzzles").text);
            var p = json.First(x => (string)x["id"] == puzzleId);
            return p["solutions"].First(s => ((string)s["id"]).EndsWith("." + route))["steps"]
                .Select(s => ((string)s["verb"], (string)s["targetId"], (string)s["sourceId"])).ToList();
        }

        private static IEnumerator WalkTo(string scene)
        {
            var app = AppServices.Current;
            for (var guard = 0; guard < 6 && app.Flow.Session.State.CurrentSceneId != scene; guard++)
            {
                var exit = FirstExitToward(app.Flow.Session.State.CurrentSceneId, scene);
                Assert.IsNotNull(exit, "no open path to " + scene);
                yield return Click("Hotspot:" + exit);
                yield return DrainDialogue();
            }
            Assert.AreEqual(scene, app.Flow.Session.State.CurrentSceneId);
        }

        private static string FirstExitToward(string from, string to)
        {
            var app = AppServices.Current;
            var queue = new Queue<(string scene, string first)>();
            var seen = new HashSet<string> { from };
            queue.Enqueue((from, null));
            while (queue.Count > 0)
            {
                var (scene, first) = queue.Dequeue();
                foreach (var h in app.Content.Scenes.Scene(scene).Hotspots)
                {
                    if (!h.IsExitOpen(app.Flow.Session.State) || !seen.Add(h.ExitTo)) continue;
                    var firstExit = first ?? h.Id;
                    if (h.ExitTo == to) return firstExit;
                    queue.Enqueue((h.ExitTo, firstExit));
                }
            }
            return null;
        }

        private static IEnumerator PlayPuzzle(string pid, string route)
        {
            AppServices.Current.Content.Rules.TryGetPuzzle(pid, out var puzzle);
            yield return WalkTo(puzzle.SceneId);
            foreach (var (verb, target, source) in Route(pid, route))
            {
                if (source != null) yield return Click("Item:" + source);
                yield return Click("Hotspot:" + target);
                yield return DrainDialogue();
            }
            Assert.IsTrue(AppServices.Current.Flow.Session.State.HasFlag(puzzle.CompletionFlag), pid + " not solved via UI");
        }

        [UnityTest]
        public IEnumerator NewGamePlaysChapterOneThroughTheUiToTheChapterCard()
        {
            yield return Click("Menu:New");
            yield return WaitFor(() => Gameplay != null && Gameplay.Stage != null, "gameplay");
            Assert.IsTrue(Gameplay.Dialogue.IsBusy, "intro conversation should play on arrival");
            yield return DrainDialogue();
            var routes = new[] { "a", "b", "a", "b", "a", "b", "a", "b", "a" };
            for (var i = 0; i < 9; i++) yield return PlayPuzzle($"ch01.p0{i + 1}", routes[i]);
            yield return DrainDialogue();
            yield return WaitFor(() => Find("Chapter:Next") != null, "chapter complete card");
            yield return Click("Chapter:Next");
            yield return WaitFor(() => AppServices.Current.Flow.CurrentChapter.Id == "ch02", "chapter 2 start");
            Assert.IsTrue(AppServices.Current.Flow.IsChapterComplete("ch01"));
        }

        [UnityTest]
        public IEnumerator LanguageSwitchRelabelsWithoutRestart()
        {
            yield return Click("Menu:New");
            yield return WaitFor(() => Gameplay != null && Gameplay.Stage != null, "gameplay");
            yield return DrainDialogue();
            var label = Find("Hotspot:prop.ch01.desk_stamp").transform.Find("Name").GetComponent<Text>();
            AppServices.Current.SetLanguage("en");
            yield return null;
            Assert.AreEqual("Stamp tray", label.text);
            yield return Click("Game:Menu");
            yield return Click("Pause:Settings");
            yield return Click("Settings:Lang:vi");
            Assert.AreEqual("Khay con dấu", label.text);
            Assert.AreEqual("vi", AppServices.Current.Settings.Current.Language);
            Assert.IsTrue(UiKitFontCovers("Khay con dấu ượ ố Đ"), "font lacks Vietnamese glyphs");
        }

        private static bool UiKitFontCovers(string text) => text.All(c => char.IsWhiteSpace(c) || Presentation.Ui.UiKit.Font.HasCharacter(c));

        [UnityTest]
        public IEnumerator QuitAndContinueResumesExactly()
        {
            yield return Click("Menu:New");
            yield return WaitFor(() => Gameplay != null && Gameplay.Stage != null, "gameplay");
            yield return DrainDialogue();
            yield return PlayPuzzle("ch01.p01", "a");
            yield return Click("Game:Menu");
            yield return Click("Pause:Quit");
            yield return WaitFor(() => Find("Menu:Continue") != null, "menu with continue");
            var flow = AppServices.Current.Flow;
            yield return Click("Menu:Continue");
            yield return WaitFor(() => Gameplay != null && Gameplay.Stage != null, "gameplay after continue");
            Assert.IsTrue(flow.Session.State.HasItem("item.ch01.dismissal_notice"));
            Assert.IsNotNull(Find("Item:item.ch01.dismissal_notice"), "inventory shows restored item");
            Assert.AreEqual("ch01.p02", flow.Hints.ActivePuzzle.Id);
        }

        [UnityTest]
        public IEnumerator DraggingAnItemOntoAHotspotEqualsTapSelect()
        {
            yield return Click("Menu:New");
            yield return WaitFor(() => Gameplay != null && Gameplay.Stage != null, "gameplay");
            yield return DrainDialogue();
            yield return Click("Hotspot:prop.ch01.desk_stamp");
            yield return DrainDialogue();
            var slot = Find("Item:item.ch01.stamp").GetComponent<ItemSlot>();
            var target = Find("Hotspot:prop.ch01.notice").GetComponent<HotspotView>();
            var ev = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { pointerDrag = slot.gameObject };
            target.OnDrop(ev);
            yield return DrainDialogue();
            Assert.IsTrue(AppServices.Current.Flow.Session.State.HasFlag("flag.ch01.p01.complete"));
        }
    }
}
