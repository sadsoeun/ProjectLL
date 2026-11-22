using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyGame
{
    /// <summary>
    /// Perfect-dodge time stop: the world freezes (Time.timeScale = 0) while the player keeps moving, the screen turns
    /// black-and-white and a clock ticks. Only the player is simulated: physics runs in script mode, stepped here on
    /// unscaled time together with <see cref="PlayerMotor.ManualStep"/>; every other dynamic body is held kinematic.
    /// Built in code like <see cref="NowCutIn"/>. Only one runs at a time; pause is refused while it runs.
    /// Everything it changes is restored in <see cref="Finish"/>, which is safe to call from physics callbacks.
    /// </summary>
    public sealed class TimeStop : MonoBehaviour
    {
        public const float DefaultDuration = 2f;

        private const float FadeIn = 0.12f;     // grayscale ramp in (seconds)
        private const float FadeOut = 0.3f;     // ramp back to color at the end of the stop
        private const float FirstTick = 0.15f;
        private const float TickInterval = 0.5f;
        private const float MaxFrameStep = 0.05f; // a hitch never simulates more than this at once

        private static TimeStop current;
        public static bool IsActive => current != null;

        private PlayerMotor player;
        private float duration;
        private float elapsed;
        private float accumulator;
        private float nextTick;
        private int tickCount;
        private bool finished;

        private float savedTimeScale = 1f;
        private SimulationMode savedSimulationMode;

        private struct FrozenBody { public Rigidbody body; public Vector3 velocity, angularVelocity; }
        private readonly List<FrozenBody> frozenBodies = new List<FrozenBody>();
        private readonly List<(Animator animator, AnimatorUpdateMode mode)> playerAnimators = new List<(Animator, AnimatorUpdateMode)>();

        private UniversalAdditionalCameraData cameraData;
        private bool savedPostProcessing;
        private Volume volume;
        private VolumeProfile profile;

        /// <summary>Starts a time stop for the player. Returns false when one is running, a cut-in owns time, or time is already stopped.</summary>
        public static bool Play(PlayerMotor player, float duration = DefaultDuration)
        {
            if (current != null || player == null || NowCutIn.IsPlaying || Time.timeScale == 0f)
                return false;
            var go = new GameObject("TimeStop");
            current = go.AddComponent<TimeStop>();
            current.Begin(player, duration);
            return true;
        }

        /// <summary>Ends a running time stop immediately (death, goal, cut-in).</summary>
        public static void Cancel()
        {
            if (current != null)
                current.Finish();
        }

        private void Begin(PlayerMotor target, float seconds)
        {
            player = target;
            duration = Mathf.Max(0.1f, seconds);
            nextTick = FirstTick;

            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            savedSimulationMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            var playerBody = player.GetComponent<Rigidbody>();
            foreach (var rb in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (rb == playerBody || rb.isKinematic)
                    continue;
                frozenBodies.Add(new FrozenBody { body = rb, velocity = rb.linearVelocity, angularVelocity = rb.angularVelocity });
                rb.isKinematic = true;
            }

            player.SetManualStepping(true);
            foreach (var animator in player.GetComponentsInChildren<Animator>(true))
            {
                playerAnimators.Add((animator, animator.updateMode));
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            BuildGrayscale();
            Sfx.Play(SfxId.TimeStopStart);
            if (GameSystems.Exists)
                GameSystems.Instance.Audio.DuckBgm(0.35f, 0.1f);
        }

        // ---------------------------------------------------------------- grayscale

        private void BuildGrayscale()
        {
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out cameraData))
            {
                savedPostProcessing = cameraData.renderPostProcessing;
                cameraData.renderPostProcessing = true;
            }

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "TimeStopProfile";
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(-100f);
            color.contrast.Override(18f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(Color.black);
            // The camera normally renders without post-processing: neutralize what the pipeline's default profile adds.
            profile.Add<Bloom>(true).intensity.Override(0f);
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.None);

            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = profile;
            volume.weight = 0f;
        }

        private void ApplyWeight()
        {
            if (volume == null)
                return;
            float w = Mathf.Min(Mathf.Clamp01(elapsed / FadeIn), Mathf.Clamp01((duration - elapsed) / FadeOut));
            volume.weight = w;
        }

        // ---------------------------------------------------------------- loop

        private void Update()
        {
            if (finished)
                return;
            if (player == null || !player.isActiveAndEnabled)
            {
                Finish();
                return;
            }

            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
            elapsed += dt;
            accumulator += dt;
            float step = Time.fixedDeltaTime;
            while (accumulator >= step)
            {
                accumulator -= step;
                player.ManualStep(step);
                Physics.Simulate(step); // trigger callbacks run here and may end the stop (death, goal)
                if (finished)
                    return;
            }

            while (elapsed >= nextTick && nextTick < duration)
            {
                Sfx.Play(tickCount % 2 == 0 ? SfxId.TimeStopTick : SfxId.TimeStopTock);
                tickCount++;
                nextTick += TickInterval;
            }

            ApplyWeight();
            if (elapsed >= duration)
                Finish();
        }

        // ---------------------------------------------------------------- end

        private void Finish()
        {
            if (finished)
                return;
            finished = true;
            if (current == this)
                current = null;

            Physics.simulationMode = savedSimulationMode;
            if (Time.timeScale == 0f)
                Time.timeScale = savedTimeScale > 0f ? savedTimeScale : 1f; // pause is refused while this runs

            foreach (var f in frozenBodies)
            {
                if (f.body == null)
                    continue;
                f.body.isKinematic = false;
                f.body.linearVelocity = f.velocity;
                f.body.angularVelocity = f.angularVelocity;
            }
            frozenBodies.Clear();

            if (player != null)
                player.SetManualStepping(false);
            foreach (var (animator, mode) in playerAnimators)
            {
                if (animator != null)
                    animator.updateMode = mode;
            }
            playerAnimators.Clear();

            if (cameraData != null)
                cameraData.renderPostProcessing = savedPostProcessing;
            if (volume != null)
                volume.weight = 0f;
            if (GameSystems.Exists && !GameSystems.Instance.Loader.IsBusy)
                GameSystems.Instance.Audio.DuckBgm(1f, 0.3f);

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            Finish(); // scene unload while running
            if (profile != null)
                Destroy(profile);
        }
    }
}
