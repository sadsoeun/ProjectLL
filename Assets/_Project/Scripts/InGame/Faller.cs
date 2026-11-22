using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Falling hazard hanging at its start position. When the player is within triggerHalfWidth horizontally and
    /// below it, it shakes for `delay` seconds, then drops (accelerating) until it hits the ground, lingers briefly,
    /// disappears and stays gone until the player dies or retries (or, with respawnOnlyOnDeath off, reappears after
    /// `respawn` seconds). Kills on contact (HazardContact on the same collider).
    /// All timing runs on the fixed step, so pause freezes it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class Faller : MonoBehaviour, IStageElement
    {
        private enum Phase { Hanging, Armed, Falling, Landed, Gone }

        [SerializeField, Min(0.1f)] private float triggerHalfWidth = 1.5f;
        [SerializeField, Min(0f)] private float delay = 0.4f;
        [Tooltip("Once fallen it stays gone until the player dies or retries. Off: it comes back after `respawn` seconds.")]
        [SerializeField] private bool respawnOnlyOnDeath = true;
        [Tooltip("Seconds until it reappears (only when Respawn Only On Death is off).")]
        [SerializeField, Min(0f)] private float respawn = 3f;
        [SerializeField, Min(0.1f)] private float halfHeight = 0.7f;
        [SerializeField, Min(0.1f)] private float gravity = 30f;
        [SerializeField, Min(1f)] private float maxFallSpeed = 30f;
        [SerializeField, Min(0f)] private float landedLinger = 0.3f;
        [Tooltip("Visual child shaken while armed.")]
        [SerializeField] private Transform visual;

        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

        private StageRoot stage;
        private Rigidbody body;
        private Collider hitBox;
        private Vector3 start;
        private Vector3 visualStart;
        private Phase phase;
        private float timer;
        private float fallSpeed;
        private float landY;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            hitBox = GetComponent<Collider>();
            start = transform.position;
            if (visual != null)
                visualStart = visual.localPosition;
        }

        public void Bind(StageRoot owner) => stage = owner;
        public void ResetForRespawn() => ResetToHanging();
        public void ResetForAttempt() => ResetToHanging();

        private void ResetToHanging()
        {
            phase = Phase.Hanging;
            timer = 0f;
            fallSpeed = 0f;
            gameObject.SetActive(true);
            hitBox.enabled = true;
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.localPosition = visualStart;
            }
            transform.position = start;
            body.position = start;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            switch (phase)
            {
                case Phase.Hanging:
                    if (PlayerUnder())
                    {
                        phase = Phase.Armed;
                        timer = 0f;
                    }
                    break;

                case Phase.Armed:
                    timer += dt;
                    if (visual != null)
                        visual.localPosition = visualStart + Vector3.right * (Mathf.Sin(timer * 70f) * 0.06f);
                    if (timer >= delay)
                    {
                        if (visual != null)
                            visual.localPosition = visualStart;
                        landY = FindLandingY();
                        fallSpeed = 0f;
                        phase = Phase.Falling;
                    }
                    break;

                case Phase.Falling:
                    fallSpeed = Mathf.Min(fallSpeed + gravity * dt, maxFallSpeed);
                    float y = body.position.y - fallSpeed * dt;
                    if (y <= landY)
                    {
                        y = landY;
                        phase = Phase.Landed;
                        timer = 0f;
                    }
                    body.MovePosition(new Vector3(start.x, y, start.z));
                    break;

                case Phase.Landed:
                    timer += dt;
                    if (timer >= landedLinger)
                    {
                        Vanish();
                        phase = Phase.Gone;
                        timer = 0f;
                    }
                    break;

                case Phase.Gone:
                    if (respawnOnlyOnDeath)
                        break; // back only through ResetForRespawn / ResetForAttempt
                    timer += dt;
                    if (timer >= respawn)
                        ResetToHanging();
                    break;
            }
        }

        private bool PlayerUnder()
        {
            if (stage == null || stage.Player == null)
                return false;
            Vector3 p = stage.Player.position;
            return Mathf.Abs(p.x - start.x) <= triggerHalfWidth && p.y < start.y - halfHeight;
        }

        /// <summary>Y of the block centre when it rests on the first solid thing below (the player is ignored).</summary>
        private float FindLandingY()
        {
            float fallback = (stage != null ? stage.KillY : start.y - 50f) - 5f;
            int n = Physics.RaycastNonAlloc(new Ray(start, Vector3.down), HitBuffer, 200f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < n; i++)
            {
                var h = HitBuffer[i];
                if (h.collider == hitBox || PlayerMotor.TryGetFromCollider(h.collider, out _))
                    continue;
                if (h.distance < best)
                {
                    best = h.distance;
                    found = true;
                }
            }
            return found ? start.y - best + halfHeight : fallback;
        }

        private void Vanish()
        {
            hitBox.enabled = false;
            if (visual != null)
                visual.gameObject.SetActive(false);
        }
    }
}
