using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>Sound effect identifiers. Append new values at the end (the bank stores them by value).</summary>
    public enum SfxId
    {
        UiClick = 0,
        Jump = 1,
        Land = 2,
        Coin = 3,
        Special = 4,
        Stomp = 5,
        Death = 6,
        Checkpoint = 7,
        StageClear = 8,
        Switch = 9,
        Gate = 10,
        Burrow = 11,
        BatDive = 12,
        SpikeUp = 13,
        BossRoar = 14,
        BossSlam = 15,
        BossCharge = 16,
        BossHit = 17,
        BossDefeat = 18,
        MonsterAlert = 19,
        MonsterSwing = 20,
        CutInNow = 21,
        TimeStopStart = 22,
        TimeStopTick = 23,
        TimeStopTock = 24,
    }

    /// <summary>All gameplay / UI sound effects with their mix settings (one asset, tuned in the Inspector).</summary>
    [CreateAssetMenu(menuName = "MyGame/Sfx Bank", fileName = "SfxBank")]
    public sealed class SfxBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public SfxId id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Tooltip("Random pitch offset (+/-) per play, so repeated sounds do not feel mechanical.")]
            [Range(0f, 0.3f)] public float pitchJitter = 0.05f;
            [Tooltip("Plays closer together than this are skipped (seconds, unscaled).")]
            [Min(0f)] public float minInterval = 0.05f;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public bool TryGet(SfxId id, out Entry entry)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i] != null && entries[i].id == id && entries[i].clip != null)
                {
                    entry = entries[i];
                    return true;
                }
            }
            entry = null;
            return false;
        }
    }
}
