using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// World-map avatar. The root moves between nodes; the visual model is a child that turns toward the
    /// travel direction (and back toward the camera when idle) and, if an Animator is set, runs while moving.
    /// </summary>
    public sealed class MapPlayer : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");
        private bool hasMotionSpeed;

        [SerializeField] private Transform visual;
        [Tooltip("Optional character Animator (Speed / Grounded parameters, same controller as in game).")]
        [SerializeField] private Animator animator;
        [SerializeField, Min(0.1f)] private float moveSpeed = 6f;
        [Tooltip("Degrees per second the visual turns.")]
        [SerializeField, Min(0f)] private float turnSpeed = 720f;
        [Tooltip("World yaw the visual faces while standing (180 = toward the map camera).")]
        [SerializeField] private float idleYaw = 180f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 6f;
        [Tooltip("Ground speed (m/s at visual scale 1) the Run clip covers at MotionSpeed 1; keeps the feet from sliding. Same value as PlayerAnimator.runClipSpeed.")]
        [SerializeField, Min(0.1f)] private float runClipSpeed = 5.4f;
        [SerializeField, Min(0.1f)] private float maxMotionSpeed = 1.8f;

        private Vector3 target;
        private Vector3[] route = new Vector3[0];
        private int routeIndex;
        private Vector3 visualBase;
        private Quaternion facing;

        public bool IsMoving { get; private set; }
        public event Action Arrived;

        private void Awake()
        {
            UnityEngine.Assertions.Assert.IsNotNull(visual, "MapPlayer.visual is not assigned.");
            visualBase = visual.localPosition;
            facing = Quaternion.Euler(0f, idleYaw, 0f);
            visual.rotation = facing;
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.SetBool(GroundedId, true);
                foreach (var p in animator.parameters)
                    if (p.nameHash == MotionSpeedId && p.type == AnimatorControllerParameterType.Float)
                        hasMotionSpeed = true;
            }
        }

        public void Teleport(Vector3 position)
        {
            transform.position = position;
            IsMoving = false;
        }

        public void MoveTo(Vector3 position) => MoveAlong(new[] { position });

        /// <summary>World yaw faced while standing (the rotating world map camera keeps it pointed at itself).</summary>
        public void SetIdleYaw(float yaw)
        {
            idleYaw = yaw;
            if (!IsMoving)
                facing = Quaternion.Euler(0f, idleYaw, 0f);
        }

        /// <summary>Walks the points in order (a path route); Arrived fires at the last one.</summary>
        public void MoveAlong(Vector3[] points)
        {
            if (points == null || points.Length == 0)
                return;
            route = points;
            routeIndex = 0;
            target = route[0];
            IsMoving = true;
        }

        private void Update()
        {
            if (IsMoving)
            {
                // Spend the whole frame's travel distance, carrying over past route corners.
                float step = moveSpeed * Time.unscaledDeltaTime;
                while (step > 0f)
                {
                    Vector3 delta = target - transform.position;
                    float dist = delta.magnitude;
                    var dir = new Vector3(delta.x, 0f, delta.z);
                    if (dir.sqrMagnitude > 0.0001f)
                        facing = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    if (dist > step)
                    {
                        transform.position += delta / dist * step;
                        break;
                    }
                    transform.position = target;
                    step -= dist;
                    if (++routeIndex < route.Length)
                    {
                        target = route[routeIndex];
                        continue;
                    }
                    IsMoving = false;
                    facing = Quaternion.Euler(0f, idleYaw, 0f);
                    Arrived?.Invoke();
                    break;
                }
            }

            visual.rotation = Quaternion.RotateTowards(visual.rotation, facing, turnSpeed * Time.unscaledDeltaTime);
            float bob = IsMoving ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * bobSpeed)) * bobHeight : 0f;
            visual.localPosition = visualBase + Vector3.up * bob;
            if (animator != null)
            {
                animator.SetFloat(SpeedId, IsMoving ? 1f : 0f, 0.05f, Time.unscaledDeltaTime);
                if (hasMotionSpeed)
                {
                    // Run clip cadence matched to the travel speed (the clip stride scales with the avatar's size).
                    float scale = Mathf.Max(0.01f, animator.transform.lossyScale.y);
                    animator.SetFloat(MotionSpeedId, Mathf.Min(maxMotionSpeed, moveSpeed / (runClipSpeed * scale)));
                }
            }
        }
    }
}
