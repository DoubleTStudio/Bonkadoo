using System;
using System.Collections.Generic;
using System.Globalization;
using Bonkadoo.Domain;

namespace Bonkadoo.Application
{
    public enum HotspotKind { Prop, Npc, Exit, Option }

    /// <summary>Scene-authored metadata for one tappable thing; IDs come from content, never from display text.</summary>
    public sealed class HotspotInfo
    {
        public string Id { get; }
        public HotspotKind Kind { get; }
        /// <summary>Optional transient context sent with the request (e.g. input.ch01.p01.choice = "a").</summary>
        public string InputKey { get; }
        public string InputValue { get; }

        public HotspotInfo(string id, HotspotKind kind, string inputKey = null, string inputValue = null)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("id required", nameof(id));
            if (inputKey != null && !WorldKeys.IsTransient(inputKey))
                throw new ArgumentException($"Hotspot input key '{inputKey}' must start with '{WorldKeys.InputPrefix}'");
            Id = id;
            Kind = kind;
            InputKey = inputKey;
            InputValue = inputValue;
        }
    }

    public interface ITickSource
    {
        long Now { get; }
    }

    /// <summary>Unique, stable request IDs: one nonce per app run plus a counter.</summary>
    public sealed class RequestIdFactory
    {
        private readonly string _nonce;
        private long _counter;

        public RequestIdFactory(string nonce)
        {
            if (string.IsNullOrEmpty(nonce)) throw new ArgumentException("nonce required", nameof(nonce));
            _nonce = nonce;
        }

        public string Next() => _nonce + "-" + (++_counter).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Turns player gestures into interaction requests. Drag-item-onto-target and
    /// select-item-then-tap-target produce identical requests (tap alternative to drag).
    /// </summary>
    public sealed class InteractionController
    {
        private readonly GameSession _session;
        private readonly RequestIdFactory _ids;
        private readonly ITickSource _ticks;

        public string SelectedItemId { get; private set; }
        public InteractionOutcome LastOutcome { get; private set; }

        public event Action<string> SelectionChanged;
        public event Action<InteractionRequest, InteractionOutcome> Resolved;

        public InteractionController(GameSession session, RequestIdFactory ids, ITickSource ticks)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _ids = ids ?? throw new ArgumentNullException(nameof(ids));
            _ticks = ticks ?? throw new ArgumentNullException(nameof(ticks));
        }

        /// <summary>Selecting the selected item again clears it (reversible selection).</summary>
        public bool ToggleItem(string itemId)
        {
            if (itemId != null && string.Equals(itemId, SelectedItemId, StringComparison.Ordinal))
            {
                SetSelection(null);
                return true;
            }
            if (itemId != null && !_session.State.HasItem(itemId)) return false;
            SetSelection(itemId);
            return true;
        }

        public void ClearSelection() => SetSelection(null);

        public InteractionOutcome Tap(HotspotInfo hotspot)
        {
            if (hotspot == null) throw new ArgumentNullException(nameof(hotspot));
            if (SelectedItemId != null)
            {
                var item = SelectedItemId;
                SetSelection(null);
                return Send(InteractionVerb.UseItem, item, hotspot);
            }
            return Send(DefaultVerb(hotspot.Kind), null, hotspot);
        }

        public InteractionOutcome DragItemOnto(string itemId, HotspotInfo hotspot)
        {
            if (hotspot == null) throw new ArgumentNullException(nameof(hotspot));
            if (!_session.State.HasItem(itemId)) return null;
            SetSelection(null);
            return Send(InteractionVerb.UseItem, itemId, hotspot);
        }

        public static InteractionVerb DefaultVerb(HotspotKind kind)
        {
            switch (kind)
            {
                case HotspotKind.Npc: return InteractionVerb.Talk;
                case HotspotKind.Prop: return InteractionVerb.Inspect;
                default: return InteractionVerb.Tap;
            }
        }

        private InteractionOutcome Send(InteractionVerb verb, string sourceId, HotspotInfo hotspot)
        {
            Dictionary<string, FactValue> input = null;
            if (hotspot.InputKey != null)
                input = new Dictionary<string, FactValue>(StringComparer.Ordinal)
                {
                    [hotspot.InputKey] = FactValue.Of(hotspot.InputValue ?? string.Empty),
                };
            var request = new InteractionRequest(_ids.Next(), verb, sourceId, hotspot.Id, _session.State.CurrentSceneId,
                _ticks.Now, input);
            LastOutcome = _session.Interact(request);
            Resolved?.Invoke(request, LastOutcome);
            return LastOutcome;
        }

        private void SetSelection(string itemId)
        {
            if (string.Equals(itemId, SelectedItemId, StringComparison.Ordinal)) return;
            SelectedItemId = itemId;
            SelectionChanged?.Invoke(itemId);
        }
    }
}
