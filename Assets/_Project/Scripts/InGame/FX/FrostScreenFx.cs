using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyGame
{
    /// <summary>
    /// Volume settings of the frost-stage screen effect (blowing snow, frozen screen edges, wind streaks).
    /// Drawn by <see cref="FrostScreenFxFeature"/> in a single full-screen pass; inactive while intensity is 0.
    /// </summary>
    [Serializable, VolumeComponentMenu("MyGame/Frost Screen FX")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class FrostScreenFx : VolumeComponent, IPostProcessComponent
    {
        [Header("Master")]
        [Tooltip("Overall strength of the effect. 0 disables the pass.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);

        [Header("Snow")]
        [Tooltip("Share of grid cells that hold a flake (all three depth layers).")]
        public ClampedFloatParameter snowAmount = new ClampedFloatParameter(0.55f, 0f, 1f);
        [Tooltip("Flake size multiplier.")]
        public ClampedFloatParameter flakeSize = new ClampedFloatParameter(1f, 0.3f, 2.5f);
        [Tooltip("Fall / drift speed multiplier.")]
        public ClampedFloatParameter snowSpeed = new ClampedFloatParameter(1f, 0f, 3f);
        public ColorParameter snowColor = new ColorParameter(new Color(0.95f, 0.98f, 1f, 1f), false, false, true);

        [Header("Wind")]
        [Tooltip("Screen-space wind direction (x = right, y = up). Normalized in the shader.")]
        public Vector2Parameter windDirection = new Vector2Parameter(new Vector2(-1f, -0.32f));
        [Tooltip("How hard the snow is pushed along the wind (also stretches flakes into streaks).")]
        public ClampedFloatParameter windStrength = new ClampedFloatParameter(1f, 0f, 3f);
        [Tooltip("Strength of the periodic gusts (0 = steady wind).")]
        public ClampedFloatParameter gustStrength = new ClampedFloatParameter(0.6f, 0f, 1f);
        [Tooltip("Opacity of the fast thin wind streaks.")]
        public ClampedFloatParameter streakAmount = new ClampedFloatParameter(0.35f, 0f, 1f);

        [Header("Frozen edges")]
        [Tooltip("Width of the frost border, in screen heights.")]
        public ClampedFloatParameter frostWidth = new ClampedFloatParameter(0.16f, 0f, 0.5f);
        [Tooltip("Opacity of the frost layer at the very edge.")]
        public ClampedFloatParameter frostOpacity = new ClampedFloatParameter(0.75f, 0f, 1f);
        [Tooltip("Refraction of the scene behind the frost (screen UV units).")]
        public ClampedFloatParameter frostDistortion = new ClampedFloatParameter(0.012f, 0f, 0.05f);
        public ColorParameter frostTint = new ColorParameter(new Color(0.78f, 0.9f, 1f, 1f), false, false, true);

        public bool IsActive() => active && intensity.value > 0f;
    }
}
