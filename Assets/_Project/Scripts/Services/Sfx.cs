using UnityEngine;

namespace MyGame
{
    /// <summary>One-line entry point for gameplay sound effects. Silent when GameSystems is not running (edit-mode tests).</summary>
    public static class Sfx
    {
        public static void Play(SfxId id)
        {
            if (GameSystems.Exists)
                GameSystems.Instance.Audio.Play(id);
        }

        /// <summary>A specific clip (not in the bank); falls back to <paramref name="fallback"/> when the clip is null.</summary>
        public static void Play(AudioClip clip, float volume, SfxId fallback)
        {
            if (!GameSystems.Exists)
                return;
            if (clip != null)
                GameSystems.Instance.Audio.PlaySfx(clip, volume, 0.04f);
            else
                GameSystems.Instance.Audio.Play(fallback);
        }
    }
}
