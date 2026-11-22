using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyGame
{
    /// <summary>
    /// Volume settings of the subtle background outline (screen-space depth + normals edges).
    /// Drawn by <see cref="ScreenOutlineFeature"/>; inactive while intensity is 0 (the default, so scenes without an
    /// outline volume such as the title pay nothing). Stages get it from the 06_InGame volume, the world map from its own.
    /// Characters are excluded per renderer with the Rendering Layer "ScreenOutlineExclude".
    /// </summary>
    [Serializable, VolumeComponentMenu("MyGame/Screen Outline")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class ScreenOutline : VolumeComponent, IPostProcessComponent
    {
        [Header("Master")]
        [Tooltip("Overall strength of the outline. 0 disables the pass.")]
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("Line thickness in pixels at 1080p (scaled with the screen height, at least 1 px).")]
        public ClampedFloatParameter thickness = new ClampedFloatParameter(1f, 0.5f, 3f);
        [Tooltip("Multiply tint at full edge strength (the line darkens the surface colour, it is never pure black).")]
        public ColorParameter tint = new ColorParameter(new Color(0.32f, 0.3f, 0.36f, 1f), false, false, true);
        [Tooltip("Cap of the darkening amount per pixel.")]
        public ClampedFloatParameter maxDarkening = new ClampedFloatParameter(1f, 0f, 1f);

        [Header("Edge detection")]
        [Tooltip("Relative depth step (convexity) that starts a silhouette line. Higher = fewer lines.")]
        public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(0.012f, 0.001f, 0.2f);
        [Tooltip("Weight of silhouette (depth) lines.")]
        public ClampedFloatParameter depthWeight = new ClampedFloatParameter(1f, 0f, 1f);
        [Tooltip("Crease threshold as 1 - cos(angle). 0.3 ≈ 46°. Higher = only sharp corners.")]
        public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.3f, 0.02f, 1.5f);
        [Tooltip("Weight of crease (normal) lines.")]
        public ClampedFloatParameter normalWeight = new ClampedFloatParameter(0.7f, 0f, 1f);
        [Tooltip("Crease weight multiplier on wind foliage (keeps leaves from turning noisy).")]
        public ClampedFloatParameter foliageCreaseScale = new ClampedFloatParameter(0.1f, 0f, 1f);
        [Tooltip("Ramp width above the thresholds, as a multiple of the threshold (larger = softer lines).")]
        public ClampedFloatParameter softness = new ClampedFloatParameter(1.5f, 0.1f, 5f);

        [Header("Distance")]
        [Tooltip("Eye distance (m) where the outline starts fading out.")]
        public MinFloatParameter fadeStart = new MinFloatParameter(24f, 0f);
        [Tooltip("Eye distance (m) where the outline is gone.")]
        public MinFloatParameter fadeEnd = new MinFloatParameter(55f, 0f);
        [Tooltip("Lines fade in over this distance from the camera (m).")]
        public MinFloatParameter nearFade = new MinFloatParameter(1f, 0f);

        public bool IsActive() => active && intensity.value > 0f;
    }
}
