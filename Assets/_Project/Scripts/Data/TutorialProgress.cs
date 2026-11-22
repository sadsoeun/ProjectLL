using System;
using System.Collections.Generic;

namespace MyGame
{
    /// <summary>
    /// Which tutorial prompts this save has already shown (persisted inside save_01.json as SaveData.tutorial).
    /// Backward compatible: a save written before this field existed loads with <see cref="evaluated"/> = false and is
    /// judged once by <see cref="EvaluateLegacy"/>. Seeing a prompt never grants clears, collectibles or unlocks.
    /// </summary>
    [Serializable]
    public sealed class TutorialProgress
    {
        /// <summary>Stage whose clear marks a pre-tutorial save as experienced (all prompts treated as seen).</summary>
        public const string LegacyClearedStageId = "W1_S1";

        /// <summary>False only for saves written before the tutorial existed (field missing in the JSON).</summary>
        public bool evaluated;
        /// <summary>True when an old save had already cleared <see cref="LegacyClearedStageId"/>: no prompts are shown.</summary>
        public bool skipped;
        /// <summary>Ids of prompts that have completed (performed or passed).</summary>
        public List<string> seen = new List<string>();
        /// <summary>Version of the "steps added later" rule already applied to this save (see <see cref="ApplyAddedSteps"/>).</summary>
        public int rules;

        public const int CurrentRules = 2;
        /// <summary>Steps added after the first tutorial release, per stage (rules version 2: the W1_B boss lessons).</summary>
        private static readonly (string stageId, string[] ids)[] AddedSteps =
        {
            ("W1_B", new[] { "w1b_boss", "w1b_stun", "w1b_rain" }),
        };

        public static TutorialProgress CreateFresh() => new TutorialProgress { evaluated = true, rules = CurrentRules };

        public int SeenCount => seen != null ? seen.Count : 0;

        public bool IsSeen(string id)
        {
            if (skipped)
                return true;
            return seen != null && !string.IsNullOrEmpty(id) && seen.Contains(id);
        }

        /// <summary>Marks the prompt as seen. Returns true if it was not seen before.</summary>
        public bool MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id) || IsSeen(id))
                return false;
            if (seen == null)
                seen = new List<string>();
            seen.Add(id);
            return true;
        }

        /// <summary>Player chose "skip tutorial": every prompt counts as seen. Grants nothing else.</summary>
        public void SkipAll()
        {
            evaluated = true;
            skipped = true;
        }

        /// <summary>Dev cheat: every prompt shows again (regardless of stage clears).</summary>
        public void ResetAll()
        {
            evaluated = true;
            skipped = false;
            seen = new List<string>();
            rules = CurrentRules; // the one-time rules are not applied again: every prompt really shows
        }

        /// <summary>
        /// One-time rule for saves that predate the tutorial: if the first stage is already cleared the player is not new,
        /// so every prompt is treated as seen; otherwise the tutorial runs normally.
        /// </summary>
        public void EvaluateLegacy(SaveData owner)
        {
            if (!evaluated)
            {
                evaluated = true;
                skipped = owner != null && owner.IsCleared(LegacyClearedStageId);
                if (seen == null)
                    seen = new List<string>();
            }
            ApplyAddedSteps(owner);
        }

        /// <summary>
        /// One-time rule for saves written before a lesson existed: if its stage is already cleared the player has done it
        /// without the lesson, so it is marked seen. A stage cleared later never flips it, and the dev reset shows it again.
        /// </summary>
        private void ApplyAddedSteps(SaveData owner)
        {
            if (rules >= CurrentRules)
                return;
            rules = CurrentRules;
            if (owner == null || skipped)
                return;
            foreach (var (stageId, ids) in AddedSteps)
                if (owner.IsCleared(stageId))
                    foreach (var id in ids)
                        MarkSeen(id);
        }
    }
}
