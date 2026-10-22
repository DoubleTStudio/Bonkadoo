using System;
using System.Collections.Generic;
using Bonkadoo.Domain;

namespace Bonkadoo.Application
{
    public interface IEventStore
    {
        long Count { get; }
        IReadOnlyList<WorldEvent> ReadFrom(long cursor);

        /// <summary>Appends all events or none. Events whose ID is already stored are skipped.</summary>
        /// <returns>Number of events actually appended.</returns>
        int AppendAtomic(IReadOnlyList<WorldEvent> events);
    }

    public sealed class InMemoryEventStore : IEventStore
    {
        private readonly List<WorldEvent> _events = new List<WorldEvent>();
        private readonly HashSet<string> _ids = new HashSet<string>(StringComparer.Ordinal);

        public InMemoryEventStore() { }

        public InMemoryEventStore(IEnumerable<WorldEvent> existing)
        {
            foreach (var e in existing)
            {
                if (!_ids.Add(e.Id)) throw new ArgumentException($"Duplicate event id in journal: {e.Id}");
                _events.Add(e);
            }
        }

        public long Count => _events.Count;

        public IReadOnlyList<WorldEvent> ReadFrom(long cursor)
        {
            if (cursor < 0 || cursor > _events.Count) throw new ArgumentOutOfRangeException(nameof(cursor));
            return _events.GetRange((int)cursor, _events.Count - (int)cursor);
        }

        public int AppendAtomic(IReadOnlyList<WorldEvent> events)
        {
            if (events == null) throw new ArgumentNullException(nameof(events));
            var fresh = new List<WorldEvent>(events.Count);
            var batch = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in events)
            {
                if (e == null) throw new ArgumentException("null event in batch");
                if (_ids.Contains(e.Id)) continue;
                if (!batch.Add(e.Id)) throw new ArgumentException($"Duplicate event id in batch: {e.Id}");
                fresh.Add(e);
            }
            // validation finished before any mutation
            foreach (var e in fresh)
            {
                _ids.Add(e.Id);
                _events.Add(e);
            }
            return fresh.Count;
        }
    }
}
