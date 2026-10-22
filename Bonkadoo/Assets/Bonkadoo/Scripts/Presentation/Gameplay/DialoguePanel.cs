using System;
using System.Collections.Generic;
using Bonkadoo.Domain;
using Bonkadoo.Narrative;
using Bonkadoo.Presentation.Audio;
using Bonkadoo.Presentation.Ui;
using UnityEngine;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Gameplay
{
    /// <summary>
    /// Shows interaction feedback lines first, then any Yarn conversation. Typewriter speed follows settings;
    /// reduced motion shows lines instantly. Tap completes the line, tap again advances.
    /// </summary>
    public sealed class DialoguePanel : MonoBehaviour
    {
        public const string HintSpeaker = "hint";
        private const int LogLimit = 80;

        private readonly Queue<DialogueLine> _pending = new Queue<DialogueLine>();
        private readonly List<string> _log = new List<string>();
        private ConversationRunner _runner;
        private LocalizedText _speaker, _text, _continue;
        private RectTransform _options;
        private string _full;
        private float _shown;
        private bool _showingPending;

        public bool IsBusy => gameObject.activeSelf;
        public IReadOnlyList<string> Log => _log;
        public string CurrentText => _full;
        public event Action Idle;

        public static DialoguePanel Create(RectTransform parent)
        {
            var bg = UiKit.Image("DialoguePanel", parent, new Color(0.1f, 0.1f, 0.18f, 1f), raycast: true);
            bg.rectTransform.Anchor(new Vector2(0.03f, 0f), new Vector2(0.97f, 0.34f), new Vector2(0, 12), Vector2.zero);
            var button = bg.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            var panel = bg.gameObject.AddComponent<DialoguePanel>();
            button.onClick.AddListener(panel.Tap);
            panel._speaker = UiKit.Label("Speaker", bg.transform, null, 34, UiKit.Accent, TextAnchor.UpperLeft);
            panel._speaker.GetComponent<RectTransform>().Anchor(new Vector2(0, 0.74f), Vector2.one, new Vector2(36, 0), new Vector2(-36, -12));
            panel._text = UiKit.Label("Text", bg.transform, null, 38, Color.white, TextAnchor.UpperLeft);
            panel._text.GetComponent<RectTransform>().Anchor(Vector2.zero, new Vector2(1, 0.76f), new Vector2(36, 50), new Vector2(-36, 0));
            panel._continue = UiKit.Label("Continue", bg.transform, "ui.game.tap_continue", 22, UiKit.Muted, TextAnchor.LowerRight);
            panel._continue.GetComponent<RectTransform>().Anchor(Vector2.zero, new Vector2(1, 0.2f), new Vector2(36, 8), new Vector2(-36, 0));
            panel._options = UiKit.Rect("Options", bg.transform).Anchor(new Vector2(0.45f, 0.05f), new Vector2(0.98f, 0.95f));
            UiKit.Column(panel._options, 10);
            bg.gameObject.SetActive(false);
            return panel;
        }

        public void Bind(ConversationRunner runner)
        {
            if (_runner != null) _runner.Changed -= OnRunnerChanged;
            _runner = runner;
            if (_runner != null) _runner.Changed += OnRunnerChanged;
        }

        // the runner outlives this scene; a dangling subscription would touch destroyed UI
        private void OnDestroy() => Bind(null);

        public void Enqueue(IEnumerable<DialogueLine> lines)
        {
            foreach (var l in lines) _pending.Enqueue(l);
            if (!_showingPending && (_runner == null || !_runner.IsActive)) Next();
        }

        /// <summary>Picks up a conversation that started before this panel existed.</summary>
        public void Sync()
        {
            if (!_showingPending) Next();
        }

        public void Tap()
        {
            if (_full != null && _shown < _full.Length)
            {
                _shown = _full.Length;
                return;
            }
            AppServices.Current?.Audio?.Play(Cue.Page);
            if (_showingPending) Next();
            else if (_runner != null && _runner.State == ConversationState.Line) _runner.Advance();
        }

        private void OnRunnerChanged()
        {
            if (!_showingPending) Render();
        }

        private void Next()
        {
            if (_pending.Count > 0)
            {
                _showingPending = true;
                var line = _pending.Dequeue();
                ShowLine(line.Speaker, AppServices.Current.T(line.Key));
                return;
            }
            _showingPending = false;
            Render();
        }

        private void Render()
        {
            UiKit.Clear(_options);
            if (_runner != null && _runner.State == ConversationState.Line)
            {
                ShowLine(_runner.CurrentLine.Speaker, AppServices.Current.T(_runner.CurrentLine.Key));
                return;
            }
            if (_runner != null && _runner.State == ConversationState.Options)
            {
                gameObject.SetActive(true);
                _continue.gameObject.SetActive(false);
                for (var i = 0; i < _runner.Options.Count; i++)
                {
                    var index = i;
                    var opt = _runner.Options[i];
                    var b = UiKit.Button("Option:" + i, _options, opt.Key, () =>
                    {
                        AppServices.Current?.Audio?.Play(Cue.Choice);
                        AddLog("ti", AppServices.Current.T(opt.Key));
                        _runner.Choose(index);
                    }, UiKit.Calm, 30);
                    b.interactable = opt.Available;
                    b.Size(-1, 90);
                }
                return;
            }
            _full = null;
            gameObject.SetActive(false);
            Idle?.Invoke();
        }

        private void ShowLine(string speaker, string text)
        {
            gameObject.SetActive(true);
            UiKit.Clear(_options);
            _continue.gameObject.SetActive(true);
            var name = speaker == HintSpeaker ? AppServices.Current.T("ui.game.hint") : AppServices.Current.Speaker(speaker);
            _speaker.SetLiteral(name);
            _full = text ?? string.Empty;
            _shown = AppServices.Current.Settings.Current.ReducedMotion ? _full.Length : 0;
            _text.SetLiteral(_full.Substring(0, (int)_shown));
            AddLog(name, _full);
        }

        private void AddLog(string speaker, string text)
        {
            _log.Add(string.IsNullOrEmpty(speaker) ? text : speaker + ": " + text);
            if (_log.Count > LogLimit) _log.RemoveAt(0);
        }

        private void Update()
        {
            if (_full == null || _shown >= _full.Length) return;
            var speed = 45f * (AppServices.Current?.Settings.Current.DialogueSpeed ?? 1f);
            _shown = Mathf.Min(_full.Length, _shown + speed * Time.unscaledDeltaTime);
            _text.SetLiteral(_full.Substring(0, (int)_shown));
        }
    }
}
