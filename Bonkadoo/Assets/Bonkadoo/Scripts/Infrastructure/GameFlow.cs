using System;
using System.Collections.Generic;
using System.Linq;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Narrative;

namespace Bonkadoo.Infrastructure
{
    public enum Ending { None, Patch, Rewrite, Reveal, WalkAway }

    /// <summary>
    /// Session lifecycle: new game, continue, chapter start checkpoints, replay branch (never touches the
    /// main slot, TC-010), conversations triggered by content, chapter/ending detection.
    /// </summary>
    public sealed class GameFlow
    {
        public const string ReplaySlot = "replay";
        public const string GameCompleteFlag = "flag.game.complete";
        public const string EndingFlag = "flag.ending";

        private readonly SaveService _saves;
        private readonly CheckpointService _checkpoints;
        private readonly InteractionEngine _engine;
        private readonly ITickSource _ticks;
        private readonly Func<string> _nonce;
        private readonly HashSet<string> _completedBefore = new HashSet<string>(StringComparer.Ordinal);
        private bool _gameCompleteBefore;

        public GameContent Content { get; }
        public YarnNarrative Narrative { get; }
        public GameSession Session { get; private set; }
        public InteractionController Controller { get; private set; }
        public HintService Hints { get; private set; }
        public ConversationRunner Conversation { get; private set; }
        public AutosaveCoordinator Autosave { get; private set; }
        public string ActiveSlot { get; private set; } = CheckpointService.PrimarySlot;
        public bool IsReplay => ActiveSlot == ReplaySlot;

        /// <summary>Set when a chapter's complete flag flips; the UI shows the chapter card once dialogue ends.</summary>
        public string PendingChapterComplete { get; private set; }
        public bool PendingGameComplete { get; private set; }

        public event Action SessionStarted;
        public event Action<InteractionRequest, InteractionOutcome> OutcomeResolved;
        public event Action<string, string> SceneChanged;
        public event Action<Exception> SaveFailed;

