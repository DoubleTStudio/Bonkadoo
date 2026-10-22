using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Bonkadoo.Infrastructure.Save
{
    /// <summary>One step of the N -> N+1 chain. Operates on raw JSON so old shapes never need C# types.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        JObject Migrate(JObject save);
    }

    public sealed class SaveMigrationException : Exception
    {
        public bool IsFutureVersion { get; }
        public SaveMigrationException(string message, bool isFutureVersion = false) : base(message) =>
            IsFutureVersion = isFutureVersion;
    }

    public sealed class SaveMigrator
    {
        private readonly Dictionary<int, ISaveMigration> _steps;
        public int TargetVersion { get; }

        public SaveMigrator(int targetVersion, IEnumerable<ISaveMigration> steps)
        {
            TargetVersion = targetVersion;
            _steps = new Dictionary<int, ISaveMigration>();
            foreach (var s in steps ?? Enumerable.Empty<ISaveMigration>())
            {
                if (_steps.ContainsKey(s.FromVersion)) throw new ArgumentException($"Two migrations from v{s.FromVersion}");
                _steps.Add(s.FromVersion, s);
            }
        }

        /// <summary>No shipped migrations yet: v1 is the first save format. Register new steps here.</summary>
        public static SaveMigrator Default => new SaveMigrator(SaveGame.CurrentVersion, Array.Empty<ISaveMigration>());

        public JObject Upgrade(JObject save)
        {
            var version = (int?)save["saveVersion"] ?? throw new SaveMigrationException("saveVersion missing");
            if (version > TargetVersion)
                throw new SaveMigrationException($"Save v{version} is newer than this build (v{TargetVersion})", true);
            var current = (JObject)save.DeepClone();
            while (version < TargetVersion)
            {
                if (!_steps.TryGetValue(version, out var step))
                    throw new SaveMigrationException($"No migration from v{version}");
                current = step.Migrate(current);
                var next = (int?)current["saveVersion"];
                if (next != version + 1)
                    throw new SaveMigrationException($"Migration from v{version} produced v{next}, expected v{version + 1}");
                version = next.Value;
            }
            return current;
        }
    }
}
