using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Per-stage atmosphere (theme kit values written by the stage builder): sky colour, distance fog and sun tint.
    /// Stage scenes are loaded additively under 06_InGame, so the stage cannot rely on its own scene RenderSettings; this
    /// component applies its values while the stage is loaded and restores the previous ones when it unloads.
    /// Fog pushes the background layers into a cool haze so the white play strip reads clearly in front of them.
    /// </summary>
    public sealed class StageAtmosphere : MonoBehaviour
    {
        [Header("Sky")]
        public Color skyColor = new Color(0.55f, 0.75f, 0.95f);

        [Header("Distance fog (linear, from the camera)")]
        public bool fog = true;
        public Color fogColor = new Color(0.62f, 0.72f, 0.86f);
        [Min(0f)] public float fogStart = 22f;
        [Min(1f)] public float fogEnd = 150f;

        [Header("Sun (the stage's directional light, optional)")]
        public Light sun;
        public Color sunColor = Color.white;
        [Min(0f)] public float sunIntensity = 1.1f;

        private bool applied;
        private bool oldFog;
        private Color oldFogColor;
        private FogMode oldFogMode;
        private float oldFogStart, oldFogEnd;
        private Camera cam;
        private Color oldSky;

        private void Start() => Apply();

        /// <summary>Applies the values (also call after editing them at runtime).</summary>
        public void Apply()
        {
            if (!applied)
            {
                oldFog = RenderSettings.fog;
                oldFogColor = RenderSettings.fogColor;
                oldFogMode = RenderSettings.fogMode;
                oldFogStart = RenderSettings.fogStartDistance;
                oldFogEnd = RenderSettings.fogEndDistance;
                cam = Camera.main;
                if (cam != null)
                    oldSky = cam.backgroundColor;
                applied = true;
            }
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = Mathf.Max(fogStart + 1f, fogEnd);
            if (cam != null)
                cam.backgroundColor = skyColor;
            if (sun != null)
            {
                sun.color = sunColor;
                sun.intensity = sunIntensity;
            }
        }

        private void OnDestroy()
        {
            if (!applied)
                return;
            RenderSettings.fog = oldFog;
            RenderSettings.fogColor = oldFogColor;
            RenderSettings.fogMode = oldFogMode;
            RenderSettings.fogStartDistance = oldFogStart;
            RenderSettings.fogEndDistance = oldFogEnd;
            if (cam != null)
                cam.backgroundColor = oldSky;
        }
    }
}
