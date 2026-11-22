using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Smooth follow camera with a fixed rotation. Used by the world map (angled view)
    /// and by the in-game side camera (optional X/Y clamp from the stage bounds).
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 2.5f, -12f);
        [SerializeField, Min(0f)] private float smoothTime = 0.15f;
        [SerializeField] private bool useBounds;
        [SerializeField] private float minX = -100000f;
        [SerializeField] private float maxX = 100000f;
        [SerializeField] private float minY = -100000f;
        [Tooltip("With view bounds, minX / maxX are the terrain ends and the whole view stays inside them at this depth (back of the course).")]
        [SerializeField] private float edgePlaneZ = 2f;

        [Header("Zoom (scales the offset along its own direction; 1 = offset as set)")]
        [SerializeField, Min(0.1f)] private float minZoom = 0.55f;
        [SerializeField, Min(0.1f)] private float maxZoom = 1.6f;
        [SerializeField, Min(0f)] private float zoomSmoothTime = 0.12f;

        private Vector3 velocity;
        private bool clampViewEdges;
        private Camera cam;
        private float zoom = 1f;
        private float targetZoom = 1f;
        private float zoomVelocity;

        /// <summary>Zoom the camera is easing toward (smaller = closer). Clamped to [minZoom, maxZoom].</summary>
        public float TargetZoom => targetZoom;

        public void SetZoom(float value)
        {
            targetZoom = Mathf.Clamp(value, minZoom, Mathf.Max(minZoom, maxZoom));
        }

        /// <summary>Multiplies the target zoom (factor &lt; 1 moves closer).</summary>
        public void ZoomBy(float factor)
        {
            if (factor > 0f)
                SetZoom(targetZoom * factor);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>Clamps the camera centre to [newMinX, newMaxX].</summary>
        public void SetBounds(float newMinX, float newMaxX, float newMinY)
        {
            useBounds = true;
            clampViewEdges = false;
            minX = newMinX;
            maxX = Mathf.Max(newMinX, newMaxX);
            minY = newMinY;
        }

        /// <summary>
        /// Keeps the visible area inside the terrain [leftEdge, rightEdge]: near the ends of the course the camera stops
        /// so no empty space past the terrain shows. Recomputed every frame, so aspect changes are handled.
        /// </summary>
        public void SetViewBounds(float leftEdge, float rightEdge, float newMinY)
        {
            SetBounds(leftEdge, rightEdge, newMinY);
            clampViewEdges = true;
        }

        // Arena lock (boss fights): the view is held inside [left, right]; Unlock restores the stage bounds.
        private bool arenaLocked;
        private bool savedUseBounds, savedClamp;
        private float savedMinX, savedMaxX, savedMinY;

        public bool IsArenaLocked => arenaLocked;

        /// <summary>Keeps the whole view inside the arena [left, right]; a view wider than the arena is centred on it.</summary>
        public void LockArena(float left, float right)
        {
            if (!arenaLocked)
            {
                savedUseBounds = useBounds;
                savedClamp = clampViewEdges;
                savedMinX = minX;
                savedMaxX = maxX;
                savedMinY = minY;
                arenaLocked = true;
            }
            float y = useBounds ? minY : -100000f;
            SetViewBounds(left, right, y);
        }

        /// <summary>Restores the bounds that were active before LockArena. No-op when not locked.</summary>
        public void UnlockArena()
        {
            if (!arenaLocked)
                return;
            arenaLocked = false;
            useBounds = savedUseBounds;
            clampViewEdges = savedClamp;
            minX = savedMinX;
            maxX = savedMaxX;
            minY = savedMinY;
        }

        public void SnapToTarget()
        {
            velocity = Vector3.zero;
            zoom = targetZoom;
            zoomVelocity = 0f;
            if (target != null)
                transform.position = Desired();
        }

        private Vector3 Desired()
        {
            Vector3 p = target.position + offset * zoom;
            if (useBounds)
            {
                float lo = minX, hi = maxX;
                if (clampViewEdges)
                {
                    float halfWidth = ViewHalfWidth(p.z);
                    lo += halfWidth;
                    hi -= halfWidth;
                }
                p.x = lo <= hi ? Mathf.Clamp(p.x, lo, hi) : (minX + maxX) * 0.5f; // course narrower than the view: centre it
                p.y = Mathf.Max(p.y, minY);
            }
            return p;
        }

        /// <summary>Half the visible width at edgePlaneZ for a camera standing at depth <paramref name="cameraZ"/>.</summary>
        private float ViewHalfWidth(float cameraZ)
        {
            if (cam == null)
                cam = GetComponent<Camera>();
            float distance = Mathf.Abs(edgePlaneZ - cameraZ);
            if (cam.orthographic)
                return cam.orthographicSize * cam.aspect;
            return distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * cam.aspect;
        }

        private void LateUpdate()
        {
            if (target == null)
                return;
            zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            // Unscaled so the world map camera keeps moving; the paused game does not move the target anyway.
            transform.position = Vector3.SmoothDamp(transform.position, Desired(), ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }
    }
}
