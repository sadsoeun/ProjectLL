using System;
using System.Collections.Generic;

namespace MyGame
{
    /// <summary>
    /// Progress data persisted to save_01.json (spec 6). JsonUtility cannot serialise dictionaries,
    /// so stage records are stored as a list.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public string createdAt;
        public string updatedAt;
        public string currentNodeId;
        public int lives;
        public int totalCoins;
        public List<StageRecord> stages = new List<StageRecord>();
        /// <summary>Tutorial prompts already shown. Missing in pre-tutorial saves (see TutorialProgress.EvaluateLegacy).</summary>
        public TutorialProgress tutorial = new TutorialProgress();
        /// <summary>Voice Studio purchases and switches. Missing in older saves (see <see cref="Voice"/>).</summary>
        public VoiceProgress voice = new VoiceProgress();

        /// <summary>Voice Studio state, created and sized on first access for saves that predate the field.</summary>
        public VoiceProgress Voice
        {
            get
            {
                if (voice == null)
                    voice = new VoiceProgress();
                voice.EnsureSize();
                return voice;
            }
        }

        /// <summary>Golden leaves collected over all stages (opens the Voice Studio slots).</summary>
        public int TotalLeaves()
        {
            int n = 0;
            if (stages != null)
                for (int i = 0; i < stages.Count; i++)
                    if (stages[i] != null)
                        n += stages[i].CollectedCount();
            return n;
        }

        public static SaveData CreateNew(string startNodeId, int initialLives)
        {
            string now = DateTime.UtcNow.ToString("o");
            return new SaveData
            {
                version = CurrentVersion,
                createdAt = now,
                updatedAt = now,
                currentNodeId = startNodeId,
                lives = initialLives,
                totalCoins = 0,
                stages = new List<StageRecord>(),
                tutorial = TutorialProgress.CreateFresh(),
            };
        }

        /// <summary>Structural validation of loaded data. Does not throw.</summary>
        public bool IsValid()
        {
            return version >= 1
                && version <= CurrentVersion
                && !string.IsNullOrEmpty(currentNodeId)
                && lives >= 0
                && totalCoins >= 0
                && stages != null;
        }

        /// <summary>Tutorial state, created and judged on first access for saves that predate the field.</summary>
        public TutorialProgress Tutorial
        {
            get
            {
                if (tutorial == null)
                    tutorial = new TutorialProgress();
                tutorial.EvaluateLegacy(this);
                return tutorial;
            }
        }

        public StageRecord FindStage(string stageId)
        {
            if (stages == null || string.IsNullOrEmpty(stageId))
                return null;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] != null && stages[i].stageId == stageId)
                    return stages[i];
            }
            return null;
        }

        public bool IsCleared(string stageId)
        {
            var r = FindStage(stageId);
            return r != null && r.cleared;
        }

        public void RecordClear(string stageId, float clearTime)
        {
            if (string.IsNullOrEmpty(stageId))
                throw new ArgumentException("stageId is empty", nameof(stageId));
            var r = FindStage(stageId);
            if (r == null)
            {
                r = new StageRecord { stageId = stageId };
                stages.Add(r);
            }
            r.bestTime = (r.cleared && r.bestTime > 0f) ? Math.Min(r.bestTime, clearTime) : clearTime;
            r.cleared = true;
            updatedAt = DateTime.UtcNow.ToString("o");
        }

        /// <summary>
        /// Records a clear and merges the attempt result: specials are OR-merged into the stage record
        /// (record slots only ever turn on) and the attempt coins are added to totalCoins.
        /// </summary>
        public void RecordClear(string stageId, float clearTime, bool[] collectedSpecials, int coins)
        {
            RecordClear(stageId, clearTime);
            var r = FindStage(stageId);
            r.MergeCollectibles(collectedSpecials);
            if (coins > 0)
                totalCoins = (int)Math.Min((long)totalCoins + coins, int.MaxValue);
        }
    }

    [Serializable]
    public sealed class StageRecord
    {
        public string stageId;
        public bool cleared;
        public float bestTime;
        public bool[] collectibles = new bool[0];

        public bool IsCollected(int slot) => collectibles != null && slot >= 0 && slot < collectibles.Length && collectibles[slot];

        public int CollectedCount()
        {
            int n = 0;
            if (collectibles != null)
                for (int i = 0; i < collectibles.Length; i++)
                    if (collectibles[i]) n++;
            return n;
        }

        /// <summary>OR-merges the given slots; grows the array if the record came from an older save.</summary>
        public void MergeCollectibles(bool[] collected)
        {
            if (collected == null)
                return;
            if (collectibles == null || collectibles.Length < collected.Length)
            {
                var grown = new bool[collected.Length];
                if (collectibles != null)
                    Array.Copy(collectibles, grown, collectibles.Length);
                collectibles = grown;
            }
            for (int i = 0; i < collected.Length; i++)
                collectibles[i] |= collected[i];
        }
    }
}
