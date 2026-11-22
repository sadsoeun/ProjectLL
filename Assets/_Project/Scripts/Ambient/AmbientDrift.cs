using UnityEngine;

namespace MyGame
{
    /// <summary>Slow back-and-forth drift for background clouds.</summary>
    public sealed class AmbientDrift : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new Vector3(2.5f, 0.15f, 0f);
        [SerializeField, Min(1f)] private float period = 50f;

        private Vector3 basePosition;
        private float phase;

        private void Awake()
        {
            basePosition = transform.localPosition;
            phase = (transform.position.x * 0.13f + transform.position.z * 0.07f) % (Mathf.PI * 2f);
        }

        private void Update()
        {
            float t = Time.time * Mathf.PI * 2f / period + phase;
            transform.localPosition = basePosition + new Vector3(Mathf.Sin(t) * offset.x, Mathf.Sin(t * 1.7f) * offset.y, Mathf.Cos(t) * offset.z);
        }
    }
}
