using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Springy footing (layout blocks with surface "bounce"). The moment the player lands on a collider with this
    /// component, PlayerMotor launches them straight up to <see cref="bounceHeight"/>; steering in the air is unchanged.
    /// The cushion child (optional) is squashed and springs back as feedback.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BounceSurface : MonoBehaviour
    {
        [Tooltip("Height of the launch above the pad, in metres (a normal jump is 3.2).")]
        [Min(0.5f)] public float bounceHeight = 5.5f;
        [Tooltip("Visual squashed on a launch (optional).")]
        [SerializeField] private Transform cushion;
        [SerializeField, Min(0.01f)] private float squashSeconds = 0.25f;
        [SerializeField, Range(0.1f, 1f)] private float squashScale = 0.55f;

        private Vector3 cushionScale;
        private float squashTimer;

        private void Awake()
        {
            if (cushion != null)
                cushionScale = cushion.localScale;
            enabled = false;
        }

        /// <summary>Called by PlayerMotor when it launches the player from this pad.</summary>
        public void NotifyLaunch()
        {
            if (cushion == null)
                return;
            squashTimer = 0f;
            enabled = true;
        }

        // Damped spring: squashed at the launch, a small stretch past the rest height, settled after two squash times.
        private void Update()
        {
            squashTimer += Time.deltaTime;
            float t = squashTimer / squashSeconds;
            bool done = t >= 2f;
            float offset = done ? 0f : (1f - squashScale) * Mathf.Exp(-3f * t) * Mathf.Cos(t * Mathf.PI * 2.5f);
            var s = cushionScale;
            s.y *= 1f - offset;
            cushion.localScale = s;
            if (done)
                enabled = false;
        }
    }
}
