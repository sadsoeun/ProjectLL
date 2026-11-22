using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyGame
{
    /// <summary>How the screen border of <see cref="WeatherScreenFx"/> is drawn.</summary>
    public enum WeatherBorderStyle
    {
        /// <summary>Warm sun glow from the top-left corner and moving leaf shade (meadow).</summary>
        LightLeak = 0,
        /// <summary>Wind-blown sand piling up on the screen edges (desert).</summary>
        Dust = 1,
        /// <summary>Water drops on the lens that refract the scene (coast).</summary>
        Droplets = 2,
        /// <summary>Charred edges with glowing cracks (lava fortress).</summary>
        Scorch = 3,
    }

    [Serializable]
    public sealed class WeatherBorderStyleParameter : VolumeParameter<WeatherBorderStyle>
    {
        public WeatherBorderStyleParameter(WeatherBorderStyle value, bool overrideState = false) : base(value, overrideState) { }
    }

    /// <summary>
    /// Volume settings of the themed stage screen effect: wind-blown particles (petals, sand, sea spray, embers),
    /// a themed screen border, wind streaks / haze and optional heat shimmer. The frost stages keep their own
    /// <see cref="FrostScreenFx"/>. Drawn by <see cref="WeatherScreenFxFeature"/> in a single full-screen pass;
    /// inactive while intensity is 0.
    /// </summary>
    [Serializable, VolumeComponentMenu("MyGame/Weather Screen FX")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class WeatherScreenFx : VolumeComponent, IPostProcessComponent
    {
        [Header("Master")]
        [Tooltip("Overall strength of the effect. 0 disables the pass.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        [Header("Particles")]
        [Tooltip("Share of grid cells that hold a particle (all three depth layers).")]
        public ClampedFloatParameter particleAmount = new ClampedFloatParameter(0.4f, 0f, 1f);
        [Tooltip("Particle size multiplier.")]
        public ClampedFloatParameter particleSize = new ClampedFloatParameter(1f, 0.3f, 2.5f);
        [Tooltip("Drift speed multiplier.")]
        public ClampedFloatParameter particleSpeed = new ClampedFloatParameter(1f, 0f, 3f);
        public ColorParameter particleColor = new ColorParameter(Color.white, false, false, true);
        [Tooltip("Second colour; each particle picks a mix of the two.")]
        public ColorParameter particleColorB = new ColorParameter(Color.white, false, false, true);
        [Tooltip("0 = opaque particles (petals, sand), 1 = glowing additive particles (embers).")]
        public ClampedFloatParameter particleGlow = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Own movement in screen heights per second (y < 0 falls, y > 0 rises), added to the wind.")]
        public Vector2Parameter particleDrift = new Vector2Parameter(new Vector2(0f, -0.05f));
        [Tooltip("Tumbling: sway and flip of each particle (petals, leaves).")]
        public ClampedFloatParameter particleFlutter = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Stretch along the movement direction (motion streak).")]
        public ClampedFloatParameter particleStretch = new ClampedFloatParameter(0.5f, 0f, 3f);

        [Header("Wind")]
        [Tooltip("Screen-space wind direction (x = right, y = up). Normalized in the shader.")]
        public Vector2Parameter windDirection = new Vector2Parameter(new Vector2(-1f, -0.1f));
        [Tooltip("How hard particles are pushed along the wind.")]
        public ClampedFloatParameter windStrength = new ClampedFloatParameter(1f, 0f, 3f);
        [Tooltip("Strength of the periodic gusts (0 = steady wind).")]
        public ClampedFloatParameter gustStrength = new ClampedFloatParameter(0.5f, 0f, 1f);
        [Tooltip("Opacity of the fast thin wind streaks.")]
        public ClampedFloatParameter streakAmount = new ClampedFloatParameter(0.2f, 0f, 1f);
        [Tooltip("Colour of the gust haze and the wind streaks.")]
        public ColorParameter hazeColor = new ColorParameter(Color.white, false, false, true);
        [Tooltip("Opacity of the drifting haze at the peak of a gust.")]
        public ClampedFloatParameter hazeAmount = new ClampedFloatParameter(0.08f, 0f, 0.5f);

        [Header("Screen border")]
        public WeatherBorderStyleParameter borderStyle = new WeatherBorderStyleParameter(WeatherBorderStyle.Dust);
        [Tooltip("Width of the border, in screen heights.")]
        public ClampedFloatParameter borderWidth = new ClampedFloatParameter(0.14f, 0f, 0.5f);
        [Tooltip("Opacity of the border layer at the very edge.")]
        public ClampedFloatParameter borderOpacity = new ClampedFloatParameter(0.5f, 0f, 1f);
        [Tooltip("Refraction of the scene behind the border (screen UV units).")]
        public ClampedFloatParameter borderDistortion = new ClampedFloatParameter(0.006f, 0f, 0.05f);
        [Tooltip("Amount of style detail: sun glow, sand grains, water drops, glowing cracks.")]
        public ClampedFloatParameter borderDetail = new ClampedFloatParameter(0.6f, 0f, 1f);
        public ColorParameter borderTint = new ColorParameter(Color.white, false, false, true);
        [Tooltip("Emissive / highlight colour of the style detail.")]
        public ColorParameter borderGlow = new ColorParameter(Color.white, false, false, true);

        [Header("Heat shimmer")]
        [Tooltip("Full-screen heat distortion (screen UV units). 0 = off.")]
        public ClampedFloatParameter heatShimmer = new ClampedFloatParameter(0f, 0f, 0.01f);
        [Tooltip("Size of the shimmer waves (higher = finer).")]
        public ClampedFloatParameter heatScale = new ClampedFloatParameter(9f, 1f, 30f);
        [Tooltip("Rise speed of the shimmer waves.")]
        public ClampedFloatParameter heatSpeed = new ClampedFloatParameter(0.6f, 0f, 3f);

        [Header("Water splashes (Droplets style)")]
        [Tooltip("Opacity of the small splashes that jump up from the bottom edge. 0 = off.")]
        public ClampedFloatParameter splashAmount = new ClampedFloatParameter(0f, 0f, 1f);

        public bool IsActive() => active && intensity.value > 0f;
    }
}
