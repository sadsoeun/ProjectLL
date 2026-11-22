using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>Player voice lines the user can record in the Voice Studio. Values are stored in the save: never reorder.</summary>
    public enum VoiceSlot { Jump = 0, Dodge = 1, PerfectDodge = 2, Death = 3, Now = 4, StageClear = 5, BossDefeat = 6, TitleCall = 7 }

    [Serializable]
    public sealed class VoiceSlotDef
    {
        public VoiceSlot slot;
        [Tooltip("Golden leaves (total over all stages) needed to open this slot.")]
        [Min(0)] public int leavesRequired;
        [Tooltip("Longest recording kept for this slot (seconds).")]
        [Min(0.2f)] public float maxSeconds = 1f;
        [Tooltip("The recording replaces the built-in sound effect of the action instead of playing on top of it.")]
        public bool replacesSfx;
        [Tooltip("A voice that is still playing is only interrupted by one of the same or a higher priority.")]
        public int priority = 1;
    }

    /// <summary>
    /// Voice Studio tuning: unlock thresholds (golden leaves), take prices (coins), recording limits and clean-up.
    /// Loaded from Resources/Voice/VoiceStudioConfig; built-in defaults are used when the asset is missing.
    /// </summary>
    [CreateAssetMenu(menuName = "MyGame/Voice Studio Config", fileName = "VoiceStudioConfig")]
    public sealed class VoiceStudioConfig : ScriptableObject
    {
        public const string ResourcePath = "Voice/VoiceStudioConfig";
        public const int SlotCount = 8;

        [Header("Slots (unlocked by the total number of golden leaves)")]
        public VoiceSlotDef[] slots = new VoiceSlotDef[0];

        [Header("Extra takes (bought with coins; the takes of a slot play at random)")]
        [Tooltip("Price of take 2, take 3, ... Take 1 comes with the slot. The array length sets how many extra takes exist.")]
        public int[] extraTakePrices = { 30, 60 };

        [Header("Playback")]
        [Range(0f, 2f)] public float voiceVolume = 1f;

        [Header("Recording")]
        [Tooltip("Requested microphone sample rate (Hz).")]
        [Min(8000)] public int sampleRate = 44100;
        [Tooltip("Seconds of the 3-2-1 countdown before the microphone starts.")]
        [Min(0f)] public float countdownSeconds = 2.4f;
        [Tooltip("BGM volume while recording / listening (0 = silent), so the speakers do not leak into the microphone.")]
        [Range(0f, 1f)] public float recordBgmDuck = 0f;
        [Range(0f, 1f)] public float previewBgmDuck = 0.25f;

        [Header("Clean-up (applied to every recording)")]
        [Tooltip("A recording whose loudest sample is below this is rejected as too quiet (0..1).")]
        [Range(0.001f, 0.5f)] public float minPeak = 0.03f;
        [Tooltip("Leading / trailing audio quieter than this fraction of the loudest sample is cut.")]
        [Range(0.01f, 0.5f)] public float silenceFraction = 0.08f;
        [Tooltip("Seconds kept before the first / after the last loud sample.")]
        [Min(0f)] public float leadPadSeconds = 0.02f;
        [Min(0f)] public float tailPadSeconds = 0.12f;
        [Tooltip("The loudest sample is scaled to this level.")]
        [Range(0.1f, 1f)] public float targetPeak = 0.9f;
        [Tooltip("Upper limit of that scaling, so a faint recording does not turn into noise.")]
        [Min(1f)] public float maxGain = 8f;

        public int MaxTakes => 1 + (extraTakePrices != null ? extraTakePrices.Length : 0);

        private static VoiceStudioConfig cached;

        public static VoiceStudioConfig Load()
        {
            if (cached != null)
                return cached;
            cached = Resources.Load<VoiceStudioConfig>(ResourcePath);
            if (cached == null || cached.slots == null || cached.slots.Length == 0)
            {
                cached = CreateInstance<VoiceStudioConfig>();
                cached.ApplyDefaults();
            }
            else
                cached.WarnMissingSlots();
            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

        public VoiceSlotDef Find(VoiceSlot slot)
        {
            if (slots != null)
                foreach (var s in slots)
                    if (s != null && s.slot == slot)
                        return s;
            return null;
        }

        /// <summary>Coins for take <paramref name="take"/> (0-based; take 0 is free).</summary>
        public int TakePrice(int take) =>
            take <= 0 || extraTakePrices == null || take - 1 >= extraTakePrices.Length ? 0 : Mathf.Max(0, extraTakePrices[take - 1]);

        /// <summary>A slot without a definition can never open: say so instead of failing silently.</summary>
        private void WarnMissingSlots()
        {
            for (int i = 0; i < SlotCount; i++)
                if (Find((VoiceSlot)i) == null)
                    Debug.LogWarning($"[Voice] VoiceStudioConfig has no entry for the slot '{(VoiceSlot)i}': it stays locked.", this);
        }

        public void ApplyDefaults()
        {
            slots = new[]
            {
                new VoiceSlotDef { slot = VoiceSlot.Jump, leavesRequired = 5, maxSeconds = 0.6f, replacesSfx = false, priority = 1 },
                new VoiceSlotDef { slot = VoiceSlot.Dodge, leavesRequired = 12, maxSeconds = 0.6f, replacesSfx = false, priority = 1 },
                new VoiceSlotDef { slot = VoiceSlot.PerfectDodge, leavesRequired = 20, maxSeconds = 1.5f, replacesSfx = false, priority = 2 },
                new VoiceSlotDef { slot = VoiceSlot.Death, leavesRequired = 30, maxSeconds = 2f, replacesSfx = false, priority = 3 },
                new VoiceSlotDef { slot = VoiceSlot.Now, leavesRequired = 40, maxSeconds = 1.5f, replacesSfx = true, priority = 3 },
                // Late-game slots: the last one needs every golden leaf of the game (25 stages x 3).
                new VoiceSlotDef { slot = VoiceSlot.StageClear, leavesRequired = 50, maxSeconds = 2f, replacesSfx = false, priority = 3 },
                new VoiceSlotDef { slot = VoiceSlot.BossDefeat, leavesRequired = 62, maxSeconds = 2.5f, replacesSfx = false, priority = 4 },
                new VoiceSlotDef { slot = VoiceSlot.TitleCall, leavesRequired = 75, maxSeconds = 2.5f, replacesSfx = false, priority = 3 },
            };
            extraTakePrices = new[] { 30, 60 };
        }
    }
}
