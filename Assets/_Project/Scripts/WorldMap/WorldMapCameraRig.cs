using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Third-person world map camera. It stands behind the avatar and faces "forward" along the stage progression
    /// (the chained path routes), so Up always means "next stage" on screen. Zoom blends a low chase view (close)
    /// into a high overview (far). Holding the right mouse button orbits freely; on release the view eases back.
    /// The avatar position is tracked along the progression polyline by arc length, so crossing or nearby
    /// routes never make the camera jump to another part of the map.
    /// </summary>
    [DefaultExecutionOrder(50)] // after MapPlayer moves, before WorldMapLightingDirector (100)
    public sealed class WorldMapCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Zoom (1 = default distance)")]
        [SerializeField, Min(0.05f)] private float minZoom = 0.3f;
        [SerializeField, Min(0.1f)] private float maxZoom = 3f;
        [SerializeField, Min(0.1f)] private float startZoom = 0.6f;
        [Tooltip("Camera distance from the focus point at zoom 1 (m).")]
        [SerializeField, Min(1f)] private float baseDistance = 20.8f;
        [SerializeField, Min(0f)] private float zoomSmoothTime = 0.12f;

        [Header("Pitch blends from chase (close) to overview (far)")]
        [SerializeField, Range(5f, 85f)] private float closePitch = 26f;
        [SerializeField, Range(5f, 89f)] private float farPitch = 62f;
        [Tooltip("Zoom at which the overview pitch is reached.")]
        [SerializeField, Min(0.1f)] private float overviewZoom = 1.8f;
        [Tooltip("Focus height above the avatar root (m).")]
        [SerializeField] private float focusHeight = 1.0f;
        [Tooltip("At close zoom the focus moves this far ahead so the path in front stays on screen (m).")]
        [SerializeField, Min(0f)] private float closeLeadDistance = 2.5f;

        [Header("Heading")]
        [Tooltip("Heading = progression direction measured from this far behind to this far ahead of the avatar (m).")]
        [SerializeField, Min(0.1f)] private float lookAhead = 4.5f;
        [SerializeField, Min(0f)] private float lookBehind = 1.5f;
        [SerializeField, Min(0f)] private float yawSmoothTime = 0.55f;
        [SerializeField, Min(0f)] private float followSmoothTime = 0.12f;

        [Header("Free look (right mouse)")]
        [SerializeField, Min(0f)] private float lookDegreesPerPixel = 0.25f;
        [SerializeField] private Vector2 lookPitchRange = new Vector2(-20f, 25f);
        [SerializeField, Min(0f)] private float lookReturnTime = 0.45f;

        [Header("Obstacles")]
        [SerializeField, Min(0f)] private float collisionRadius = 0.35f;
        [SerializeField] private LayerMask collisionMask = ~0;

        private readonly List<Vector3> line = new List<Vector3>();
        private readonly List<float> lineS = new List<float>();
        private float trackedS;
        private float zoom = 1f, targetZoom = 1f, zoomVelocity;
        private float yaw, yawVelocity;
        private float lookYaw, lookPitch, lookYawVelocity, lookPitchVelocity;
        private bool looking;
        private Vector3 focus, focusVelocity;
        private bool initialized;

        public float TargetZoom => targetZoom;
        /// <summary>Current camera yaw (degrees, without the free-look offset).</summary>
        public float Yaw => yaw;

        public void SetTarget(Transform newTarget) => target = newTarget;

        public void SetZoom(float value) => targetZoom = Mathf.Clamp(value, minZoom, Mathf.Max(minZoom, maxZoom));

        public void ZoomBy(float factor)
        {
            if (factor > 0f)
                SetZoom(targetZoom * factor);
        }

        /// <summary>Progression polyline (all routes chained in stage order); tracking starts at the vertex nearest to <paramref name="start"/>.</summary>
        public void SetProgression(IList<Vector3> points, Vector3 start)
        {
            line.Clear();
            lineS.Clear();
            float s = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0)
                {
                    float d = Vector3.Distance(Flat(points[i]), Flat(points[i - 1]));
                    if (d < 1e-4f)
                        continue;
                    s += d;
                }
                line.Add(points[i]);
                lineS.Add(s);
            }
            trackedS = NearestS(start);
        }

        /// <summary>Free look: orbit by the mouse delta while held; eases back to the progression heading when released.</summary>
        public void Look(bool held, Vector2 mouseDelta)
        {
            looking = held;
            if (!held)
                return;
            lookYaw += mouseDelta.x * lookDegreesPerPixel;
            lookPitch = Mathf.Clamp(lookPitch - mouseDelta.y * lookDegreesPerPixel, lookPitchRange.x, lookPitchRange.y);
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;
            if (!initialized)
            {
                SetZoom(startZoom);
                initialized = true;
            }
            zoom = targetZoom;
            zoomVelocity = 0f;
            yaw = HeadingYaw();
            yawVelocity = 0f;
            lookYaw = lookPitch = 0f;
            focus = Focus(Heading(yaw));
            focusVelocity = Vector3.zero;
            Place();
        }

        private void LateUpdate()
        {
            if (target == null)
                return;
            float dt = Time.unscaledDeltaTime;
            zoom = Mathf.SmoothDamp(zoom, targetZoom, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, dt);
            yaw = Mathf.SmoothDampAngle(yaw, HeadingYaw(), ref yawVelocity, yawSmoothTime, Mathf.Infinity, dt);
            if (!looking)
            {
                lookYaw = Mathf.SmoothDampAngle(lookYaw, 0f, ref lookYawVelocity, lookReturnTime, Mathf.Infinity, dt);
                lookPitch = Mathf.SmoothDamp(lookPitch, 0f, ref lookPitchVelocity, lookReturnTime, Mathf.Infinity, dt);
            }
            focus = Vector3.SmoothDamp(focus, Focus(Heading(yaw)), ref focusVelocity, followSmoothTime, Mathf.Infinity, dt);
            Place();
        }

        // ------------------------------------------------------------------ placement

        private float Blend => Mathf.Clamp01(Mathf.InverseLerp(minZoom, overviewZoom, zoom));

        private Vector3 Focus(Vector3 heading)
        {
            float lead = Mathf.Lerp(closeLeadDistance, 0f, Blend);
            return target.position + Vector3.up * focusHeight + heading * lead;
        }

        private void Place()
        {
            float pitch = Mathf.Lerp(closePitch, farPitch, Blend) + lookPitch;
            var rot = Quaternion.Euler(Mathf.Clamp(pitch, 5f, 89f), yaw + lookYaw, 0f);
            float distance = baseDistance * zoom;
            Vector3 back = rot * Vector3.back;
            if (collisionRadius > 0f && Physics.SphereCast(focus, collisionRadius, back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore))
                distance = Mathf.Max(1f, hit.distance);
            transform.SetPositionAndRotation(focus + back * distance, rot);
        }

        private static Vector3 Heading(float yawDegrees) => Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;

        // ------------------------------------------------------------------ progression tracking

        private float HeadingYaw()
        {
            if (line.Count < 2)
                return yaw;
            trackedS = Project(Flat(target.position), trackedS);
            Vector3 a = PointAt(trackedS - lookBehind), b = PointAt(trackedS + lookAhead);
            Vector3 d = Flat(b) - Flat(a);
            if (d.sqrMagnitude < 1e-4f)
            {
                // At the very end of the line: keep the last segment's direction.
                d = Flat(line[line.Count - 1]) - Flat(line[line.Count - 2]);
            }
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>Arc length of the closest polyline point, searched only near the previous value.</summary>
        private float Project(Vector3 p, float around)
        {
            const float Window = 8f;
            float best = around, bestDist = float.MaxValue;
            for (int i = 1; i < line.Count; i++)
            {
                if (lineS[i] < around - Window || lineS[i - 1] > around + Window)
                    continue;
                Vector3 a = Flat(line[i - 1]), b = Flat(line[i]);
                Vector3 ab = b - a;
                float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                float dist = (a + ab * t - p).sqrMagnitude;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = Mathf.Lerp(lineS[i - 1], lineS[i], t);
                }
            }
            return best;
        }

        private Vector3 PointAt(float s)
        {
            if (s <= 0f)
                return line[0];
            for (int i = 1; i < line.Count; i++)
            {
                if (s <= lineS[i])
                    return Vector3.Lerp(line[i - 1], line[i], (s - lineS[i - 1]) / Mathf.Max(lineS[i] - lineS[i - 1], 1e-6f));
            }
            return line[line.Count - 1];
        }

        /// <summary>Arc length of the polyline vertex closest to <paramref name="p"/> (global search, used once per node).</summary>
        public float NearestS(Vector3 p)
        {
            p = Flat(p);
            float best = 0f, bestDist = float.MaxValue;
            for (int i = 0; i < line.Count; i++)
            {
                float d = (Flat(line[i]) - p).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = lineS[i];
                }
            }
            return best;
        }

        private static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
