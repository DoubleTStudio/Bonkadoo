using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bonkadoo.Presentation.Audio
{
    public enum Cue { Tap, Success, Fail, Pickup, Unlock, Page, Choice }

    /// <summary>
    /// Procedurally synthesized placeholder SFX and music (original, no third-party assets). Essential cues
    /// raise captions so no solution depends on audio (AUDIO_BIBLE).
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        private const int Rate = 22050;
        private static readonly Dictionary<Cue, string> CaptionKeys = new Dictionary<Cue, string>
        {
            [Cue.Success] = "ui.caption.success", [Cue.Fail] = "ui.caption.fail",
            [Cue.Pickup] = "ui.caption.pickup", [Cue.Unlock] = "ui.caption.unlock",
        };

        private readonly Dictionary<Cue, AudioClip> _cues = new Dictionary<Cue, AudioClip>();
        private readonly Dictionary<string, AudioClip> _music = new Dictionary<string, AudioClip>();
        private AudioSource _sfx, _musicSource;
        private string _currentMood;

        public static event Action<string> CaptionRaised;

        private void Awake()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _cues[Cue.Tap] = Tone("tap", new[] { 880f }, 0.05f, 0.25f);
            _cues[Cue.Page] = Tone("page", new[] { 660f, 990f }, 0.06f, 0.2f);
            _cues[Cue.Success] = Tone("success", new[] { 523f, 659f, 784f, 1046f }, 0.09f, 0.35f);
            _cues[Cue.Fail] = Tone("fail", new[] { 330f, 262f }, 0.14f, 0.35f);
            _cues[Cue.Pickup] = Tone("pickup", new[] { 784f, 1175f }, 0.07f, 0.3f);
            _cues[Cue.Unlock] = Tone("unlock", new[] { 392f, 523f, 659f }, 0.1f, 0.3f);
            _cues[Cue.Choice] = Tone("choice", new[] { 587f, 740f }, 0.07f, 0.25f);
            ApplyVolumes();
            if (AppServices.Current != null) AppServices.Current.Settings.Changed += _ => ApplyVolumes();
        }

        public void Play(Cue cue)
        {
            if (_cues.TryGetValue(cue, out var clip)) _sfx.PlayOneShot(clip);
            var app = AppServices.Current;
            if (app != null && app.Settings.Current.Captions && CaptionKeys.TryGetValue(cue, out var key)) CaptionRaised?.Invoke(key);
        }

        public void PlayMusic(string mood)
        {
            mood = string.IsNullOrEmpty(mood) ? "calm" : mood;
            if (mood == _currentMood && _musicSource.isPlaying) return;
            _currentMood = mood;
            if (!_music.TryGetValue(mood, out var clip)) _music[mood] = clip = Loop(mood);
            _musicSource.clip = clip;
            _musicSource.Play();
        }

        public void StopMusic()
        {
            _currentMood = null;
            _musicSource.Stop();
        }

        private void ApplyVolumes()
        {
            var s = AppServices.Current?.Settings.Current;
            _sfx.volume = s?.SfxVolume ?? 0.9f;
            _musicSource.volume = (s?.MusicVolume ?? 0.7f) * 0.35f;
        }

        private static AudioClip Tone(string name, float[] notes, float noteSeconds, float gain)
        {
            var per = Mathf.RoundToInt(noteSeconds * Rate);
            var data = new float[per * notes.Length];
            for (var n = 0; n < notes.Length; n++)
                for (var i = 0; i < per; i++)
                {
                    var t = (float)i / Rate;
                    var env = Mathf.Exp(-t * 18f) * Mathf.Min(1f, i / 40f);
                    data[n * per + i] = gain * env * (Mathf.Sin(2 * Mathf.PI * notes[n] * t) + 0.3f * Mathf.Sin(4 * Mathf.PI * notes[n] * t));
                }
            var clip = AudioClip.Create("sfx." + name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static readonly Dictionary<string, (float root, float bpm, int[] steps)> Moods =
            new Dictionary<string, (float, float, int[])>
            {
                ["calm"] = (220f, 84, new[] { 0, 4, 7, 9, 7, 4, 2, 4 }),
                ["office"] = (246.9f, 96, new[] { 0, 2, 4, 7, 4, 2, 0, -3 }),
                ["tension"] = (196f, 78, new[] { 0, 3, 7, 3, 0, -2, 0, 3 }),
                ["market"] = (261.6f, 112, new[] { 0, 4, 7, 12, 9, 7, 4, 2 }),
                ["archive"] = (174.6f, 70, new[] { 0, 7, 3, 10, 7, 3, 5, 2 }),
                ["core"] = (164.8f, 90, new[] { 0, 0, 7, 0, 5, 0, 3, 2 }),
            };

        private static AudioClip Loop(string mood)
        {
            if (!Moods.TryGetValue(mood, out var m)) m = Moods["calm"];
            var stepSeconds = 60f / m.bpm / 2f;
            var per = Mathf.RoundToInt(stepSeconds * Rate);
            var bars = 4;
            var data = new float[per * m.steps.Length * bars];
            for (var b = 0; b < bars; b++)
                for (var s = 0; s < m.steps.Length; s++)
                {
                    var semis = m.steps[s] + (b == 2 ? 5 : b == 3 ? -2 : 0);
                    var f = m.root * Mathf.Pow(2f, semis / 12f);
                    var offset = (b * m.steps.Length + s) * per;
                    for (var i = 0; i < per; i++)
                    {
                        var t = (float)i / Rate;
                        var pluck = Mathf.Exp(-t * 7f) * (Mathf.Sin(2 * Mathf.PI * f * t) + 0.25f * Mathf.Sin(6 * Mathf.PI * f * t));
                        var bass = s % 4 == 0 ? 0.35f * Mathf.Exp(-t * 3f) * Mathf.Sin(2 * Mathf.PI * m.root / 2f * t) : 0f;
                        data[offset + i] = 0.3f * (pluck + bass);
                    }
                }
            var clip = AudioClip.Create("music." + mood, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
