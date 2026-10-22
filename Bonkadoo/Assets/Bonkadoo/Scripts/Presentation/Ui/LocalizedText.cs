using UnityEngine;
using UnityEngine.UI;

namespace Bonkadoo.Presentation.Ui
{
    /// <summary>Binds a Text to a localization key (or a literal) and re-renders on language or text-size changes.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        private Text _text;
        private int _baseSize;
        private string _key;
        private string _literal;
        private object[] _args;

        public Text Text => _text;
        public string Key { get => _key; set { _key = value; _literal = null; Refresh(); } }
        public string Shown => _text != null ? _text.text : null;

        public void Init(Text text, int baseSize)
        {
            _text = text;
            _baseSize = baseSize;
        }

        /// <summary>Shows text that is already localized (e.g. composed by a screen).</summary>
        public void SetLiteral(string value)
        {
            _key = null;
            _literal = value;
            Refresh();
        }

        public void SetArgs(params object[] args)
        {
            _args = args;
            Refresh();
        }

        private void OnEnable()
        {
            AppServices.LanguageOrScaleChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => AppServices.LanguageOrScaleChanged -= Refresh;

        public void Refresh()
        {
            if (_text == null) return;
            var scale = AppServices.Current?.Settings.Current.TextScale ?? 1f;
            _text.resizeTextMaxSize = Mathf.RoundToInt(_baseSize * scale);
            _text.fontSize = _text.resizeTextMaxSize;
            if (_literal != null) { _text.text = _literal; return; }
            if (string.IsNullOrEmpty(_key)) { _text.text = string.Empty; return; }
            var s = AppServices.Current?.Localizer.Get(_key) ?? _key;
            _text.text = _args != null && _args.Length > 0 ? SafeFormat(s, _args) : s;
        }

        private static string SafeFormat(string format, object[] args)
        {
            try { return string.Format(format, args); }
            catch (System.FormatException) { return format; }
        }
    }
}
