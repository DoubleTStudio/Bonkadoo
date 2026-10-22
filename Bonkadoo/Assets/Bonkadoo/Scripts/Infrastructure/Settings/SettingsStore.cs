using System;
using System.Text;
using Bonkadoo.Application;
using Bonkadoo.Infrastructure.Save;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Bonkadoo.Infrastructure.Settings
{
    /// <summary>Player settings and local entitlement cache; stored apart from story saves (ARCHITECTURE).</summary>
    public sealed class GameSettings
    {
        public string Language;
        public float TextScale = 1f;
        public bool ReducedMotion;
        public float MusicVolume = 0.7f;
        public float SfxVolume = 0.9f;
        public bool Haptics = true;
        public bool Captions = true;
        public float DialogueSpeed = 1f;
        public int SessionCount;
        public bool RemoveAdsOwned;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();
    }

    public sealed class SettingsStore : IEntitlements
    {
        public const string FileName = "settings.json";
        public static readonly float[] TextScales = { 1f, 1.2f, 1.4f };
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private readonly IFileStore _files;
        public GameSettings Current { get; private set; }
        public event Action<GameSettings> Changed;
        event Action<bool> IEntitlements.Changed { add => _entitlementChanged += value; remove => _entitlementChanged -= value; }
        private event Action<bool> _entitlementChanged;

        public bool RemoveAdsOwned => Current.RemoveAdsOwned;
        public bool IsFirstSession => Current.SessionCount <= 1;

        public SettingsStore(IFileStore files)
        {
            _files = files ?? throw new ArgumentNullException(nameof(files));
            Current = Load();
        }

        /// <summary>Counts launches; the first session gets the interstitial grace (D17).</summary>
        public void BeginSession()
        {
            Current.SessionCount++;
            Save();
        }

        public void Update(Action<GameSettings> change)
        {
            var next = Current.Clone();
            change(next);
            next.TextScale = Clamp(next.TextScale, 0.8f, 1.6f);
            next.MusicVolume = Clamp(next.MusicVolume, 0f, 1f);
            next.SfxVolume = Clamp(next.SfxVolume, 0f, 1f);
            next.DialogueSpeed = Clamp(next.DialogueSpeed, 0.5f, 2f);
            var entitlementChanged = next.RemoveAdsOwned != Current.RemoveAdsOwned;
            Current = next;
            Save();
            Changed?.Invoke(Current);
            if (entitlementChanged) _entitlementChanged?.Invoke(Current.RemoveAdsOwned);
        }

        public void SetRemoveAdsOwned(bool owned) => Update(s => s.RemoveAdsOwned = owned);

        private GameSettings Load()
        {
            try
            {
                if (!_files.Exists(FileName)) return new GameSettings();
                var o = JsonConvert.DeserializeObject<JObject>(Utf8.GetString(_files.Read(FileName)));
                return o?.ToObject<GameSettings>() ?? new GameSettings();
            }
            catch (Exception ex) when (ex is JsonException || ex is System.IO.IOException || ex is ArgumentException)
            {
                // corrupt settings fall back to defaults; story saves are unaffected
                return new GameSettings();
            }
        }

        private void Save()
        {
            var bytes = Utf8.GetBytes(JObject.FromObject(Current).ToString(Formatting.None));
            try
            {
                _files.Write(FileName + ".tmp", bytes);
                _files.Replace(FileName + ".tmp", FileName);
            }
            catch (System.IO.IOException)
            {
                // settings persistence is best-effort
            }
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
    }
}
