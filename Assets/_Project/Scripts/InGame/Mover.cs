using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Kinematic ping-pong platform (axis X = horizontal mover, Y = vertical lift).
    /// Cycle: wait at start, move +distance, wait, move back. Driven by an accumulated fixed-step clock, so it
    /// freezes with Time.timeScale = 0 and its Velocity is exactly what the physics step will move it by.
    /// It runs before PlayerMotor (execution order) so the player reads the velocity of the current step.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Mover : MonoBehaviour, IStageElement, IPlatformMotion
    {
        public enum MoveAxis { X, Y }

        [SerializeField] private MoveAxis axis = MoveAxis.X;
        [SerializeField] private float distance = 4f;
        [SerializeField, Min(0.01f)] private float speed = 2f;
        [Tooltip("Seconds to wait at each end.")]
        [SerializeField, Min(0f)] private float wait = 0.5f;

        private Rigidbody body;
        private Vector3 start;
        private float clock;

        /// <summary>Velocity the platform moves with during the current physics step.</summary>
        public Vector3 Velocity { get; private set; }

        private Vector3 Direction => axis == MoveAxis.X ? Vector3.right : Vector3.up;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            start = transform.position;
        }

        public void Bind(StageRoot stage) { }

        public void ResetForRespawn() => ResetToStart();
        public void ResetForAttempt() => ResetToStart();

        private void ResetToStart()
        {
            clock = 0f;
            Velocity = Vector3.zero;
            if (body == null)
                return;
            transform.position = start;
            body.position = start;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 current = body.position;
            clock += dt;
            Vector3 next = start + Direction * Offset(clock);
            Velocity = (next - current) / dt;
            body.MovePosition(next);
        }

        /// <summary>Signed offset along the axis at time t (linear moves with waits at both ends).</summary>
        private float Offset(float t)
        {
            float travel = Mathf.Abs(distance) / speed;
            float cycle = 2f * (travel + wait);
            if (cycle <= 0f)
                return 0f;
            float m = t % cycle;
            float s;
            if (m < wait) s = 0f;
            else if (m < wait + travel) s = (m - wait) / travel;
            else if (m < 2f * wait + travel) s = 1f;
            else s = 1f - (m - 2f * wait - travel) / travel;
            return distance * s;
        }
    }
}
