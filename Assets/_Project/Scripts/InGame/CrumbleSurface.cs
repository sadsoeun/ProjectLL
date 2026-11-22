using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Footing that gives way (layout blocks with surface "crumble"). Once the player stands on it, it shakes for
    /// <see cref="delay"/> seconds, then breaks apart and comes back after <see cref="respawn"/> seconds.
    /// With <see cref="chunks"/> assigned (the fractured slab model) the chunks rattle, a few drop early as a warning,
    /// then all of them fall and tumble away from the spot that was stepped on; before the block is solid again they
    /// pop back into place. Without chunks the renderers are simply hidden. Timing runs on the fixed step, so pause
    /// freezes it; the chunk motion is drawn from that same clock.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class CrumbleSurface : MonoBehaviour, IStageElement
    {
        private enum Phase { Solid, Shaking, Gone }

        [Tooltip("Seconds between the first step on the block and the collapse.")]
        [Min(0.05f)] public float delay = 0.7f;
        [Tooltip("Seconds until the block is back.")]
        [Min(0.1f)] public float respawn = 3f;
        [Tooltip("Shake amplitude in metres while it is about to fall.")]
        [SerializeField, Min(0f)] private float shake = 0.04f;
        [Tooltip("Visual root that is shaken before the fall (optional).")]
        [SerializeField] private Transform visual;

        [Header("Break-apart (optional)")]
        [Tooltip("Pieces of the fractured model. Each one falls on its own.")]
        [SerializeField] private Transform[] chunks;
        [Tooltip("Dust bursts played at the moment of the collapse.")]
        [SerializeField] private ParticleSystem[] dust;
        [Tooltip("Seconds over which the collapse spreads from the stepped spot to the far end.")]
        [SerializeField, Min(0f)] private float spread = 0.22f;
        [Tooltip("Share of the pieces that drop during the shake, as a warning.")]
        [SerializeField, Range(0f, 0.5f)] private float earlyShare = 0.18f;
        [Tooltip("Fall acceleration of the pieces (m/s^2).")]
        [SerializeField, Min(1f)] private float chunkGravity = 26f;
        [Tooltip("Seconds a piece is visible after it starts to fall; it shrinks away over the last part.")]
        [SerializeField, Min(0.2f)] private float fallSeconds = 1.0f;
        [Tooltip("Largest spin of a falling piece (degrees per second).")]
        [SerializeField, Min(0f)] private float spin = 300f;
        [Tooltip("Seconds before the block is solid again in which the pieces pop back into place.")]
        [SerializeField, Min(0.05f)] private float rebuildSeconds = 0.45f;

        private struct Chunk
        {
            public Vector3 pos, scale;
            public Quaternion rot;
            public Vector3 velocity, axis, jitter;
            public float spinSpeed, order, random;
            public bool early;
            public float fallAt; // seconds after the first step
        }

        private Collider solid;
        private Renderer[] renderers;
        private bool[] rendererWasOn;
        private Vector3 visualStart;
        private Chunk[] state;
        private Phase phase;
        private float timer;      // time in the current phase (fixed step)
        private float sinceStep;  // time since the first step (fixed step)
        private bool HasChunks => state != null && state.Length > 0;

        private void Awake()
        {
            solid = GetComponent<Collider>();
            renderers = GetComponentsInChildren<Renderer>(true);
            rendererWasOn = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                rendererWasOn[i] = renderers[i].enabled;
            if (visual != null)
                visualStart = visual.localPosition;

            int n = chunks != null ? chunks.Length : 0;
            state = new Chunk[n];
            var rng = new System.Random(GetInstanceID());
            float R() => (float)rng.NextDouble();
            for (int i = 0; i < n; i++)
            {
                var t = chunks[i];
                if (t == null)
                    continue;
                state[i] = new Chunk
                {
                    pos = t.localPosition, rot = t.localRotation, scale = t.localScale,
                    velocity = new Vector3(R() * 1.6f - 0.8f, R() * 1.6f, R() * 1.2f - 0.6f),
                    axis = new Vector3(R() - 0.5f, R() - 0.5f, R() - 0.5f).normalized,
                    jitter = new Vector3(R() * 6.28f, R() * 6.28f, R() * 6.28f),
                    spinSpeed = (0.35f + 0.65f * R()) * (R() < 0.5f ? -1f : 1f),
                    random = R(),
                    early = R() < earlyShare,
                };
            }
        }

        public void Bind(StageRoot stage) { }
        public void ResetForRespawn() => Restore();
        public void ResetForAttempt() => Restore();

        /// <summary>Called by PlayerMotor every physics step the player stands on this block.</summary>
        /// <param name="worldX">Where the player stands; the collapse spreads from there.</param>
        public void NotifyStoodOn(float worldX = float.NaN)
        {
            if (phase != Phase.Solid)
                return;
            phase = Phase.Shaking;
            timer = 0f;
            sinceStep = 0f;
            if (!HasChunks)
                return;
            if (float.IsNaN(worldX))
                worldX = transform.position.x;
            float far = 0.01f;
            for (int i = 0; i < state.Length; i++)
            {
                if (chunks[i] == null)
                    continue;
                state[i].order = Mathf.Abs(chunks[i].position.x - worldX);
                far = Mathf.Max(far, state[i].order);
            }
            for (int i = 0; i < state.Length; i++)
            {
                ref var c = ref state[i];
                c.fallAt = c.early
                    ? delay * (0.45f + 0.4f * c.random)                               // warning pieces, during the shake
                    : delay + spread * (c.order / far) + 0.07f * c.random;           // the rest, outward from the step
            }
        }

        private void Restore()
        {
            phase = Phase.Solid;
            timer = 0f;
            sinceStep = 0f;
            if (solid != null)
                solid.enabled = true;
            if (renderers != null)
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null)
                        renderers[i].enabled = rendererWasOn[i];
            if (visual != null)
                visual.localPosition = visualStart;
            if (state != null)
                for (int i = 0; i < state.Length; i++)
                    Place(i, state[i].pos, state[i].rot, 1f);
            if (dust != null)
                foreach (var d in dust)
                    if (d != null)
                        d.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Place(int i, Vector3 pos, Quaternion rot, float scale)
        {
            var t = chunks[i];
            if (t == null)
                return;
            bool show = scale > 0.001f;
            if (t.gameObject.activeSelf != show)
                t.gameObject.SetActive(show);
            if (!show)
                return;
            t.localPosition = pos;
            t.localRotation = rot;
            t.localScale = state[i].scale * scale;
        }

        private void FixedUpdate()
        {
            if (phase == Phase.Solid)
                return;
            timer += Time.fixedDeltaTime;
            sinceStep += Time.fixedDeltaTime;
            if (phase == Phase.Shaking)
            {
                if (timer >= delay)
                {
                    phase = Phase.Gone;
                    timer = 0f;
                    solid.enabled = false;
                    if (visual != null)
                        visual.localPosition = visualStart;
                    if (HasChunks)
                    {
                        if (dust != null)
                            foreach (var d in dust)
                                if (d != null)
                                    d.Play(true);
                    }
                    else
                    {
                        foreach (var r in renderers)
                            if (r != null)
                                r.enabled = false;
                    }
                }
            }
            else if (timer >= respawn)
            {
                Restore();
            }
        }

        private void LateUpdate()
        {
            if (phase == Phase.Solid)
                return;
            // draw between fixed steps so the motion stays smooth at any frame rate
            float lead = Mathf.Clamp(Time.time - Time.fixedTime, 0f, Time.fixedDeltaTime);
            float t = sinceStep + lead;

            if (phase == Phase.Shaking && visual != null)
            {
                float k = Mathf.Clamp01(t / delay); // shakes harder toward the end
                visual.localPosition = visualStart + new Vector3(Mathf.Sin(t * 61f), Mathf.Sin(t * 47f), 0f) * (shake * (0.4f + 0.6f * k));
            }
            if (!HasChunks)
                return;

            float rebuildAt = delay + respawn - rebuildSeconds;
            for (int i = 0; i < state.Length; i++)
            {
                ref var c = ref state[i];
                if (t >= rebuildAt)
                {
                    // pop back into place, left to right by the random order
                    float u = Mathf.Clamp01((t - rebuildAt - 0.45f * rebuildSeconds * c.random) / (0.55f * rebuildSeconds));
                    float s = u <= 0f ? 0f : 1f + 0.18f * Mathf.Sin(u * Mathf.PI) * (1f - u) - (1f - u) * (1f - u);
                    Place(i, c.pos - new Vector3(0f, 0.25f * (1f - u) * (1f - u), 0f), c.rot, Mathf.Max(0f, s));
                    continue;
                }
                float f = t - c.fallAt;
                if (f < 0f)
                {
                    // rattle in place, harder as the fall comes closer
                    float k = Mathf.Clamp01(t / Mathf.Max(0.01f, c.fallAt));
                    float a = 0.012f + 0.03f * k * k;
                    var offset = new Vector3(Mathf.Sin(t * 53f + c.jitter.x), Mathf.Sin(t * 67f + c.jitter.y), Mathf.Sin(t * 41f + c.jitter.z)) * a;
                    var tilt = Quaternion.Euler(Mathf.Sin(t * 37f + c.jitter.z) * 4f * k, 0f, Mathf.Sin(t * 43f + c.jitter.x) * 5f * k);
                    Place(i, c.pos + offset, c.rot * tilt, 1f);
                    continue;
                }
                if (f >= fallSeconds)
                {
                    Place(i, c.pos, c.rot, 0f);
                    continue;
                }
                var p = c.pos + c.velocity * f + new Vector3(0f, -0.5f * chunkGravity * f * f, 0f);
                var r = Quaternion.AngleAxis(spin * c.spinSpeed * f, c.axis) * c.rot;
                float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fallSeconds * 0.65f, fallSeconds, f));
                Place(i, p, r, fade);
            }
        }
    }
}
