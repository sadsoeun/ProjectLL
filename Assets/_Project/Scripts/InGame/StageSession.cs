namespace MyGame
{
    public enum StageState
    {
        Loading,
        Ready,
        Playing,
        Paused,
        Dead,
        Clear,
        Result,
        GameOver,
    }

    /// <summary>
    /// Pure-C# model of one stage attempt (spec 5.6 state diagram): state, lives, elapsed time.
    /// No Unity dependencies, so it can be unit-tested.
    /// </summary>
    public sealed class StageSession
    {
        public StageState State { get; private set; } = StageState.Loading;
        public int Lives { get; private set; }
        public float Elapsed { get; private set; }

        public const int SpecialSlotCount = 3;

        private readonly bool[] specials = new bool[SpecialSlotCount];

        /// <summary>Coins collected in this attempt (kept across deaths, cleared by Retry).</summary>
        public int Coins { get; private set; }

        /// <summary>Id of the active checkpoint, or null when the spawn point is used.</summary>
        public string CheckpointId { get; private set; }
        public float CheckpointX { get; private set; }

        public StageSession(int lives)
        {
            if (lives < 1)
                throw new System.ArgumentOutOfRangeException(nameof(lives), "At least one life is required.");
            Lives = lives;
        }

        public static bool CanTransition(StageState from, StageState to)
        {
            switch (from)
            {
                case StageState.Loading: return to == StageState.Ready;
                case StageState.Ready: return to == StageState.Playing;
                case StageState.Playing: return to == StageState.Paused || to == StageState.Dead || to == StageState.Clear;
                case StageState.Paused: return to == StageState.Playing || to == StageState.Ready;
                case StageState.Dead: return to == StageState.Ready || to == StageState.GameOver;
                case StageState.Clear: return to == StageState.Result;
                default: return false; // Result and GameOver are terminal
            }
        }

        public bool TryTransition(StageState next)
        {
            if (!CanTransition(State, next))
                return false;
            State = next;
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (State == StageState.Playing && deltaTime > 0f)
                Elapsed += deltaTime;
        }

        /// <summary>Consumes a life. Returns true if lives remain.</summary>
        public bool ConsumeLife()
        {
            if (Lives > 0)
                Lives--;
            return Lives > 0;
        }

        public void AddCoin() => Coins++;

        /// <summary>Marks a special collectible. Returns true if it was newly collected; false for an invalid or repeated slot.</summary>
        public bool CollectSpecial(int slot)
        {
            if (slot < 0 || slot >= SpecialSlotCount || specials[slot])
                return false;
            specials[slot] = true;
            return true;
        }

        public bool IsSpecialCollected(int slot) => slot >= 0 && slot < SpecialSlotCount && specials[slot];

        public bool[] CopySpecials() => (bool[])specials.Clone();

        /// <summary>Makes the checkpoint the respawn point unless the current one is further along the stage.</summary>
        public bool TryActivateCheckpoint(string id, float x)
        {
            if (string.IsNullOrEmpty(id) || (CheckpointId != null && x <= CheckpointX))
                return false;
            CheckpointId = id;
            CheckpointX = x;
            return true;
        }

        /// <summary>Retry from the pause menu: timer, lives, coins, specials and checkpoint are all reset.</summary>
        public void ResetForRetry(int lives)
        {
            if (lives < 1)
                throw new System.ArgumentOutOfRangeException(nameof(lives), "At least one life is required.");
            Lives = lives;
            Elapsed = 0f;
            Coins = 0;
            System.Array.Clear(specials, 0, specials.Length);
            CheckpointId = null;
            CheckpointX = 0f;
        }
    }
}
