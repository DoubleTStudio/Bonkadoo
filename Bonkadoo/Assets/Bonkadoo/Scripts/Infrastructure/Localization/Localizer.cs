using System;
using System.Collections.Generic;
using System.Linq;

namespace Bonkadoo.Infrastructure.Localization
{
    public interface ILocalizer
    {
        string Language { get; }
        IReadOnlyList<string> Languages { get; }
        string Get(string key);
        bool Has(string key);
        void SetLanguage(string language);
        event Action<string> LanguageChanged;
    }

    /// <summary>
    /// Runtime-switchable string tables. Development builds fall back to English and mark the gap;
    /// release builds never show a raw key because the validator blocks missing keys.
    /// </summary>
    public sealed class Localizer : ILocalizer
    {
        public const string FallbackLanguage = "en";

        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _tables;
        private readonly bool _development;
        private IReadOnlyDictionary<string, string> _current;

        public string Language { get; private set; }
        public IReadOnlyList<string> Languages { get; }
        public event Action<string> LanguageChanged;

        public Localizer(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables, string language,
            bool development)
        {
            _tables = tables ?? throw new ArgumentNullException(nameof(tables));
            if (!_tables.ContainsKey(FallbackLanguage)) throw new ArgumentException("English table required");
            _development = development;
            Languages = _tables.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray();
            Select(language);
        }

        public bool Has(string key) => key != null && _current.ContainsKey(key);

        public string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (_current.TryGetValue(key, out var text)) return text;
            if (_tables[FallbackLanguage].TryGetValue(key, out var en)) return _development ? "[EN] " + en : en;
            return _development ? "⟦" + key + "⟧" : string.Empty;
        }

        public void SetLanguage(string language)
        {
            if (string.Equals(language, Language, StringComparison.Ordinal)) return;
            Select(language);
            LanguageChanged?.Invoke(Language);
        }

        private void Select(string language)
        {
            if (language == null || !_tables.TryGetValue(language, out var table))
                throw new ArgumentException($"Unsupported language '{language}'");
            Language = language;
            _current = table;
        }

        /// <summary>Keys present in one table but not another; empty means parity.</summary>
        public static IReadOnlyList<string> FindParityGaps(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> tables)
        {
            var all = new SortedSet<string>(tables.Values.SelectMany(t => t.Keys), StringComparer.Ordinal);
            var gaps = new List<string>();
            foreach (var lang in tables.Keys.OrderBy(k => k, StringComparer.Ordinal))
                foreach (var key in all)
                    if (!tables[lang].ContainsKey(key)) gaps.Add($"{lang}:{key}");
            return gaps;
        }
    }
}
