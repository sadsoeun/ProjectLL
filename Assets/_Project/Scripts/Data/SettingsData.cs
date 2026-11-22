using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>Device settings persisted to settings.json (spec 6).</summary>
    [Serializable]
    public sealed class SettingsData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public bool hasSelectedLanguage;
        public string languageCode = "en";
        public float masterVolume = 1f;
        public float bgmVolume = 0.8f;
        public float sfxVolume = 0.8f;
        public bool fullscreen = true;
        public string resolution = "1920x1080";
        public bool vSync = true;
        /// <summary>Ranking nickname. Empty until the player chose one (asked before the first game start).</summary>
        public string nickname = "";

        /// <summary>Clamps loaded values into valid ranges. Returns false if the data is unusable.</summary>
        public bool Sanitize()
        {
            if (version < 1 || version > CurrentVersion)
                return false;
            if (string.IsNullOrEmpty(languageCode))
                languageCode = "en";
            masterVolume = SanitizeVolume(masterVolume, 1f);
            bgmVolume = SanitizeVolume(bgmVolume, 0.8f);
            sfxVolume = SanitizeVolume(sfxVolume, 0.8f);
            if (string.IsNullOrEmpty(resolution))
                resolution = "1920x1080";
            nickname = RankingBoards.CleanNickname(nickname);
            return true;
        }

        private static float SanitizeVolume(float v, float fallback)
        {
            if (float.IsNaN(v) || float.IsInfinity(v))
                return fallback;
            return Mathf.Clamp01(v);
        }
    }
}
