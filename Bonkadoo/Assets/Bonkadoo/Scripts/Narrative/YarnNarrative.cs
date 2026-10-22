using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bonkadoo.Domain;
using Yarn;
using Yarn.Compiler;

namespace Bonkadoo.Narrative
{
    public sealed class NarrativeException : Exception
    {
        public NarrativeException(string message) : base(message) { }
    }

    /// <summary>
    /// Compiles chapter .yarn sources once. Yarn only reads world state through these functions;
    /// it never stores authoritative variables (D03).
    /// </summary>
    public sealed class YarnNarrative
    {
        public const string LinePrefix = "line:";
        public const string KeyPrefix = "yarn.";

        private Func<IReadOnlyWorldState> _state;
        public Program Program { get; }
        public Library Library { get; }
        public IReadOnlyCollection<string> Nodes { get; }

        public YarnNarrative(IEnumerable<string> sources, Func<IReadOnlyWorldState> state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            Library = BuildLibrary();
            var combined = string.Join("\n\n", sources.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (combined.Length == 0) throw new NarrativeException("no yarn sources");
            var result = Compiler.Compile(CompilationJob.CreateFromString("bonkadoo.yarn", combined, Library));
            var errors = result.Diagnostics.Where(d => d.Severity == Diagnostic.DiagnosticSeverity.Error).ToList();
            if (errors.Count > 0 || result.Program == null)
                throw new NarrativeException("Yarn compile failed:\n" + string.Join("\n", errors.Select(e => e.ToString())));
            Program = result.Program;
            Nodes = Program.Nodes.Keys.ToArray();
        }

        /// <summary>Points function lookups at a different session (e.g. after loading a replay branch).</summary>
        public void Rebind(Func<IReadOnlyWorldState> state) => _state = state ?? throw new ArgumentNullException(nameof(state));

        public static string KeyForLineId(string lineId) =>
            KeyPrefix + (lineId.StartsWith(LinePrefix, StringComparison.Ordinal) ? lineId.Substring(LinePrefix.Length) : lineId);

        private Library BuildLibrary()
        {
            var lib = new Library();
            lib.RegisterFunction("metric", (Func<string, float>)(k => _state().GetMetric(k)));
            lib.RegisterFunction("has_event", (Func<string, bool>)(t => _state().HasEvent(t)));
            lib.RegisterFunction("flag", (Func<string, bool>)(k => _state().HasFlag(k)));
            lib.RegisterFunction("flag_text", (Func<string, string>)(k =>
            {
                var v = _state().GetFlag(k);
                return v.HasValue ? v.Value.Serialize() : string.Empty;
            }));
            lib.RegisterFunction("npc", (Func<string, float>)(id => _state().GetNpcDisposition(id)));
            return lib;
        }
    }

    public enum ConversationState { Idle, Line, Options, Done }

    public readonly struct ConversationOption
    {
        public string Key { get; }
        public bool Available { get; }
        public ConversationOption(string key, bool available) { Key = key; Available = available; }
    }

    /// <summary>
    /// Pull-style runner over the Yarn VM: the UI shows <see cref="CurrentLine"/> or <see cref="Options"/>
    /// and calls <see cref="Advance"/> / <see cref="Choose"/>. <c>&lt;&lt;act id&gt;&gt;</c> commands are
    /// forwarded to the interaction engine, so choices change state only through content rules.
    /// </summary>
    public sealed class ConversationRunner
    {
        private readonly YarnNarrative _narrative;
        private readonly IReadOnlyDictionary<string, string> _speakers;
        private readonly Action<string> _onAct;
        private readonly Dialogue _dialogue;
        private readonly Queue<string> _queue = new Queue<string>();
        private bool _waiting;
        private bool _completed;
        private OptionSet.Option[] _rawOptions = Array.Empty<OptionSet.Option>();

