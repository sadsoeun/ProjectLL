using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Stage-clear camera move: eases the in-game camera toward the character (upper body framing) while the victory pose plays.
    /// Takes over the camera from <see cref="CameraFollow"/> while active and restores it on <see cref="End"/>.
    /// Runs on unscaled time so it keeps moving when the result screen pauses the game.
    /// </summary>
    public sealed class ClearCameraZoom : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Settings
        {
            [Tooltip("Distance from the focus point to the camera (m).")]
            public float distance = 3.7f;
            [Tooltip("Height above the character's feet the camera looks at (m): about chest/shoulder height frames the upper body.")]
            public float focusHeight = 1.0f;
            [Tooltip("Camera x offset (m); negative moves the camera left so the character sits on the right third of the frame.")]
            public float sideOffset = -1.0f;
            [Tooltip("Field of view during the zoom (0 keeps the camera's own value).")]
            public float fieldOfView = 0f;
            [Tooltip("Seconds to reach the close-up.")]
            public float duration = 1.4f;
            public AnimationCurve ease = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 0f, 0f));
        }

        private Settings settings;
        private Transform target;
        private CameraFollow follow;
        private Camera cam;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float startFov;
        private float elapsed;
        private bool active;
        private bool followWasEnabled;

        public bool IsActive => active;

        public void Begin(Transform newTarget, Settings newSettings)
        {
            target = newTarget;
            settings = newSettings ?? new Settings();
            cam = GetComponent<Camera>();
            follow = GetComponent<CameraFollow>();
            if (cam == null || target == null)
                return;
            followWasEnabled = follow != null && follow.enabled;
            if (follow != null)
                follow.enabled = false;
            startPosition = transform.position;
            startRotation = transform.rotation;
            startFov = cam.fieldOfView;
            elapsed = 0f;
            active = true;
        }

        public void End()
        {
            if (!active)
                return;
            active = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            if (cam != null)
                cam.fieldOfView = startFov;
            if (follow != null)
                follow.enabled = followWasEnabled;
        }

        private void LateUpdate()
        {
            if (!active || target == null)
                return;
            elapsed += Time.unscaledDeltaTime;
            float t = settings.duration <= 0f ? 1f : Mathf.Clamp01(elapsed / settings.duration);
            float k = settings.ease != null ? settings.ease.Evaluate(t) : t;
            Vector3 focus = target.position + Vector3.up * settings.focusHeight;
            Vector3 endPosition = focus + new Vector3(settings.sideOffset, 0f, -settings.distance);
            Quaternion endRotation = startRotation;   // the side camera keeps its rotation: the x offset puts the character on the right third
            transform.position = Vector3.LerpUnclamped(startPosition, endPosition, k);
            transform.rotation = Quaternion.SlerpUnclamped(startRotation, endRotation, k);
            if (settings.fieldOfView > 0f)
                cam.fieldOfView = Mathf.LerpUnclamped(startFov, settings.fieldOfView, k);
        }
    }
}
