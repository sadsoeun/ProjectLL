using System;

namespace MyGame
{
    /// <summary>
    /// Voice Studio state stored in the save (indexed by <see cref="VoiceSlot"/>). Whether a slot is open is not stored:
    /// it follows the golden-leaf total. The recordings themselves are WAV files on this machine (VoiceLibrary).
    /// </summary>
    [Serializable]
    public sealed class VoiceProgress
    {
        /// <summary>Extra takes bought with coins, per slot (take 1 comes with the slot).</summary>
        public int[] extraTakes = new int[VoiceStudioConfig.SlotCount];
        /// <summary>The player switched the recordings of the slot off (the built-in sound plays).</summary>
        public bool[] disabled = new bool[VoiceStudioConfig.SlotCount];
        /// <summary>The "new slot" notice of the slot was shown.</summary>
        public bool[] announced = new bool[VoiceStudioConfig.SlotCount];

        /// <summary>Repairs arrays of saves written by an older version (or edited by hand).</summary>
        public void EnsureSize()
        {
            extraTakes = Fit(extraTakes);
            disabled = Fit(disabled);
            announced = Fit(announced);
        }

        private static T[] Fit<T>(T[] array)
        {
            if (array != null && array.Length >= VoiceStudioConfig.SlotCount)
                return array;
            var grown = new T[VoiceStudioConfig.SlotCount];
            if (array != null)
                Array.Copy(array, grown, array.Length);
            return grown;
        }

        /// <summary>Takes the player owns for the slot (1 = only the free one).</summary>
        public int OwnedTakes(VoiceSlot slot, int maxTakes) => Math.Min(maxTakes, 1 + Math.Max(0, extraTakes[(int)slot]));
    }
}
