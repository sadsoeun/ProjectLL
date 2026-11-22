using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Moving belt (layout blocks with surface "conveyor_r" / "conveyor_l"). The block itself stays put; it reports a
    /// constant <see cref="Velocity"/> as a platform, so PlayerMotor carries the player along it exactly like on a
    /// moving platform (walking against it is slower, with it faster; a jump keeps the belt's momentum).
    /// The arrow children (optional) scroll along the belt to show the direction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ConveyorSurface : MonoBehaviour, IPlatformMotion
    {
        [Tooltip("Belt speed in m/s; positive carries toward +X (right), negative toward -X.")]
        public float speed = 3f;
        [Tooltip("Arrow markers scrolled along the belt (optional).")]
        [SerializeField] private Transform[] arrows;
        [Tooltip("Half length of the belt in the arrows' local space: arrows wrap around at +/- this value.")]
        [SerializeField, Min(0.1f)] private float halfLength = 1f;

        [Tooltip("Rollers under the belt, turned about their local Z as the belt runs (optional).")]
        [SerializeField] private Transform[] wheels;
        [SerializeField, Min(0.01f)] private float wheelRadius = 0.115f;
        [Tooltip("Driving drums at the ends (optional).")]
        [SerializeField] private Transform[] drums;
        [SerializeField, Min(0.01f)] private float drumRadius = 0.215f;
        [Tooltip("Upper limit of the visible spin in degrees per second (fast spokes strobe on screen).")]
        [SerializeField, Min(0f)] private float maxSpinDegrees = 420f;

        public Vector3 Velocity => new Vector3(speed, 0f, 0f);

        private void Update()
        {
            float dt = Time.deltaTime;
            if (arrows != null)
            {
                // The step is converted to the arrows' local units (their parent may be scaled).
                foreach (var a in arrows)
                {
                    if (a == null)
                        continue;
                    float parentScale = a.parent != null ? a.parent.lossyScale.x : 1f;
                    float localStep = speed * dt / Mathf.Max(0.0001f, parentScale);
                    var p = a.localPosition;
                    p.x += localStep;
                    if (p.x > halfLength) p.x -= 2f * halfLength;
                    else if (p.x < -halfLength) p.x += 2f * halfLength;
                    a.localPosition = p;
                }
            }
            Spin(wheels, wheelRadius, dt);
            Spin(drums, drumRadius, dt);
        }

        /// <summary>A belt running toward +X turns its rollers clockwise as seen from the camera (negative Z angle).</summary>
        private void Spin(Transform[] parts, float radius, float dt)
        {
            if (parts == null || parts.Length == 0)
                return;
            float degrees = Mathf.Clamp(-speed / radius * Mathf.Rad2Deg, -maxSpinDegrees, maxSpinDegrees) * dt;
            foreach (var t in parts)
            {
                if (t != null)
                    t.Rotate(0f, 0f, degrees, Space.Self);
            }
        }
    }
}
