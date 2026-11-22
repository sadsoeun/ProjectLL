using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Title diorama extra: walks a monster visual back and forth between two local X positions, idling at each end.
    /// Drives the Stage01 visual Animator ("Speed") and turns the model toward its walk direction, slightly toward the camera.
    /// </summary>
    public sealed class TitleCritter : MonoBehaviour
    {
        [SerializeField] private float minX = -1f;
        [SerializeField] private float maxX = 1f;
        [SerializeField, Min(0f)] private float speed = 0.6f;
        [SerializeField, Min(0f)] private float idleSeconds = 2.5f;
        [Tooltip("Extra yaw toward the camera (-Z) so the face reads.")]
        [SerializeField, Range(0f, 80f)] private float faceCameraAngle = 35f;
        [Tooltip("When false the critter only idles in place, facing the camera.")]
        [SerializeField] private bool walks = true;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private Animator animator;
        private int direction = 1;
        private float idleUntil;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }
            idleUntil = Time.unscaledTime + Random.Range(0.5f, idleSeconds);
            Turn();
        }

        private void Update()
        {
            if (!walks)
                return;
            bool moving = Time.unscaledTime >= idleUntil;
            if (moving)
            {
                var p = transform.localPosition;
                p.x += direction * speed * Time.unscaledDeltaTime;
                if ((direction > 0 && p.x >= maxX) || (direction < 0 && p.x <= minX))
                {
                    p.x = Mathf.Clamp(p.x, minX, maxX);
                    direction = -direction;
                    idleUntil = Time.unscaledTime + idleSeconds;
                    moving = false;
                }
                transform.localPosition = p;
                Turn();
            }
            if (animator != null)
                animator.SetFloat(SpeedParam, moving ? speed : 0f);
        }

        private void Turn()
        {
            float yaw = walks ? direction * (90f + faceCameraAngle) : 180f;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
