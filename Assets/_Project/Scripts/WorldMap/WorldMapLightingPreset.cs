using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame
{
    /// <summary>
    /// Lighting mood of one world map region (a world, or an override such as a boss node).
    /// WorldMapLightingDirector blends the presets of nearby regions as the map avatar walks.
    /// </summary>
    [CreateAssetMenu(menuName = "MyGame/World Map Lighting Preset", fileName = "WML_")]
    public sealed class WorldMapLightingPreset : ScriptableObject
    {
        [Header("Sun (main directional light)")]
        public Color sunColor = Color.white;
        [Min(0f)] public float sunIntensity = 1.2f;
        [Tooltip("Sun elevation in degrees (90 = straight down).")]
        [Range(5f, 90f)] public float sunPitch = 45f;
        [Tooltip("Sun heading in degrees (world yaw the light points toward).")]
        public float sunYaw = 330f;
        [Range(0f, 1f)] public float shadowStrength = 0.75f;

        [Header("Ambient (gradient: sky / horizon / ground)")]
        public Color ambientSky = new Color(0.62f, 0.72f, 0.88f);
        public Color ambientEquator = new Color(0.58f, 0.62f, 0.62f);
        public Color ambientGround = new Color(0.42f, 0.40f, 0.34f);

        [Header("Distance fog (distances are measured beyond the map avatar, so the haze is stable at every zoom)")]
        public Color fogColor = new Color(0.70f, 0.82f, 0.95f);
        [Tooltip("Fog starts this far beyond the camera-to-avatar distance.")]
        public float fogStartBeyondAvatar = 10f;
        [Tooltip("Fog is full this far beyond the camera-to-avatar distance.")]
        [Min(1f)] public float fogEndBeyondAvatar = 120f;

        [Header("Sky (camera clear colour)")]
        public Color skyColor = new Color(0.55f, 0.78f, 0.97f);

        [Header("Post-processing")]
        [Tooltip("Colour grading of this region. All world map profiles must override the same parameters so they blend.")]
        public VolumeProfile profile;
    }
}
