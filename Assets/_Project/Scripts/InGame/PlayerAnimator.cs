using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>
    /// Drives the character Animator from PlayerMotor state (animation is kept out of the movement code).
    /// Base parameters: Speed (0 idle, walkBlend at walk speed, 1 at run speed; relative to a moving platform), Grounded, VerticalSpeed.
    /// Extended parameters (used when the controller has them, e.g. the Leaf Explorer controller): Dodge / Bounce triggers,
    /// KnockedBack / Dead / Clear bools and MotionSpeed (cadence multiplier at low speed).
    /// Root motion stays off; the motor owns all movement.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");
        private static readonly int VerticalSpeedId = Animator.StringToHash("VerticalSpeed");
        private static readonly int DodgeId = Animator.StringToHash("Dodge");
        private static readonly int BounceId = Animator.StringToHash("Bounce");
        private static readonly int KnockedBackId = Animator.StringToHash("KnockedBack");
        private static readonly int DeadId = Animator.StringToHash("Dead");
        private static readonly int ClearId = Animator.StringToHash("Clear");
        private static readonly int ClearStartId = Animator.StringToHash("ClearStart");
        private static readonly int MotionSpeedId = Animator.StringToHash("MotionSpeed");
        private static readonly int LandImpactId = Animator.StringToHash("LandImpact");

        [SerializeField] private Animator animator;
        [Tooltip("Smoothing time for the Speed parameter (seconds).")]
        [SerializeField, Min(0f)] private float speedDampTime = 0.05f;
        [Tooltip("Speed parameter value at walk speed (the Locomotion blend tree's walk threshold).")]
        [SerializeField, Range(0f, 1f)] private float walkBlend = 0.3f;
        [Tooltip("Optional face overlay (Leaf Explorer). Found automatically on the visual when empty.")]
        [SerializeField] private LeafExplorerFace face;
        [Header("Stride matching (foot slide)")]
        [Tooltip("Ground speed (world m/s) the Walk clip covers at playback speed 1.")]
        [SerializeField, Min(0.1f)] private float walkClipSpeed = 4.3f;
        [Tooltip("Ground speed (world m/s) the Run clip covers at playback speed 1.")]
        [SerializeField, Min(0.1f)] private float runClipSpeed = 5.4f;
        [Tooltip("Lowest locomotion playback speed (MotionSpeed) when barely moving.")]
        [SerializeField, Range(0.2f, 1f)] private float minMotionSpeed = 0.55f;
        [Tooltip("Highest locomotion playback speed; above this the feet slide a little instead of looking frantic.")]
        [SerializeField, Range(1f, 3f)] private float maxMotionSpeed = 1.8f;

        [Header("Stage clear pose (storyboard timeline, seconds after the goal)")]
        [Tooltip("Visual yaw at the end of the turn (about 195 = facing the camera, a little toward screen left, right hand on the camera side).")]
        [SerializeField] private float clearFacingYaw = 195f;
        [Tooltip("02 relief: eyes closed, relieved smile starts.")]
        [SerializeField, Min(0f)] private float reliefAt = 0.4f;
        [Tooltip("03 eye contact: the head turns first, the body follows. Bright smile.")]
        [SerializeField, Min(0f)] private float turnAt = 0.85f;
        [SerializeField, Min(0.05f)] private float turnDuration = 0.55f;
        [SerializeField, Min(0f)] private float brightAt = 0.9f;
        [Tooltip("04 V sign: the V hand replaces the right fist and sparkles appear.")]
        [SerializeField, Min(0f)] private float vSignAt = 1.5f;
        [Tooltip("05 clear: wink.")]
        [SerializeField, Min(0f)] private float winkAt = 2.0f;
        [Tooltip("06 result: satisfied smile.")]
        [SerializeField, Min(0f)] private float satisfiedAt = 3.0f;
        [Tooltip("How far the head leads the body turn (fraction of the remaining turn, clamped to 45 degrees).")]
        [SerializeField, Range(0f, 1f)] private float headLeadWeight = 0.6f;

        [Header("Landing recoil")]
        [Tooltip("Fall speed (m/s) at or below which the landing is the lightest recoil (LandImpact 0).")]
        [SerializeField, Min(0f)] private float landSoftSpeed = 10f;
        [Tooltip("Fall speed (m/s) at or above which the landing is the hardest recoil (LandImpact 1).")]
        [SerializeField, Min(0.1f)] private float landHardSpeed = 26f;
        [Tooltip("Ignore touchdowns slower than this fall speed (steps, slopes).")]
        [SerializeField, Min(0f)] private float minLandSpeed = 2.5f;

        /// <summary>Raised on a jump takeoff or an enemy-stomp bounce (squash and stretch hook).</summary>
        public event System.Action Takeoff;
        /// <summary>Raised at touchdown with the normalised impact (0 soft, 1 hard).</summary>
        public event System.Action<float> Landing;

        private PlayerMotor motor;
        private bool wasGrounded = true;
        private float peakFallSpeed;
        private bool extended;
        private ExplorerVSign vSign;
        private float clearTime = -1f;
        private float clearStartYaw;
        private bool vShown;
        private Transform headBone;
        private AnimatorUpdateMode savedUpdateMode;
        private bool dead, clear, airDeath;
        private string expression = "default";

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            Assert.IsNotNull(animator, "PlayerAnimator.animator is not assigned.");
            animator.applyRootMotion = false;
            extended = HasParameter(DodgeId);
            if (face == null)
                face = animator.GetComponentInChildren<LeafExplorerFace>(true);
            vSign = animator.GetComponentInChildren<ExplorerVSign>(true);
            foreach (var t in animator.GetComponentsInChildren<Transform>(true))
                if (t.name == "Head") { headBone = t; break; }
        }

        private void OnEnable()
        {
            motor = motor != null ? motor : GetComponent<PlayerMotor>();
            motor.Dodged += OnDodged;
            motor.Bounced += OnBounced;
            motor.Jumped += OnJumped;
        }

        private void OnDisable()
        {
            if (motor == null)
                return;
            motor.Dodged -= OnDodged;
            motor.Bounced -= OnBounced;
            motor.Jumped -= OnJumped;
        }

        /// <summary>The animated visual root (its position is at the character's feet).</summary>
        public Transform VisualRoot => animator != null ? animator.transform : null;

        /// <summary>Game over / death pose (hold until <see cref="ResetState"/>).</summary>
        public void PlayDeath()
        {
            dead = true;
            airDeath = false;
            clear = false;
            SetExpression("damaged");
        }

        /// <summary>Killed in mid-air: hurt pose while falling; call <see cref="PlayDeath"/> on landing.</summary>
        public void PlayAirDeath()
        {
            airDeath = true;
            clear = false;
            SetExpression("damaged");
        }

        /// <summary>Stage clear celebration (jump + cheer loop until <see cref="ResetState"/>).</summary>
        public void PlayClear()
        {
            clear = true;
            dead = false;
            clearTime = 0f;
            vShown = false;
            clearStartYaw = motor.FacingYaw;
            savedUpdateMode = animator.updateMode;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime; // the pose keeps living on the result screen (time scale 0)
            if (extended)
                animator.SetTrigger(ClearStartId); // one-shot entry (a bool Any State transition would restart the intro every loop)
            SetExpression("default");
        }

        /// <summary>Back to normal locomotion (respawn / stage start).</summary>
        public void ResetState()
        {
            dead = false;
            airDeath = false;
            clear = false;
            if (clearTime >= 0f)
                animator.updateMode = savedUpdateMode;
            clearTime = -1f;
            vShown = false;
            motor.ClearFacingYaw();
            if (vSign != null)
                vSign.Set(false);
            SetExpression("default");
        }

        private void OnDodged()
        {
            if (extended && !dead && !clear)
                animator.SetTrigger(DodgeId);
        }

        private void OnJumped() => Takeoff?.Invoke();

        private void OnBounced()
        {
            Takeoff?.Invoke();
            if (extended && !dead && !clear)
                animator.SetTrigger(BounceId);
        }

        private void Update()
        {
            Vector3 v = motor.Velocity - motor.PlatformVelocity;
            // The player keeps animating during a time stop (Time.deltaTime is 0 then).
            float dt = TimeStop.IsActive ? Time.unscaledDeltaTime : Time.deltaTime;
            float speedParam = SpeedParameter(Mathf.Abs(v.x));
            animator.SetFloat(SpeedId, speedParam, speedDampTime, dt);
            TrackLanding(v.y);
            animator.SetBool(GroundedId, motor.IsGrounded);
            animator.SetFloat(VerticalSpeedId, v.y);
            if (clear && clearTime >= 0f)
            {
                clearTime += Time.unscaledDeltaTime;
                float turn = Mathf.SmoothStep(0f, 1f, (clearTime - turnAt) / turnDuration);
                motor.SetFacingYaw(Mathf.LerpAngle(clearStartYaw, clearFacingYaw, turn));
                if (!vShown && clearTime >= vSignAt)
                {
                    vShown = true;
                    if (vSign != null)
                        vSign.Set(true);
                    ClearSparkle.Spawn(animator.transform.position + new Vector3(-0.8f, 2.0f, -0.2f));
                }
                string wanted = clearTime < reliefAt ? "default" : clearTime < brightAt ? "eye_close" : clearTime < winkAt ? "bright" : clearTime < satisfiedAt ? "wink" : "bright";
                SetExpression(wanted);
            }
            if (!extended)
                return;
            animator.SetBool(KnockedBackId, motor.IsKnockedBack || airDeath);
            animator.SetBool(DeadId, dead);
            animator.SetBool(ClearId, clear);
            float reference = speedParam <= walkBlend || walkBlend >= 1f
                ? walkClipSpeed
                : Mathf.Lerp(walkClipSpeed, runClipSpeed, (speedParam - walkBlend) / (1f - walkBlend));
            float cadence = Mathf.Clamp(Mathf.Abs(v.x) / reference, minMotionSpeed, maxMotionSpeed);
            animator.SetFloat(MotionSpeedId, cadence);
            UpdateExpression(v);
        }

        // Captures the fall speed and, at touchdown, picks the recoil strength (LandImpact) before Grounded flips.
        private void TrackLanding(float verticalSpeed)
        {
            bool grounded = motor.IsGrounded;
            if (!grounded)
                peakFallSpeed = Mathf.Max(peakFallSpeed, -verticalSpeed);
            else if (!wasGrounded)
            {
                if (peakFallSpeed >= minLandSpeed)
                {
                    float impact = Mathf.Clamp01(Mathf.InverseLerp(landSoftSpeed, landHardSpeed, peakFallSpeed));
                    if (extended)
                        animator.SetFloat(LandImpactId, impact);
                    Landing?.Invoke(impact);
                }
                peakFallSpeed = 0f;
            }
            wasGrounded = grounded;
        }

        // Head leads the turn toward the camera (applied after the Animator, so the clip stays direction independent).
        private void LateUpdate()
        {
            if (!clear || clearTime < 0f || headBone == null)
                return;
            float bodyYaw = animator.transform.eulerAngles.y;
            float remaining = Mathf.DeltaAngle(bodyYaw, clearFacingYaw);
            float envelope = Mathf.SmoothStep(0f, 1f, (clearTime - (turnAt - 0.1f)) / 0.15f);
            float lead = Mathf.Clamp(remaining * headLeadWeight, -45f, 45f) * envelope;
            headBone.rotation = Quaternion.AngleAxis(lead, Vector3.up) * headBone.rotation;
        }

        private void UpdateExpression(Vector3 v)
        {
            if (face == null || dead || clear || airDeath)
                return;
            string wanted = "default";
            if (motor.IsKnockedBack)
                wanted = "damaged";
            else if (motor.IsDodging)
                wanted = "angry";
            else if (!motor.IsGrounded)
                wanted = v.y > 0.5f ? "smile2" : (v.y < -4f ? "surprise" : "default");
            SetExpression(wanted);
        }

        private void SetExpression(string wanted)
        {
            if (face == null || wanted == expression)
                return;
            expression = wanted;
            face.SetExpression(wanted);
        }

        private bool HasParameter(int hash)
        {
            foreach (var p in animator.parameters)
                if (p.nameHash == hash)
                    return true;
            return false;
        }

        // Piecewise: 0..walk speed -> 0..walkBlend (walk clip), walk..run speed -> walkBlend..1 (run clip).
        private float SpeedParameter(float speed)
        {
            float walk = motor.MoveSpeed, run = motor.RunSpeed;
            if (walk <= 0f)
                return 0f;
            if (speed <= walk || run <= walk)
                return Mathf.Clamp01(speed / walk) * walkBlend;
            return Mathf.Lerp(walkBlend, 1f, Mathf.Clamp01((speed - walk) / (run - walk)));
        }
    }
}
