using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Timed spike trap. One cycle of `period` seconds is: down (safe) -> warn (spikes peek out and shake, still safe)
    /// -> up (kills) and then down again. The cycle is derived from an accumulated fixed-step clock (+ offset), so it is
    /// deterministic, freezes with Time.timeScale = 0 and restarts from zero on respawn / retry.
    /// The spike visuals (`risers`) are moved along local Y: 0 = hidden, `riseHeight` = fully up.
    /// Lethal only while up: a trigger box reports the player while the phase is Up (Enter and Stay).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SpikeTrap : MonoBehaviour, IStageElement
    {
        public enum Phase { Down, Warn, Up }

        [Tooltip("Length of one full cycle in seconds.")]
        [SerializeField, Min(0.5f)] private float period = 3f;
        [Tooltip("Seconds the spikes stay up (lethal).")]
        [SerializeField, Min(0.1f)] private float upTime = 1f;
        [Tooltip("Seconds of warning before the spikes come up (peek + shake, not lethal).")]
        [SerializeField, Min(0.1f)] private float warn = 0.5f;
        [Tooltip("Shifts the cycle in time so neighbouring traps do not fire together.")]
        [SerializeField] private float offset;
        [Tooltip("Spike transforms that move up. Their rest position is their start local position.")]
        [SerializeField] private Transform[] risers;
        [Tooltip("Local Y distance from hidden to fully up.")]
        [SerializeField, Min(0.05f)] private float riseHeight = 0.58f;
        [Tooltip("Fraction of riseHeight shown during the warning.")]
        [SerializeField, Range(0f, 0.6f)] private float warnPeek = 0.2f;
        [SerializeField, Min(0f)] private float warnShake = 0.025f;

        private StageRoot stage;
        private Vector3[] restLocal;
        private float clock;
        private Phase phase = Phase.Down;

        public Phase CurrentPhase => phase;
        public float Period => period;

        private void Awake()
        {
            CacheRest();
            ApplyVisual(Phase.Down, 0f);
        }

        private void CacheRest()
        {
            if (restLocal != null || risers == null)
                return;
            restLocal = new Vector3[risers.Length];
            for (int i = 0; i < risers.Length; i++)
                restLocal[i] = risers[i] != null ? risers[i].localPosition : Vector3.zero;
        }

        public void Bind(StageRoot owner) => stage = owner;
        public void ResetForRespawn() => ResetClock();
        public void ResetForAttempt() => ResetClock();

        private void ResetClock()
        {
            clock = 0f;
            CacheRest();
            ApplyVisual(Phase.Down, 0f);
        }

        /// <summary>Phase at stage time <paramref name="t"/> (seconds since the clock reset) with the trap's offset.</summary>
        public Phase PhaseAt(float t)
        {
            float cycle = Mathf.Max(0.5f, period);
            float up = Mathf.Min(upTime, cycle - 0.1f);
            float w = Mathf.Min(warn, Mathf.Max(0f, cycle - up));
            float m = Mathf.Repeat(t + offset, cycle);
            float upStart = cycle - up;
            if (m >= upStart)
                return Phase.Up;
            if (m >= upStart - w)
                return Phase.Warn;
            return Phase.Down;
        }

        private void FixedUpdate()
        {
            clock += Time.fixedDeltaTime;
            var previous = phase;
            phase = PhaseAt(clock);
            if (phase == Phase.Up && previous != Phase.Up && PlayerNearby())
                Sfx.Play(SfxId.SpikeUp);
            ApplyVisual(phase, clock);
        }

        // Many traps share the stage: only the ones near the player are heard.
        private bool PlayerNearby() => stage != null && stage.Player != null && Mathf.Abs(stage.Player.position.x - transform.position.x) < 10f;

        private void ApplyVisual(Phase p, float t)
        {
            phase = p;
            if (risers == null || restLocal == null)
                return;
            float h = p == Phase.Up ? 1f : p == Phase.Warn ? warnPeek : 0f;
            float shake = p == Phase.Warn ? Mathf.Sin(t * 80f) * warnShake : 0f;
            for (int i = 0; i < risers.Length; i++)
            {
                if (risers[i] != null)
                    risers[i].localPosition = restLocal[i] + new Vector3(shake, riseHeight * h, 0f);
            }
        }

        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);

        private void Hit(Collider other)
        {
            if (phase == Phase.Up && stage != null && PlayerMotor.TryGetFromCollider(other, out _))
                stage.NotifyPlayerHit();
        }
    }
}
