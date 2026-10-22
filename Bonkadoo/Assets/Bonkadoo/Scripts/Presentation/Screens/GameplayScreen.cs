using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Unity;
using Bonkadoo.Presentation.Audio;
using Bonkadoo.Presentation.Gameplay;
using Bonkadoo.Presentation.Ui;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Screens
{
    /// <summary>
    /// Landscape layout: top bar (menu upper-left, hint top-right), fixed-aspect stage (~75%), compact bottom
    /// inventory, dialogue overlay. Presentation only: every decision is made by GameFlow / the rule engine.
    /// </summary>
    public sealed class GameplayScreen : ScreenBase
    {
        private const float TopBar = 0.11f, InventoryHeight = 0.17f, BannerHeight = 0.08f;

        private StageView _stage;
        private InventoryBar _inventory;
        private DialoguePanel _dialogue;
        private RectTransform _stageArea, _banner;
        private LocalizedText _sceneName, _objective, _caption;
        private Button _hint;
        private GameObject _overlay;
        private string _draggingItem;
        private float _captionTimer, _pulse;

        public StageView Stage => _stage;
        public InventoryBar Inventory => _inventory;
        public DialoguePanel Dialogue => _dialogue;
        public bool OverlayOpen => _overlay != null;

        protected override Color BackgroundColor => UiKit.Ink;

        protected override void Build()
        {
            var flow = App.Flow;
            if (flow.Session == null && !flow.Continue())
            {
                Nav.Go(Nav.Menu);
                return;
            }

            var top = UiKit.Rect("TopBar", Root).Anchor(new Vector2(0, 1 - TopBar), Vector2.one);
            UiKit.Button("Game:Menu", top, "ui.game.menu", OpenPause).GetComponent<RectTransform>()
                .Anchor(new Vector2(0, 0.08f), new Vector2(0.11f, 0.92f), new Vector2(12, 0), Vector2.zero);
            _sceneName = UiKit.Label("SceneName", top, null, 36, Color.white, TextAnchor.UpperCenter);
            _sceneName.GetComponent<RectTransform>().Anchor(new Vector2(0.2f, 0.45f), new Vector2(0.8f, 1f));
            _objective = UiKit.Label("Objective", top, null, 26, UiKit.Muted, TextAnchor.LowerCenter);
            _objective.GetComponent<RectTransform>().Anchor(new Vector2(0.2f, 0f), new Vector2(0.8f, 0.5f));
            UiKit.Button("Game:Log", top, "ui.game.log", OpenLog).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.78f, 0.08f), new Vector2(0.88f, 0.92f));
            _hint = UiKit.Button("Game:Hint", top, "ui.game.hint", RevealHint, UiKit.Accent);
            _hint.GetComponent<RectTransform>().Anchor(new Vector2(0.89f, 0.08f), new Vector2(1f, 0.92f), Vector2.zero, new Vector2(-12, 0));

            _banner = UiKit.Image("BannerSlot", Root, new Color(1, 1, 1, 0.08f)).rectTransform;
            _banner.Anchor(new Vector2(0.3f, 1 - TopBar - BannerHeight), new Vector2(0.7f, 1 - TopBar));
            UiKit.Label("BannerLabel", _banner, "ui.ad.banner_test", 22, UiKit.Muted).GetComponent<RectTransform>().Stretch();

            _stageArea = UiKit.Rect("StageHolder", Root);
            _stage = StageView.Create(_stageArea);
            _stage.HotspotTapped += OnHotspotTapped;
            _stage.ItemDropped += (v, item) => Resolve(() => flow.Controller.DragItemOnto(item, v.Info));

            var invArea = UiKit.Rect("InventoryArea", Root).Anchor(Vector2.zero, new Vector2(1, InventoryHeight));
            _inventory = InventoryBar.Create(invArea, Canvas, App.Content.Scenes);
            _inventory.ItemTapped += id =>
            {
                App.Audio?.Play(Cue.Tap);
                flow.Controller.ToggleItem(id);
                RefreshInventory();
            };
            _inventory.DragStarted += id => _draggingItem = id;
            _inventory.ItemOnItem += (source, target) => Resolve(() =>
            {
                flow.Controller.ToggleItem(source);
                return flow.Controller.Combine(target);
            });

            if (flow.IsReplay)
            {
                var replay = UiKit.Image("ReplayBanner", Root, new Color(0.88f, 0.48f, 0.37f, 0.9f));
                replay.rectTransform.Anchor(new Vector2(0.25f, InventoryHeight), new Vector2(0.75f, InventoryHeight + 0.05f));
                UiKit.Label("Text", replay.transform, "ui.replay.banner", 24, Color.white).GetComponent<RectTransform>().Stretch();
            }

            _caption = UiKit.Label("Caption", Root, null, 26, UiKit.Accent, TextAnchor.UpperRight);
            _caption.GetComponent<RectTransform>().Anchor(new Vector2(0.55f, 1 - TopBar - 0.06f), new Vector2(0.99f, 1 - TopBar));
            AudioService.CaptionRaised += OnCaption;

            _dialogue = DialoguePanel.Create(Root);
            _dialogue.Bind(flow.Conversation);
            _dialogue.Idle += OnDialogueIdle;

            flow.OutcomeResolved += OnOutcome;
            flow.SceneChanged += OnSceneChanged;
            flow.SessionStarted += OnSessionStarted;
            App.Ads.BannerChanged += OnBannerChanged;
            SimulatedAdService.InterstitialRequested += ShowTestInterstitial;
            OnBannerChanged(App.Ads.BannerVisible);
            ShowScene();
            if (flow.Conversation != null && flow.Conversation.IsActive) _dialogue.Sync();
        }

        private void OnDestroy()
        {
            AudioService.CaptionRaised -= OnCaption;
            SimulatedAdService.InterstitialRequested -= ShowTestInterstitial;
            if (App == null) return;
            App.Flow.OutcomeResolved -= OnOutcome;
            App.Flow.SceneChanged -= OnSceneChanged;
            App.Flow.SessionStarted -= OnSessionStarted;
            App.Ads.BannerChanged -= OnBannerChanged;
            App.Ads.DialogueActive = false;
            App.Ads.PuzzleActive = false;
        }

        private void OnSessionStarted()
        {
            _dialogue.Bind(App.Flow.Conversation);
            ShowScene();
        }

        private void ShowScene()
        {
            var flow = App.Flow;
            var scene = App.Content.Scenes.Scene(flow.Session.State.CurrentSceneId);
            if (scene == null) return;
            _sceneName.Key = scene.NameKey;
            var chapter = flow.CurrentChapter;
            _objective.Key = chapter?.ObjectiveKey;
            _stage.Show(scene, flow.Session.State);
            RefreshInventory();
            App.Audio?.PlayMusic(scene.Music);
        }

        private void OnBannerChanged(bool visible)
        {
            // the stage shrinks below the banner so ads never cover puzzle objects (ADS_AND_IAP)
            _banner.gameObject.SetActive(visible && Debug.isDebugBuild);
            var topEdge = 1 - TopBar - (visible ? BannerHeight : 0);
            _stageArea.Anchor(new Vector2(0, InventoryHeight), new Vector2(1, topEdge), new Vector2(16, 8), new Vector2(-16, -4));
        }

        private void OnHotspotTapped(HotspotView view)
        {
            if (_dialogue.IsBusy || OverlayOpen) return;
            App.Audio?.Play(Cue.Tap);
            Resolve(() => App.Flow.Controller.Tap(view.Info));
        }

        private void Resolve(System.Func<InteractionOutcome> action)
        {
            if (_dialogue.IsBusy || OverlayOpen) return;
            _draggingItem = null;
            action();
            RefreshInventory();
        }

        private void OnOutcome(InteractionRequest request, InteractionOutcome outcome)
        {
            if (request.Verb == InteractionVerb.Talk && request.TargetId.StartsWith("choice.")) return;
            var gainedItem = outcome.Events.Any(e => e.Type == WorldEventTypes.ItemAdded);
            var solved = outcome.Events.Any(e => e.Type.StartsWith("evt.") && e.Type.Contains(".solved."));
            var unlocked = outcome.Events.Any(e => e.Type == WorldEventTypes.ExitUnlocked);
            if (outcome.Status == OutcomeStatus.Fallback || outcome.Status == OutcomeStatus.Rejected)
            {
                App.Audio?.Play(Cue.Fail);
                var speaker = App.Content.Rules.FallbackSpeaker(outcome.FeedbackKey);
                _dialogue.Enqueue(new[] { new DialogueLine(outcome.FeedbackKey ?? RuleSet.DefaultFallbackKey, speaker) });
            }
            else if (outcome.Status == OutcomeStatus.Committed)
            {
                if (solved)
                {
                    App.Audio?.Play(Cue.Success);
                    Haptic();
                }
                else if (unlocked) App.Audio?.Play(Cue.Unlock);
                else if (gainedItem) App.Audio?.Play(Cue.Pickup);
                if (unlocked && solved) App.Audio?.Play(Cue.Unlock);
                _dialogue.Enqueue(outcome.DialogueLines);
            }
            if (request.Verb != InteractionVerb.Enter) _stage.Refresh(App.Flow.Session.State);
            RefreshInventory();
        }

        private void OnSceneChanged(string from, string to)
        {
            ShowScene();
            // scene change is the natural break; the transition has already happened, ads never block it
            App.Ads.DialogueActive = _dialogue.IsBusy;
            App.Ads.PuzzleActive = false;
            App.Ads.OnNaturalBreak();
        }

        private void OnDialogueIdle()
        {
            var flow = App.Flow;
            var chapter = flow.ConsumeChapterComplete();
            if (chapter != null) ShowChapterComplete(chapter);
        }

        private void RefreshInventory()
        {
            var flow = App.Flow;
            var state = (WorldState)flow.Session.State;
            _inventory.Refresh(state, state.AllItems, flow.Controller.SelectedItemId);
        }

        private void RevealHint()
        {
            if (_dialogue.IsBusy || OverlayOpen) return;
            App.Audio?.Play(Cue.Tap);
            var key = App.Flow.RevealHint() ?? "ui.game.hint_none";
            _dialogue.Enqueue(new[] { new DialogueLine(key, DialoguePanel.HintSpeaker) });
        }

        private void OnCaption(string key)
        {
            _caption.Key = key;
            _captionTimer = 2.5f;
        }

        private void Haptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (App.Settings.Current.Haptics) Handheld.Vibrate();
