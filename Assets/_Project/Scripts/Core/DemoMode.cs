using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Developer-only switches for the automated showcase reel (Tools/MyGame/Demo) and editor tests. Everything is off by
    /// default and nothing in normal play turns it on. Scripted input replaces the device input for gameplay while
    /// <see cref="ScriptedInput"/> is true (see InputModeService); <see cref="Invincible"/> makes deaths no-ops.
    /// </summary>
    public static class DemoMode
    {
        public static bool Invincible;
        public static bool ScriptedInput;
        /// <summary>Tutorial hints stay on under scripted input (the story trailer shoots them); off by default.</summary>
        public static bool ShowTutorials;
        /// <summary>Scripted input hides tutorial hints, unless the shoot asked for them.</summary>
        public static bool HidesTutorials => ScriptedInput && !ShowTutorials;
        private static int confirmFrame = -1;
        /// <summary>Confirm counts as pressed during the next frame (read by any number of consumers).</summary>
        public static void PressConfirm() => confirmFrame = Time.frameCount + 1;
        public static bool ConfirmPressed => ScriptedInput && Time.frameCount == confirmFrame;

        public static float MoveX;
        public static bool JumpHeld;
        public static bool RunHeld;
        private static bool jumpQueued;
        private static bool dodgeQueued;

        /// <summary>Press (and hold, until <see cref="ReleaseJump"/>) jump.</summary>
        public static void PressJump() { jumpQueued = true; JumpHeld = true; }
        public static void ReleaseJump() => JumpHeld = false;
        public static void PressDodge() => dodgeQueued = true;

        /// <summary>One-shot presses are consumed by the single gameplay reader (PlayerInputReader).</summary>
        public static bool ConsumeJump() { bool v = jumpQueued; jumpQueued = false; return v; }
        public static bool ConsumeDodge() { bool v = dodgeQueued; dodgeQueued = false; return v; }

        public static void Reset()
        {
            Invincible = false;
            ScriptedInput = false;
            ShowTutorials = false;
            confirmFrame = -1;
            MoveX = 0f;
            JumpHeld = RunHeld = jumpQueued = dodgeQueued = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => Reset();
    }
}