        public ConversationState State { get; private set; } = ConversationState.Idle;
        public DialogueLine CurrentLine { get; private set; }
        public IReadOnlyList<ConversationOption> Options { get; private set; } = Array.Empty<ConversationOption>();
        public string CurrentNode { get; private set; }
        public bool IsActive => State == ConversationState.Line || State == ConversationState.Options;

        public event Action Changed;
        public event Action<string> Finished;

        public ConversationRunner(YarnNarrative narrative, IReadOnlyDictionary<string, string> speakers, Action<string> onAct)
        {
            _narrative = narrative ?? throw new ArgumentNullException(nameof(narrative));
            _speakers = speakers ?? new Dictionary<string, string>();
            _onAct = onAct ?? (_ => { });
            _dialogue = new Dialogue(new MemoryVariableStore())
            {
                LineHandler = OnLine,
                OptionsHandler = OnOptions,
                CommandHandler = OnCommand,
                DialogueCompleteHandler = OnComplete,
                LogDebugMessage = _ => { },
                LogErrorMessage = m => throw new NarrativeException(m),
            };
            _dialogue.Library.ImportLibrary(narrative.Library);
            _dialogue.SetProgram(narrative.Program);
        }

        /// <summary>Queues a node; starts it now if nothing is playing.</summary>
        public void Enqueue(string node)
        {
            if (string.IsNullOrEmpty(node)) return;
            if (!_dialogue.NodeExists(node)) throw new NarrativeException($"Unknown conversation node '{node}'");
            _queue.Enqueue(node);
            if (!IsActive) StartNext();
        }

        public void Advance()
        {
            if (State != ConversationState.Line) return;
            Pump();
        }

        public void Choose(int index)
        {
            if (State != ConversationState.Options) return;
            if (index < 0 || index >= _rawOptions.Length || !_rawOptions[index].IsAvailable)
                throw new ArgumentOutOfRangeException(nameof(index));
            _dialogue.SetSelectedOption(_rawOptions[index].ID);
            Pump();
        }

        /// <summary>Ends the current conversation and drops queued ones (e.g. leaving to the menu).</summary>
        public void StopAll()
        {
            _queue.Clear();
            if (_dialogue.IsActive) _dialogue.Stop();
            State = ConversationState.Idle;
            Changed?.Invoke();
        }

        private void StartNext()
        {
            while (_queue.Count > 0)
            {
                CurrentNode = _queue.Dequeue();
                _dialogue.SetNode(CurrentNode);
                Pump();
                if (IsActive) return;
            }
        }

        private void Pump()
        {
            _waiting = false;
            _completed = false;
            var guard = 0;
            // the VM reports inactive until its first Continue after SetNode
            do
            {
                if (++guard > 10000) throw new NarrativeException($"Conversation {CurrentNode} did not yield");
                _dialogue.Continue();
            } while (!_waiting && !_completed && _dialogue.IsActive);
            if (!_waiting) FinishNode();
        }

        private void OnLine(Line line)
        {
            var key = YarnNarrative.KeyForLineId(line.ID);
            _speakers.TryGetValue(key, out var speaker);
            CurrentLine = new DialogueLine(key, speaker == "narrator" ? null : speaker);
            State = ConversationState.Line;
            _waiting = true;
            Changed?.Invoke();
        }

        private void OnOptions(OptionSet options)
        {
            _rawOptions = options.Options;
            Options = _rawOptions.Select(o => new ConversationOption(YarnNarrative.KeyForLineId(o.Line.ID), o.IsAvailable)).ToArray();
            State = ConversationState.Options;
            _waiting = true;
            Changed?.Invoke();
        }

        private void OnCommand(Command command)
        {
            var parts = command.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && parts[0] == "act") _onAct(parts[1]);
            else throw new NarrativeException($"Unsupported Yarn command '{command.Text}'");
        }

        private void OnComplete() => _completed = true;

        private void FinishNode()
        {
            var finished = CurrentNode;
            State = ConversationState.Done;
            Finished?.Invoke(finished);
            if (_queue.Count > 0) StartNext();
            else Changed?.Invoke();
        }
    }
}