#endif
        }

        private void Update()
        {
            if (App?.Flow.Session == null) return;
            var busy = _dialogue != null && _dialogue.IsBusy;
            App.Ads.DialogueActive = busy;
            App.Ads.PuzzleActive = _draggingItem != null || App.Flow.Controller.SelectedItemId != null;
            if (!busy && !OverlayOpen) App.Flow.Hints?.Tick(Time.unscaledDeltaTime);

            if (_captionTimer > 0 && (_captionTimer -= Time.unscaledDeltaTime) <= 0) _caption.SetLiteral(string.Empty);
            if (_hint != null)
            {
                var nudge = App.Flow.Hints != null && App.Flow.Hints.NudgeAvailable;
                _pulse += Time.unscaledDeltaTime * 4f;
                var t = nudge && !App.Settings.Current.ReducedMotion ? (Mathf.Sin(_pulse) + 1) / 2 : nudge ? 1 : 0;
                _hint.image.color = Color.Lerp(UiKit.Accent, Color.white, t * 0.6f);
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (OverlayOpen) CloseOverlay();
                else OpenPause();
            }
        }

        private RectTransform OpenOverlay(string name)
        {
            CloseOverlay();
            var safe = Overlay(name);
            _overlay = safe.parent.gameObject;
            return safe;
        }

        public void CloseOverlay()
        {
            if (_overlay != null) Destroy(_overlay);
            _overlay = null;
        }

        private void OpenPause()
        {
            var safe = OpenOverlay("Pause");
            var card = Card(safe, "PauseCard", new Vector2(800, 760));
            UiKit.Column(card, 18, 36);
            UiKit.Label("Title", card, "ui.pause.title", 56, Color.white).Size(-1, 90);
            UiKit.Button("Pause:Resume", card, "ui.pause.resume", CloseOverlay, UiKit.Accent).Size(-1, 96);
            UiKit.Button("Pause:Settings", card, "ui.menu.settings", () => SettingsPanel.Open(this)).Size(-1, 96);
            if (App.Flow.IsReplay)
                UiKit.Button("Pause:ExitReplay", card, "ui.pause.exit_replay", () =>
                {
                    App.Flow.ExitReplay();
                    Nav.Go(Nav.ChapterHub);
                }).Size(-1, 96);
            else
                UiKit.Button("Pause:Hub", card, "ui.pause.hub", () =>
                {
                    App.Flow.SaveNow();
                    Nav.Go(Nav.ChapterHub);
                }).Size(-1, 96);
            UiKit.Button("Pause:Quit", card, "ui.pause.quit", () =>
            {
                App.Flow.SaveNow();
                Nav.Go(Nav.Menu);
            }).Size(-1, 96);
        }

        private void OpenLog()
        {
            var safe = OpenOverlay("Log");
            var card = Card(safe, "LogCard", new Vector2(1500, 900));
            UiKit.Label("Title", card, "ui.game.log_title", 48, Color.white).GetComponent<RectTransform>()
                .Anchor(new Vector2(0, 0.88f), Vector2.one);
            var body = UiKit.Label("Body", card, null, 28, Color.white, TextAnchor.LowerLeft);
            body.Text.resizeTextForBestFit = false;
            body.Text.verticalOverflow = VerticalWrapMode.Truncate;
            body.GetComponent<RectTransform>().Anchor(Vector2.zero, new Vector2(1, 0.88f), new Vector2(40, 130), new Vector2(-40, 0));
            body.SetLiteral(string.Join("\n", _dialogue.Log.Skip(System.Math.Max(0, _dialogue.Log.Count - 18))));
            UiKit.Button("Log:Close", card, "ui.close", CloseOverlay, UiKit.Accent).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.4f, 0.02f), new Vector2(0.6f, 0.12f));
        }

        private void ShowChapterComplete(string chapterId)
        {
            var flow = App.Flow;
            var chapter = App.Content.Chapters.First(c => c.Id == chapterId);
            var safe = OpenOverlay("ChapterComplete");
            var card = Card(safe, "ChapterCard", new Vector2(1300, 760));
            UiKit.Column(card, 20, 40);
            UiKit.Label("Heading", card, "ui.chapter.complete", 40, UiKit.Accent).Size(-1, 70);
            UiKit.Label("Title", card, chapter.TitleKey, 64, Color.white).Size(-1, 100);
            UiKit.Label("Reveal", card, chapter.RevealKey, 34, UiKit.Muted).Size(-1, 160);
            var row = UiKit.Rect("Buttons", card);
            UiKit.Row(row, 30).Size(-1, 110);
            var gameDone = flow.ConsumeGameComplete();
            var next = flow.NextChapter(chapterId);
            if (gameDone || next == null)
                UiKit.Button("Chapter:Ending", row, "ui.chapter.finale", () => Nav.Go(Nav.Credits), UiKit.Accent).Size(420);
            else if (!flow.IsReplay)
                UiKit.Button("Chapter:Next", row, "ui.chapter.next", () =>
                {
                    CloseOverlay();
                    flow.StartChapter(next.Id);
                }, UiKit.Accent).Size(420);
            UiKit.Button("Chapter:Hub", row, "ui.chapter.hub", () =>
            {
                if (flow.IsReplay) flow.ExitReplay();
                Nav.Go(Nav.ChapterHub);
            }).Size(420);
        }

        private void ShowTestInterstitial(System.Action<bool> closed)
        {
            // development builds only: stands in for the SDK's full-screen ad
            var safe = OpenOverlay("TestInterstitial");
            UiKit.Label("Label", safe, "ui.ad.test_label", 56, Color.white).GetComponent<RectTransform>()
                .Anchor(new Vector2(0.1f, 0.4f), new Vector2(0.9f, 0.7f));
            UiKit.Button("Ad:Close", safe, "ui.close", () =>
            {
                CloseOverlay();
                closed(true);
            }, UiKit.Accent).GetComponent<RectTransform>().Anchor(new Vector2(0.4f, 0.15f), new Vector2(0.6f, 0.27f));
        }
    }
}
