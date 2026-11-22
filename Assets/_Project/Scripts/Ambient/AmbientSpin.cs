using UnityEngine;

namespace MyGame
{
    /// <summary>Idle rotation for windmill blades, wheels and similar props; gusts speed it up.</summary>
    public sealed class AmbientSpin : MonoBehaviour
    {
        [Tooltip("Rotation axis in local space.")]
        [SerializeField] private Vector3 axis = Vector3.forward;
        [SerializeField] private float degreesPerSecond = 40f;
        [Tooltip("Extra speed at full gust, as a fraction of the base speed.")]
        [SerializeField, Min(0f)] private float gustBoost = 0.8f;

        public void Configure(Vector3 localAxis, float speed)
        {
            axis = localAxis;
            degreesPerSecond = speed;
        }

        private void Update()
        {
            float speed = degreesPerSecond * (1f + AmbientWind.Gust * gustBoost);
            transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
        }
    }
}