        public GameFlow(GameContent content, SaveService saves, ITickSource ticks, Func<string> nonce = null)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
            _nonce = nonce ?? (() => Guid.NewGuid().ToString("N"));
            _engine = new InteractionEngine(content.Rules);
            _checkpoints = new CheckpointService(saves, content.World, _engine);
            if (content.YarnSources != null && content.YarnSources.Count > 0)
                Narrative = new YarnNarrative(content.YarnSources.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Value),
                    () => Session?.State ?? new WorldState(content.World));
        }

        public bool HasMainSave => _saves.Load(CheckpointService.PrimarySlot).Game != null;

        public LoadStatus MainSaveStatus => _saves.Load(CheckpointService.PrimarySlot).Status;

        public void NewGame()
        {
            _saves.DeleteSlot(CheckpointService.PrimarySlot);
            foreach (var ch in Content.Chapters) _saves.DeleteSlot(StartSlot(ch.Id));
            Bind(NewSession(), CheckpointService.PrimarySlot);
            StartChapter(Content.Chapters[0].Id);
        }

        /// <summary>Restores the main save; false when there is nothing readable to continue.</summary>
        public bool Continue()
        {
            if (!_checkpoints.TryRestore(CheckpointService.PrimarySlot, out var session, out _)) return false;
            Bind(session, CheckpointService.PrimarySlot);
            if (Session.State.CurrentSceneId == null) StartChapter(Content.Chapters[0].Id);
            return true;
        }

        public bool IsChapterComplete(string chapterId) =>
            Session != null && Session.State.HasFlag(Chapter(chapterId).CompleteFlag);

        public bool IsChapterUnlocked(string chapterId)
        {
            var index = IndexOf(chapterId);
            return index == 0 || (Session != null && Session.State.HasFlag(Content.Chapters[index - 1].CompleteFlag));
        }

        /// <summary>Chapters completed in the main save; replay is offered only for these.</summary>
        public bool CanReplay(string chapterId) => !IsReplay && IsChapterComplete(chapterId) && _saves.Load(StartSlot(chapterId)).Game != null;

        public ChapterDefinition CurrentChapter => Session == null ? null : Content.ChapterOfScene(Session.State.CurrentSceneId);

        public ChapterDefinition NextChapter(string chapterId)
        {
            var i = IndexOf(chapterId);
            return i + 1 < Content.Chapters.Count ? Content.Chapters[i + 1] : null;
        }

        public void StartChapter(string chapterId)
        {
            if (!IsChapterUnlocked(chapterId)) throw new InvalidOperationException($"{chapterId} is locked");
            var chapter = Chapter(chapterId);
            if (!IsReplay)
                _checkpoints.SaveCheckpoint(StartSlot(chapterId), Session, new SaveMetadata { ChapterId = chapterId, CheckpointId = "start" });
            PendingChapterComplete = null;
            Controller.EnterScene(chapter.StartScene);
        }

        /// <summary>Replays a finished chapter from its start checkpoint in a separate slot.</summary>
        public bool StartReplay(string chapterId)
        {
            if (!CanReplay(chapterId)) return false;
            if (!_checkpoints.TryRestore(StartSlot(chapterId), out var session, out _)) return false;
            _saves.DeleteSlot(ReplaySlot);
            Bind(session, ReplaySlot);
            PendingChapterComplete = null;
            Controller.EnterScene(Chapter(chapterId).StartScene);
            return true;
        }

        public bool ExitReplay()
        {
            if (!IsReplay) return false;
            Conversation?.StopAll();
            _saves.DeleteSlot(ReplaySlot);
            return Continue();
        }

        public string ConsumeChapterComplete()
        {
            var c = PendingChapterComplete;
            PendingChapterComplete = null;
            return c;
        }

        public bool ConsumeGameComplete()
        {
            var c = PendingGameComplete;
            PendingGameComplete = false;
            return c;
        }

        public Ending CurrentEnding
        {
            get
            {
                var v = Session?.State.GetFlag(EndingFlag);
                if (!v.HasValue || v.Value.Kind != FactKind.String) return Ending.None;
                switch (v.Value.AsString)
                {
                    case "PATCH": return Ending.Patch;
                    case "REWRITE": return Ending.Rewrite;
                    case "REVEAL": return Ending.Reveal;
                    case "WALK_AWAY": return Ending.WalkAway;
                    default: return Ending.None;
                }
            }
        }

        public bool GameComplete => Session != null && Session.State.HasFlag(GameCompleteFlag);

        public string RevealHint() => Hints?.RevealNext(_nonce() + "-hint", _ticks.Now);

        public bool SaveNow() => Autosave != null && Autosave.SaveNow();

        public static string StartSlot(string chapterId) => "start_" + chapterId;

        private GameSession NewSession() => new GameSession(Content.World, _engine, new InMemoryEventStore());

        private void Bind(GameSession session, string slot)
        {
            Autosave?.Dispose();
            Conversation?.StopAll();
            Session = session;
            ActiveSlot = slot;
            Controller = new InteractionController(session, new RequestIdFactory(_nonce()), _ticks);
            Hints = new HintService(session, Content.Rules, Controller);
            Autosave = new AutosaveCoordinator(session, _checkpoints,
                () => new SaveMetadata { ChapterId = CurrentChapter?.Id, CheckpointId = session.State.CurrentSceneId }, slot);
            Autosave.SaveFailed += ex => SaveFailed?.Invoke(ex);
            if (Narrative != null)
            {
                Narrative.Rebind(() => Session.State);
                Conversation = new ConversationRunner(Narrative, Content.DialogueSpeakers, act => Controller.Choose(act));
            }
            _completedBefore.Clear();
            foreach (var ch in Content.Chapters)
                if (session.State.HasFlag(ch.CompleteFlag)) _completedBefore.Add(ch.Id);
            _gameCompleteBefore = session.State.HasFlag(GameCompleteFlag);
            PendingChapterComplete = null;
            PendingGameComplete = false;

            Controller.Resolved += OnResolved;
            Controller.SceneChanged += (from, to) => SceneChanged?.Invoke(from, to);
            SessionStarted?.Invoke();
        }

        private void OnResolved(InteractionRequest request, InteractionOutcome outcome)
        {
            if (outcome.Status == OutcomeStatus.Committed && Conversation != null)
                foreach (var node in outcome.Conversations) Conversation.Enqueue(node);

            foreach (var ch in Content.Chapters)
            {
                if (_completedBefore.Contains(ch.Id) || !Session.State.HasFlag(ch.CompleteFlag)) continue;
                _completedBefore.Add(ch.Id);
                PendingChapterComplete = ch.Id;
            }
            if (!_gameCompleteBefore && Session.State.HasFlag(GameCompleteFlag))
            {
                _gameCompleteBefore = true;
                PendingGameComplete = true;
            }
            OutcomeResolved?.Invoke(request, outcome);
        }

        private ChapterDefinition Chapter(string id) =>
            Content.Chapters.FirstOrDefault(c => c.Id == id) ?? throw new ArgumentException($"Unknown chapter {id}");

        private int IndexOf(string id)
        {
            for (var i = 0; i < Content.Chapters.Count; i++) if (Content.Chapters[i].Id == id) return i;
            throw new ArgumentException($"Unknown chapter {id}");
        }
    }
}
