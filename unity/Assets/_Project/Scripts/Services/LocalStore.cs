using System;
using StackIt.Client;
using UnityEngine;

namespace StackIt.Services
{
    public sealed class PlayerPrefsStore : IKeyValueStore
    {
        public string GetString(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// Local settings and caches. The server stays the source of truth for coins, skins and scores;
    /// the cached profile only makes the home screen instant and works offline.
    /// </summary>
    public sealed class SaveService
    {
        private const string SoundKey = "stackit.sound";
        private const string HapticsKey = "stackit.haptics";
        private const string MusicKey = "stackit.music";
        private const string ReduceMotionKey = "stackit.reduce_motion";
        private const string LanguageKey = "stackit.language";
        private const string SessionsKey = "stackit.sessions";
        private const string ProfileKey = "stackit.profile_cache";
        private const string LocalBestKey = "stackit.local_best";
        private const string SeenCodePromptKey = "stackit.seen_code_prompt";

        private readonly IKeyValueStore _store;

        public SaveService(IKeyValueStore store)
        {
            _store = store;
        }

        public bool SoundOn
        {
            get => _store.GetString(SoundKey) != "0";
            set => _store.SetString(SoundKey, value ? "1" : "0");
        }

        public bool HapticsOn
        {
            get => _store.GetString(HapticsKey) != "0";
            set => _store.SetString(HapticsKey, value ? "1" : "0");
        }

        public bool MusicOn
        {
            get => _store.GetString(MusicKey) != "0";
            set => _store.SetString(MusicKey, value ? "1" : "0");
        }

        /// <summary>No breathing, shake, glow pulses or slow motion (simple fades stay).</summary>
        public bool ReduceMotion
        {
            get => _store.GetString(ReduceMotionKey) == "1";
            set => _store.SetString(ReduceMotionKey, value ? "1" : "0");
        }

        /// <summary>"en" / "es", or null to follow the device language.</summary>
        public string Language
        {
            get => _store.GetString(LanguageKey);
            set => _store.SetString(LanguageKey, value);
        }

        public int SessionCount { get; private set; }
        public bool IsFirstSession => SessionCount <= 1;

        public void CountSession()
        {
            int.TryParse(_store.GetString(SessionsKey), out int count);
            SessionCount = count + 1;
            _store.SetString(SessionsKey, SessionCount.ToString());
        }

        public bool SeenCodePrompt
        {
            get => _store.GetString(SeenCodePromptKey) == "1";
            set => _store.SetString(SeenCodePromptKey, value ? "1" : "0");
        }

        /// <summary>Best score including offline runs, for the HUD before the server answers.</summary>
        public int LocalBest
        {
            get
            {
                int.TryParse(_store.GetString(LocalBestKey), out int best);
                return best;
            }
            set => _store.SetString(LocalBestKey, Math.Max(value, LocalBest).ToString());
        }

        public string CachedProfileJson
        {
            get => _store.GetString(ProfileKey);
            set
            {
                if (value == null) _store.Delete(ProfileKey);
                else _store.SetString(ProfileKey, value);
            }
        }

        public void ClearAccountData()
        {
            _store.Delete(ProfileKey);
            _store.Delete(LocalBestKey);
        }
    }
}
