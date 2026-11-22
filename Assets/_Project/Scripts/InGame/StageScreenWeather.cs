using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyGame
{
    /// <summary>
    /// Per-stage screen post-processing (theme kit profile written by the stage builder, e.g. the frost blizzard).
    /// Stages load additively under 06_InGame, whose camera normally renders without post-processing; this component
    /// turns post-processing on and adds a global volume while the stage is loaded, and restores both when it unloads.
    /// </summary>
    public sealed class StageScreenWeather : MonoBehaviour
    {
        [Tooltip("Volume profile of the stage theme (for example FrostPeaks_ScreenFx).")]
        public VolumeProfile profile;
        [Tooltip("Volume priority. Must stay below the perfect-dodge time stop (1000).")]
        public float priority = 10f;
        [Range(0f, 1f)] public float weight = 1f;

        private Volume volume;
        private UniversalAdditionalCameraData cameraData;
        private bool savedPostProcessing;
        private bool applied;

        /// <summary>The created volume (null before Start). Lets tools toggle the effect for comparisons.</summary>
        public Volume Volume => volume;

        private void Start() => Apply();

        public void Apply()
        {
            if (applied || profile == null)
                return;
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out cameraData))
            {
                savedPostProcessing = cameraData.renderPostProcessing;
                cameraData.renderPostProcessing = true;
            }
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = weight;
            volume.sharedProfile = profile;
            applied = true;
        }

        /// <summary>Turns the effect on or off at runtime (keeps the camera state consistent).</summary>
        public void SetEnabled(bool on)
        {
            if (!applied)
                Apply();
            if (volume != null)
                volume.enabled = on;
            if (cameraData != null)
                cameraData.renderPostProcessing = on || savedPostProcessing;
        }

        private void OnDestroy()
        {
            if (!applied)
                return;
            if (cameraData != null)
                cameraData.renderPostProcessing = savedPostProcessing;
        }
    }
}
