using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// First-time master clear on the world map: after a short delay a golden burst plays at the node pad, the pad
    /// blends from cleared colours to master gold, and the "Master clear!" banner shows. Finished() fires once at the
    /// end (or when skipped) so the next-stage unlock presentation can follow without overlapping.
    /// Unscaled time; the timeline freezes while Suspended (loading fade, popups, pause).
    /// </summary>
    public sealed class WorldMapMasterPresentation : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Settings
        {
            [Tooltip("Wait after the map appears before the burst (s).")]
            [Min(0f)] public float startDelay = 0.6f;
            [Tooltip("Pad colour blend from cleared to master gold (s).")]
            [Min(0.05f)] public float padRevealSeconds = 1.1f;
            [Tooltip("Halo burst after the reveal, like the unlock attention (s).")]
            [Min(0f)] public float attentionSeconds = 3f;
            [Tooltip("Banner hold time before it fades out (s).")]
            [Min(0.2f)] public float bannerSeconds = 2.2f;
            [Tooltip("Burst height above the node anchor (m).")]
            public float burstHeight = 0.25f;
            [Tooltip("Burst size multiplier.")]
            [Min(0.1f)] public float burstScale = 1f;
            [Tooltip("Confirm / cancel skips the banner after this many seconds.")]
            [Min(0f)] public float skipAfter = 0.8f;
        }

        private Settings settings = new Settings();
        private WorldMapNode node;
        private MasterClearToast toast;
        private GameObject burst;
        private System.Action finished;
        private float elapsed, burstAge;
        private bool revealed;

        public bool IsPlaying { get; private set; }
        public bool Suspended { get; set; }
        public bool CanSkip => IsPlaying && !Suspended && revealed && elapsed >= settings.startDelay + settings.skipAfter;

        public static WorldMapMasterPresentation Create(Transform owner, Transform canvas)
        {
            var p = owner.gameObject.AddComponent<WorldMapMasterPresentation>();
            p.toast = MasterClearToast.Create(canvas);
            return p;
        }

        public void Play(WorldMapNode target, Settings config, System.Action onFinished)
        {
            settings = config ?? new Settings();
            node = target;
            finished = onFinished;
            elapsed = 0f;
            revealed = false;
            IsPlaying = true;
            node.PrepareMasterReveal(); // still shows cleared colours until the burst
        }

        public void Skip()
        {
            if (!CanSkip) return;
            node.FinishMasterReveal();
            toast.Dismiss();
        }

        private void Update()
        {
            if (burst != null)
            {
                burstAge += Time.unscaledDeltaTime;
                if (burstAge >= MasterBurstFx.Lifetime) { Destroy(burst); burst = null; }
            }
            if (!IsPlaying) return;
            toast.Suspended = Suspended;
            if (Suspended) return;
            elapsed += Time.unscaledDeltaTime;
            if (!revealed)
            {
                if (elapsed < settings.startDelay) return;
                revealed = true;
                burst = MasterBurstFx.Spawn(node.Anchor + Vector3.up * settings.burstHeight, settings.burstScale);
                burstAge = 0f;
                node.PlayMasterReveal(settings.padRevealSeconds, settings.attentionSeconds);
                toast.Show(node.Data, settings.bannerSeconds);
                return;
            }
            if (!toast.IsPlaying) Finish();
        }

        private void Finish()
        {
            IsPlaying = false;
            node.FinishMasterReveal();
            var callback = finished;
            finished = null;
            callback?.Invoke();
        }

        private void OnDestroy()
        {
            if (burst != null) Destroy(burst);
        }
    }
}
