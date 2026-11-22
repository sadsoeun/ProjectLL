using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Drives the global wind read by the ambient shaders (MyGame/Ambient/*) and by AmbientSpin.
    /// One per scene; without it the shaders fall back to a gentle default breeze.
    /// </summary>
    public sealed class AmbientWind : MonoBehaviour
    {
        private static readonly int WindId = Shader.PropertyToID("_WM_Wind");

        [Tooltip("Direction the wind blows toward, in degrees on the XZ plane (0 = +X, 90 = +Z).")]
        [SerializeField] private float directionDegrees = 35f;
        [Tooltip("Scales every wind material's sway.")]
        [SerializeField, Min(0f)] private float strength = 1f;
        [Tooltip("Seconds per gust cycle (Perlin noise).")]
        [SerializeField, Min(0.1f)] private float gustPeriod = 6f;
        [SerializeField, Range(0f, 1f)] private float gustAmount = 0.6f;

        /// <summary>Current gust 0..1 (also used by spinning props).</summary>
        public static float Gust { get; private set; }

        private void Update()
        {
            Gust = Mathf.PerlinNoise(Time.time / gustPeriod, 0.37f) * gustAmount;
            float rad = directionDegrees * Mathf.Deg2Rad;
            Shader.SetGlobalVector(WindId, new Vector4(Mathf.Cos(rad), Mathf.Sin(rad), strength, Gust));
        }

        private void OnDisable()
        {
            Gust = 0f;
            Shader.SetGlobalVector(WindId, Vector4.zero);
        }
    }
}
