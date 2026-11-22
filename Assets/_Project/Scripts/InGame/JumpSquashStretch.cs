using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Procedural squash and stretch for a character visual (volume-preserving, scales the visual root around its feet).
    /// Stretches on takeoff and enemy stomps, squashes on touchdown scaled by the landing impact, and springs back with a
    /// damped overshoot. Put it on the visual root next to the Animator (Leaf Explorer prefab); other characters simply do not have it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class JumpSquashStretch : MonoBehaviour
    {
        [SerializeField] private bool squashEnabled = true;
        [Header("Takeoff / stomp stretch")]
        [Tooltip("Vertical scale added at the moment of takeoff (0.12 = +12 %).")]
        [SerializeField, Range(0f, 0.4f)] private float takeoffStretch = 0.14f;
        [Header("Landing squash")]
        [Tooltip("Vertical scale removed at maximum landing impact.")]
        [SerializeField, Range(0f, 0.5f)] private float maxLandSquash = 0.20f;
        [Tooltip("Vertical scale removed for the lightest landing.")]
        [SerializeField, Range(0f, 0.5f)] private float minLandSquash = 0.07f;
        [Header("Spring")]
        [SerializeField, Min(1f)] private float springFrequency = 16f;
        [SerializeField, Range(0.1f, 1f)] private float springDamping = 0.38f;

        private PlayerAnimator source;
        private Vector3 baseScale;
        private float offset;   // current vertical deformation (+ stretch, - squash)
        private float velocity;

        private void Awake() => baseScale = transform.localScale;

        private void OnEnable()
        {
            baseScale = transform.localScale;
            source = GetComponentInParent<PlayerAnimator>();
            if (source == null)
                return;
            source.Takeoff += OnTakeoff;
            source.Landing += OnLanding;
        }

        private void OnDisable()
        {
            if (source != null)
            {
                source.Takeoff -= OnTakeoff;
                source.Landing -= OnLanding;
            }
            transform.localScale = baseScale;
            offset = velocity = 0f;
        }

        private void OnTakeoff()
        {
            if (!squashEnabled)
                return;
            offset = takeoffStretch;
            velocity = 0f;
        }

        private void OnLanding(float impact01)
        {
            if (!squashEnabled)
                return;
            offset = -Mathf.Lerp(minLandSquash, maxLandSquash, Mathf.Clamp01(impact01));
            velocity = 0f;
        }

        private void LateUpdate()
        {
            if (!squashEnabled)
                return;
            float dt = TimeStop.IsActive || Time.timeScale <= 0f ? Time.unscaledDeltaTime : Time.deltaTime;
            dt = Mathf.Min(dt, 0.05f);
            // damped spring toward 0
            float w = springFrequency;
            float accel = -w * w * offset - 2f * springDamping * w * velocity;
            velocity += accel * dt;
            offset += velocity * dt;
            float y = 1f + offset;
            float xz = 1f / Mathf.Sqrt(Mathf.Max(0.2f, y)); // volume preserving
            transform.localScale = new Vector3(baseScale.x * xz, baseScale.y * y, baseScale.z * xz);
        }
    }
}
