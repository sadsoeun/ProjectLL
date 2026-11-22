using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Plays the user's own voice recordings on gameplay actions. A slot plays only when it is open in the current save
    /// (enough golden leaves), switched on and has at least one recording; otherwise the built-in sound is used as before.
    /// All voices share one audio source, so they never pile up: a new one interrupts the old one unless the old one has
    /// a higher priority (a jump never cuts off the "Now!" shout).
    /// </summary>
    public static class Voice
    {
        private static AudioSource source;
        private static int playingPriority;
        private static readonly int[] LastTake = new int[VoiceStudioConfig.SlotCount]; // filled with -1 in ResetStatics
        private static readonly List<int> Candidates = new List<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            source = null;
            playingPriority = 0;
            for (int i = 0; i < LastTake.Length; i++)
                LastTake[i] = -1;
        }

        /// <summary>The slot is open in this save: the golden-leaf total reached its threshold.</summary>
        public static bool IsUnlocked(VoiceSlot slot, SaveData save)
        {
            var def = VoiceStudioConfig.Load().Find(slot);
            return def != null && save != null && save.TotalLeaves() >= def.leavesRequired;
        }

        /// <summary>Plays the voice of the action when the player has one; otherwise (or on top, unless the slot replaces it) the built-in sound.</summary>
        public static void PlayOr(VoiceSlot slot, SfxId builtIn)
        {
            bool voiced = Play(slot);
            var def = VoiceStudioConfig.Load().Find(slot);
            if (!voiced || def == null || !def.replacesSfx)
                Sfx.Play(builtIn);
        }

        /// <summary>Plays a recording of the slot (a random owned take). False when nothing was played.</summary>
        public static bool Play(VoiceSlot slot)
        {
            if (!GameSystems.Exists || !GameSystems.Instance.Save.HasSave)
                return false;
            var save = GameSystems.Instance.Save.Current;
            var config = VoiceStudioConfig.Load();
            var def = config.Find(slot);
            if (def == null || save.TotalLeaves() < def.leavesRequired)
                return false;
            var progress = save.Voice;
            int index = (int)slot;
            if (progress.disabled[index])
                return false;

            Candidates.Clear();
            int owned = progress.OwnedTakes(slot, config.MaxTakes);
            for (int take = 0; take < owned; take++)
                if (VoiceLibrary.Has(slot, take))
                    Candidates.Add(take);
            if (Candidates.Count == 0)
                return false;
            // Several takes: never the same one twice in a row.
            if (Candidates.Count > 1)
                Candidates.Remove(LastTake[index]);
            int chosen = Candidates[Random.Range(0, Candidates.Count)];
            LastTake[index] = chosen;

            var output = Source();
            if (output.isPlaying && playingPriority > def.priority)
                return false; // a more important line is still being said
            playingPriority = def.priority;
            Start(output, VoiceLibrary.Get(slot, chosen), config);
            return true;
        }

        /// <summary>Voice Studio: listen to a clip (interrupts whatever voice is playing).</summary>
        public static void Preview(AudioClip clip)
        {
            if (clip == null || !GameSystems.Exists)
                return;
            playingPriority = int.MaxValue;
            Start(Source(), clip, VoiceStudioConfig.Load());
        }

        public static void Stop()
        {
            if (source != null)
                source.Stop();
            playingPriority = 0;
        }

        public static bool IsPlaying => source != null && source.isPlaying;

        /// <summary>0..1 position of the voice that is playing (0 when silent).</summary>
        public static float Progress =>
            source != null && source.isPlaying && source.clip != null && source.clip.length > 0f ? Mathf.Clamp01(source.time / source.clip.length) : 0f;

        private static void Start(AudioSource output, AudioClip clip, VoiceStudioConfig config)
        {
            output.Stop();
            output.clip = clip;
            output.volume = Mathf.Clamp01(Mathf.Clamp01(GameSystems.Instance.Settings.Data.sfxVolume) * config.voiceVolume);
            output.Play();
        }

        private static AudioSource Source()
        {
            if (source != null)
                return source;
            var go = new GameObject("PlayerVoice");
            go.transform.SetParent(GameSystems.Instance.Audio.transform, false);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
