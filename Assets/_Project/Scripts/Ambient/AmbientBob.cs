using UnityEngine;

namespace MyGame
{
    /// <summary>Floating idle motion (boats, buoys, ice floes): bob up and down with a slight roll and pitch.</summary>
    public sealed class AmbientBob : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float height = 0.08f;
        [SerializeField, Min(0.1f)] private float period = 3.2f;
        [SerializeField, Min(0f)] private float rollDegrees = 2.5f;
        [SerializeField, Min(0f)] private float pitchDegrees = 1.5f;

        private Vector3 basePosition;
        private Quaternion baseRotation;
        private float phase;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseRotation = transform.localRotation;
            // neighbouring floaters drift out of step
            phase = (transform.position.x * 0.37f + transform.position.z * 0.53f) % (Mathf.PI * 2f);
        }

        private void Update()
        {
            float t = Time.time * Mathf.PI * 2f / period + phase;
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(t) * height);
            transform.localRotation = baseRotation * Quaternion.Euler(Mathf.Sin(t * 0.8f + 1.3f) * pitchDegrees, 0f, Mathf.Sin(t * 0.6f) * rollDegrees);
        }
    }
}
