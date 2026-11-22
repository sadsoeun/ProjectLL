using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>
    /// Side-scroller movement on a dynamic Rigidbody (X/Y only, Z frozen). Input is pushed in from
    /// PlayerInputReader (Update); all physics happens in FixedUpdate. Gravity is applied manually
    /// from jumpHeight / timeToApex so the jump is tunable by feel.
    /// Moving platforms: the motor treats its velocity relative to the platform it stands on, so the
    /// platform carries the player exactly (no friction involved) and a jump inherits its momentum.
    /// </summary>
    [DefaultExecutionOrder(-50)] // Awake must run before InGameController.Awake freezes the player
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("Horizontal")]
        [Tooltip("Walk speed. Level layouts are designed and validated against this value.")]
        [SerializeField, Min(0f)] private float moveSpeed = 7f;
        [Tooltip("Speed while the Run button is held. The cap chosen on the ground is kept through the jump.")]
        [SerializeField, Min(0f)] private float runSpeed = 10f;
        [SerializeField, Min(0f)] private float groundAcceleration = 70f;
        [SerializeField, Min(0f)] private float groundDeceleration = 80f;
        [SerializeField, Min(0f)] private float airAcceleration = 40f;

        [Header("Jump")]
        [SerializeField, Min(0.1f)] private float jumpHeight = 3.2f;
        [SerializeField, Min(0.05f)] private float timeToApex = 0.4f;
        [Tooltip("Extra gravity while falling.")]
        [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.6f;
        [Tooltip("Extra gravity while rising with the jump button released (short hop).")]
        [SerializeField, Min(1f)] private float lowJumpGravityMultiplier = 2.4f;
        [SerializeField, Min(1f)] private float maxFallSpeed = 25f;
        [Tooltip("A jump press is remembered this long (seconds) in case the player is not grounded yet.")]
        [SerializeField, Min(0f)] private float jumpRequestWindow = 0.1f;
        [Tooltip("Coyote time: a jump is still allowed this long (seconds) after walking or dashing off a ledge. 0 = off.")]
        [SerializeField, Min(0f)] private float coyoteTime = 0.1f;

        [Header("Bounce (enemy stomp)")]
        [Tooltip("Bounce height as a fraction of the full jump height when Jump is not held. Holding Jump gives a full jump.")]
        [SerializeField, Range(0.1f, 1f)] private float bounceHeightFraction = 0.6f;
        [Tooltip("Repeated bounces within this many seconds are treated as one (two enemies in one landing).")]
        [SerializeField, Min(0f)] private float bounceDebounce = 0.15f;

        [Header("Dodge (Shift)")]
        [Tooltip("Dash speed while dodging (m/s). The dodge always goes forward: the held direction, else the facing.")]
        [SerializeField, Min(0f)] private float dodgeSpeed = 17f;
        [Tooltip("Dodge length (seconds). The whole dodge counts as the perfect-dodge window.")]
        [SerializeField, Min(0.05f)] private float dodgeDuration = 0.3f;
        [Tooltip("Wait after a dodge ends before the next one (seconds).")]
        [SerializeField, Min(0f)] private float dodgeCooldown = 0.35f;
        [Tooltip("Allow one dodge per airtime (a straight, gravity-free dash).")]
        [SerializeField] private bool airDodge = true;

        [Header("Ground check")]
        [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.12f;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Visual")]
        [SerializeField] private Transform visual;
        [SerializeField, Min(0f)] private float turnSpeed = 1080f;
        [Header("Camera turn (the side camera looks along +Z; 180 degrees would face it)")]
        [Tooltip("Degrees the character turns toward the camera while moving or airborne (0 = pure profile).")]
        [SerializeField, Range(0f, 60f)] private float moveTurnAngle = 25f;
        [Tooltip("Degrees toward the camera when standing still on the ground (shows the face).")]
        [SerializeField, Range(0f, 80f)] private float idleTurnAngle = 45f;
        [Tooltip("Seconds standing still before easing to the idle angle.")]
        [SerializeField, Min(0f)] private float idleTurnDelay = 0.35f;

        private Rigidbody body;
        private CapsuleCollider capsule;
        private float moveInput;
        private bool jumpHeld;
        private bool runHeld;
        private float speedCap; // walk or run speed, latched while airborne
        private float jumpRequestUntil = -1f;
        // Coyote time: refreshed every grounded step; cleared by anything that leaves the ground on purpose
        // (jump, bounce, knockback, air dodge, teleport), so only walking / dashing off a ledge keeps it.
        private float coyoteUntil = -1f;
        private bool controlEnabled = true;
        private bool autoMoveActive;
        private float autoMoveInput;
        private float EffectiveInput => autoMoveActive ? autoMoveInput : moveInput;
        private bool frozen;
        private float facingYaw = 90f;           // travel direction: +90 = +X, -90 = -X
        private bool yawOverride;
        private float overrideYaw;
        private float idleTimer;

        private Collider lastGroundCollider;
        private IPlatformMotion lastGroundMover;
        private IceSurface lastGroundIce;
        private BounceSurface lastGroundBounce;
        private CrumbleSurface lastGroundCrumble;
        private bool noLowJumpGravity; // set by an unheld bounce so the fixed-height arc is not cut short
        private float lastBounceTime = -100f;
        private float stepStartFeetY;

        // Motor-local clock: advances with every physics step, including the manual steps taken while the world is
        // stopped (TimeStop, Time.timeScale = 0). Jump buffering, bounce debounce and dodge timing all use it.
        private float clock;
        private bool manualStepping;

        private bool dodgeRequested;
        private float dodgeTimer;      // > 0 while dodging
        private float dodgeDirection;  // -1 / +1
        private float dodgeReadyAt;
        private bool airDodgeUsed;

        private float knockbackTimer;  // > 0 while pushed back (no steering, no jump)

        public bool IsGrounded { get; private set; }

        /// <summary>True while a <see cref="Knockback"/> is pushing the player.</summary>
        public bool IsKnockedBack => knockbackTimer > 0f;

        /// <summary>True during a dodge (the perfect-dodge window).</summary>
        public bool IsDodging => dodgeTimer > 0f;

        /// <summary>0..1 from the start of a dodge to the moment the next one is allowed (1 = the cooldown is over).</summary>
        public float DodgeCooldownProgress
        {
            get
            {
                float total = dodgeDuration + dodgeCooldown;
                return total <= 0f ? 1f : Mathf.Clamp01(1f - (dodgeReadyAt - clock) / total);
            }
        }

        /// <summary>Airborne with the air dodge spent (or not allowed): no dodge until she lands.</summary>
        public bool DodgeBlockedInAir => !IsGrounded && (!airDodge || airDodgeUsed);

        /// <summary>A dodge started.</summary>
        public event System.Action Dodged;

        /// <summary>A jump started: from the ground or within the coyote time (not stomp bounces).</summary>
        public event System.Action Jumped;
        /// <summary>Touched down after falling at least <see cref="landEventMinFallSpeed"/>.</summary>
        public event System.Action Landed;
        /// <summary>Stomp bounce applied.</summary>
        public event System.Action Bounced;

        [Tooltip("Minimum downward speed (m/s) for a touchdown to raise Landed (filters small steps and slopes).")]
        [SerializeField, Min(0f)] private float landEventMinFallSpeed = 4f;
        private bool wasGrounded;
        private float lastFallSpeed;
        public Vector3 Velocity => body.linearVelocity;
        public float MoveSpeed => moveSpeed;
        public float RunSpeed => Mathf.Max(runSpeed, moveSpeed);
        /// <summary>Velocity of the moving platform the player stands on (zero otherwise).</summary>
        public Vector3 PlatformVelocity => IsGrounded && lastGroundMover != null ? lastGroundMover.Velocity : Vector3.zero;

        /// <summary>Resolves the player from a collider hit by a trigger (no allocation).</summary>
        public static bool TryGetFromCollider(Collider other, out PlayerMotor motor)
        {
            motor = null;
            var rb = other.attachedRigidbody;
            return rb != null && rb.TryGetComponent(out motor);
        }

        /// <summary>World Y of the lowest point of the capsule.</summary>
        public float FeetY => transform.position.y + capsule.center.y - capsule.height * 0.5f;

        /// <summary>
        /// Feet Y at the start of the current physics step. PhysX detects trigger overlaps from the poses
        /// before integration, so by the time OnTrigger* runs the body has already moved one more step;
        /// stomp checks use this to judge where the player was when the overlap was found.
        /// </summary>
        public float StepStartFeetY => stepStartFeetY;

        /// <summary>True right after a bounce (same landing).</summary>
        public bool RecentlyBounced => clock - lastBounceTime < bounceDebounce;

        /// <summary>
        /// Launch speed (m/s) that lifts the player <paramref name="height"/> metres above the launch point (rising gravity).
        /// Used by knockbacks that must clear an obstacle.
        /// </summary>
        public float LaunchSpeedForHeight(float height) => Mathf.Sqrt(2f * Gravity * Mathf.Max(0f, height));

        /// <summary>
        /// Seconds a ballistic hop launched upward at <paramref name="launchSpeed"/> stays in the air until it is
        /// <paramref name="dropBelowStart"/> metres below the launch point (rising gravity, then the fall multiplier).
        /// </summary>
        public float AirTimeFor(float launchSpeed, float dropBelowStart = 0f)
        {
            float g = Gravity;
            float up = Mathf.Max(0f, launchSpeed) / g;
            float apex = launchSpeed * launchSpeed / (2f * g);
            float down = Mathf.Sqrt(2f * Mathf.Max(0f, apex + dropBelowStart) / (g * fallGravityMultiplier));
            return up + down;
        }

        private float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);
        private float JumpVelocity => 2f * jumpHeight / timeToApex;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            Assert.IsNotNull(visual, "PlayerMotor.visual is not assigned.");

            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            CancelDodge();
            airDodgeUsed = false;
            speedCap = moveSpeed;
            knockbackTimer = 0f;
        }

        /// <summary>Horizontal axis (-1..1) and whether the jump button is held. Called every Update.</summary>
        public void SetInput(float horizontal, bool jumpButtonHeld) => SetInput(horizontal, jumpButtonHeld, false);

        /// <summary>Same as above plus the Run button (walk speed when released).</summary>
        public void SetInput(float horizontal, bool jumpButtonHeld, bool runButtonHeld)
        {
            moveInput = Mathf.Clamp(horizontal, -1f, 1f);
            jumpHeld = jumpButtonHeld;
            runHeld = runButtonHeld;
        }

        public void RequestJump()
        {
            jumpRequestUntil = clock + jumpRequestWindow;
        }

        /// <summary>
        /// Enemy-stomp bounce: ~bounceHeightFraction of a full jump, a full jump while Jump is held.
        /// Ignored when a bounce already happened this landing. Reuses the jump math.
        /// </summary>
        public void Bounce()
        {
            if (frozen || RecentlyBounced)
                return;
            lastBounceTime = clock;
            Vector3 v = body.linearVelocity;
            float jv = JumpVelocity;
            v.y = jumpHeld ? jv : jv * Mathf.Sqrt(bounceHeightFraction); // height ~ v^2
            noLowJumpGravity = !jumpHeld;
            body.linearVelocity = v;
            IsGrounded = false;
            jumpRequestUntil = -1f;
            coyoteUntil = -1f;
            Bounced?.Invoke();
        }

        /// <summary>
        /// Harmless push (boss body contact): flings the player along <paramref name="directionX"/> with a small hop.
        /// Steering and jumping are off for <paramref name="seconds"/>; gravity still applies.
        /// </summary>
        public void Knockback(float directionX, float speedX, float speedY, float seconds)
        {
            if (frozen)
                return;
            CancelDodge();
            body.linearVelocity = new Vector3(Mathf.Sign(directionX) * speedX, speedY, 0f);
            IsGrounded = false;
            jumpRequestUntil = -1f;
            coyoteUntil = -1f;
            noLowJumpGravity = true; // the hop keeps its height whether Jump is held or not
            knockbackTimer = seconds;
        }

        /// <summary>
        /// Raises the player so the feet are at least at <paramref name="feetY"/> (a rising pillar carrying the player). Never lowers;
        /// a downward velocity is cancelled so the player does not sink back into the platform.
        /// </summary>
        public void LiftFeetTo(float feetY)
        {
            if (frozen)
                return;
            float current = body.position.y + capsule.center.y - capsule.height * 0.5f;
            if (feetY <= current)
                return;
            var p = body.position;
            p.y += feetY - current;
            body.position = p;
            if (!body.isKinematic && body.linearVelocity.y < 0f)
            {
                var v = body.linearVelocity;
                v.y = 0f;
                body.linearVelocity = v;
            }
        }

        /// <summary>False ignores all input (dead, cleared, ready); physics keeps running.</summary>
        public void SetControl(bool enabled)
        {
            controlEnabled = enabled;
            if (!enabled)
            {
                moveInput = 0f;
                jumpHeld = false;
                jumpRequestUntil = -1f;
                CancelDodge();
            }
        }

        /// <summary>Scripted walk (-1..1, walk speed) that works while control is off (stage-clear walk-in). Physics and gravity stay normal.</summary>
        public void SetAutoMove(float horizontal)
        {
            autoMoveActive = true;
            autoMoveInput = Mathf.Clamp(horizontal, -1f, 1f);
        }

        public void ClearAutoMove()
        {
            autoMoveActive = false;
            autoMoveInput = 0f;
        }

        /// <summary>Stops all physics (kinematic). Velocity is zeroed first to avoid kinematic-velocity warnings.</summary>
        public void Freeze(bool value)
        {
            if (value == frozen)
                return;
            if (value)
            {
                body.linearVelocity = Vector3.zero;
                CancelDodge();
                body.isKinematic = true;
            }
            else
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
            }
            frozen = value;
        }

        /// <summary>Sets the visual facing (yaw, degrees: 90 = +X, -90 = -X, 180 = toward the camera). Used by the clear pose.</summary>
        public void SetFacingYaw(float yaw)
        {
            yawOverride = true;
            overrideYaw = yaw;
        }

        /// <summary>Back to the normal travel facing after <see cref="SetFacingYaw"/>.</summary>
        public void ClearFacingYaw() => yawOverride = false;

        /// <summary>Current visual yaw in degrees (including the camera turn).</summary>
        public float FacingYaw => visual != null ? visual.localEulerAngles.y : facingYaw;

        public void TeleportTo(Vector3 position)
        {
            position.z = 0f;
            transform.position = position;
            body.position = position;
            stepStartFeetY = FeetY;
            if (!body.isKinematic)
                body.linearVelocity = Vector3.zero;
            IsGrounded = false;
            jumpRequestUntil = -1f;
            coyoteUntil = -1f;
            noLowJumpGravity = false;
            lastGroundCollider = null;
            lastGroundMover = null;
            lastGroundIce = null;
            lastGroundBounce = null;
            lastGroundCrumble = null;
            speedCap = moveSpeed;
            knockbackTimer = 0f;
        }

        /// <summary>Dodge (Shift): a short forward dash. Buffered until the next physics step.</summary>
        public void RequestDodge()
        {
            dodgeRequested = true;
        }

        /// <summary>
        /// While true the motor no longer runs in FixedUpdate; <see cref="TimeStop"/> calls <see cref="ManualStep"/> every
        /// frame on unscaled time and simulates physics itself (the rest of the world is frozen by Time.timeScale = 0).
        /// </summary>
        public void SetManualStepping(bool value)
        {
            if (value == manualStepping)
                return;
            manualStepping = value;
            // Per-frame manual steps need no interpolation (and interpolation would lag behind a frozen fixed clock).
            body.interpolation = value ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }

        /// <summary>One physics step of the motor with the given duration (TimeStop only).</summary>
        public void ManualStep(float dt)
        {
            if (manualStepping)
                Step(dt);
        }

        private void CancelDodge()
        {
            dodgeTimer = 0f;
            dodgeRequested = false;
        }

        private void Update()
        {
            if (Mathf.Abs(EffectiveInput) > 0.1f && !IsDodging)
                facingYaw = EffectiveInput > 0f ? 90f : -90f;
            float frameDt = manualStepping ? Time.unscaledDeltaTime : Time.deltaTime;
            float yaw;
            if (yawOverride)
                yaw = overrideYaw;
            else
            {
                bool still = IsGrounded && !IsDodging && !IsKnockedBack && Mathf.Abs(EffectiveInput) <= 0.1f && Mathf.Abs(body.linearVelocity.x) < 0.5f;
                idleTimer = still ? idleTimer + frameDt : 0f;
                float angle = still && idleTimer >= idleTurnDelay ? idleTurnAngle : moveTurnAngle;
                yaw = (facingYaw > 0f ? 1f : -1f) * (90f + angle);
            }
            var target = Quaternion.Euler(0f, yaw, 0f);
            visual.localRotation = Quaternion.RotateTowards(visual.localRotation, target, turnSpeed * frameDt);
        }

        private void FixedUpdate()
        {
            if (!manualStepping)
                Step(Time.fixedDeltaTime);
        }

        private void Step(float dt)
        {
            stepStartFeetY = body.position.y + capsule.center.y - capsule.height * 0.5f;
            if (frozen)
                return;
            clock += dt;

            Vector3 v = body.linearVelocity;

            bool hasGround = CheckGround(out IPlatformMotion platform);
            // While the world is stopped platforms do not move, so they must not carry the player either.
            Vector3 pv = platform != null && !manualStepping ? platform.Velocity : Vector3.zero;

            // Vertical speed relative to the ground we stand on (a rising lift must still count as ground).
            IsGrounded = hasGround && (v.y - pv.y) <= 0.1f;
            if (IsGrounded && !wasGrounded && lastFallSpeed >= landEventMinFallSpeed)
                Landed?.Invoke();
            if (IsGrounded)
            {
                airDodgeUsed = false;
                coyoteUntil = clock + coyoteTime;
                if (lastGroundCrumble != null)
                    lastGroundCrumble.NotifyStoodOn(body.position.x); // crumbling footing starts its countdown
            }
            // Bounce pad: touching down on it launches the player straight up (no jump press needed).
            if (IsGrounded && lastGroundBounce != null && knockbackTimer <= 0f)
            {
                CancelDodge();
                v.y = LaunchSpeedForHeight(lastGroundBounce.bounceHeight);
                lastGroundBounce.NotifyLaunch();
                IsGrounded = false;
                jumpRequestUntil = -1f;
                coyoteUntil = -1f;
                noLowJumpGravity = true; // the launch keeps its height whether Jump is held or not
                lastBounceTime = clock;
                Bounced?.Invoke();
            }
            bool onPlatform = IsGrounded && platform != null;

            if (onPlatform)
                v.x -= pv.x; // work in the platform's frame

            // Dodge start: forward = the held direction, else the facing direction.
            if (dodgeRequested)
            {
                dodgeRequested = false;
                if (controlEnabled && !IsDodging && knockbackTimer <= 0f && clock >= dodgeReadyAt && (IsGrounded || (airDodge && !airDodgeUsed)))
                {
                    dodgeDirection = Mathf.Abs(moveInput) > 0.1f ? Mathf.Sign(moveInput) : (facingYaw > 0f ? 1f : -1f);
                    facingYaw = dodgeDirection > 0f ? 90f : -90f;
                    dodgeTimer = dodgeDuration;
                    dodgeReadyAt = clock + dodgeDuration + dodgeCooldown;
                    if (!IsGrounded)
                    {
                        airDodgeUsed = true;
                        coyoteUntil = -1f; // an air dash is a deliberate air move: no late jump out of it
                    }
                    Dodged?.Invoke();
                }
            }

            // Pick walk/run on the ground only, so a jump keeps the speed it started with.
            if (IsGrounded)
                speedCap = runHeld ? RunSpeed : moveSpeed;

            bool knockedBack = knockbackTimer > 0f;
            if (knockedBack)
                knockbackTimer -= dt;

            bool dodging = IsDodging;
            if (knockedBack)
            {
                // pushed: keep the push speed (no steering); dodging is cancelled by Knockback
            }
            else if (dodging)
            {
                v.x = dodgeDirection * dodgeSpeed; // fixed dash speed, ice or not
                dodgeTimer -= dt;
                if (dodgeTimer <= 0f)
                {
                    dodgeTimer = 0f;
                    v.x = dodgeDirection * speedCap; // leave the dash at normal speed, not with a stop
                }
            }
            else
            {
                float targetX = autoMoveActive ? autoMoveInput * speedCap : (controlEnabled ? moveInput * speedCap : 0f);
                bool pressing = Mathf.Abs(targetX) > 0.01f;
                float accel = IsGrounded ? (pressing ? groundAcceleration : groundDeceleration) : airAcceleration;
                if (IsGrounded && lastGroundIce != null)
                    accel *= pressing ? lastGroundIce.accelerationScale : lastGroundIce.decelerationScale; // slippery ice
                v.x = Mathf.MoveTowards(v.x, targetX, accel * dt);
            }
            v.z = 0f;

            if (onPlatform)
                v.x += pv.x;

            if (v.y <= 0f)
                noLowJumpGravity = false;

            if (dodging && !IsGrounded)
            {
                v.y = 0f; // air dodge: a straight dash, no gravity
            }
            else
            {
                float g = Gravity;
                if (v.y < 0f)
                    g *= fallGravityMultiplier;
                else if (v.y > 0f && !jumpHeld && !noLowJumpGravity)
                    g *= lowJumpGravityMultiplier;
                v.y = Mathf.Max(v.y - g * dt, -maxFallSpeed);
            }

            if (onPlatform)
                v.y = pv.y - 0.5f; // ride the platform; the small press keeps contact on descending lifts

            // Grounded, or within the coyote time after leaving a ledge without jumping (strictly later than the last grounded step).
            bool canJump = IsGrounded || (coyoteTime > 0f && clock <= coyoteUntil);
            if (controlEnabled && !knockedBack && canJump && clock <= jumpRequestUntil)
            {
                if (IsDodging)
                {
                    CancelDodge(); // jump out of a ground dodge, keeping its speed
                    speedCap = Mathf.Max(speedCap, Mathf.Abs(v.x - pv.x));
                }
                v.y = JumpVelocity + pv.y; // inherit the platform's vertical momentum
                IsGrounded = false;
                jumpRequestUntil = -1f;
                coyoteUntil = -1f;
                Jumped?.Invoke();
            }

            body.linearVelocity = v;
            wasGrounded = IsGrounded;
            lastFallSpeed = Mathf.Max(0f, -(body.linearVelocity.y - pv.y));
        }

        private bool CheckGround(out IPlatformMotion mover)
        {
            mover = null;
            float radius = capsule.radius * 0.9f;
            Vector3 origin = transform.TransformPoint(capsule.center);
            float distance = capsule.height * 0.5f - radius + groundCheckDistance;
            // Casts ignore colliders they start inside, so the player's own collider is skipped.
            if (!Physics.SphereCast(origin, radius, Vector3.down, out RaycastHit hit, distance, groundMask, QueryTriggerInteraction.Ignore)
                || hit.normal.y <= 0.5f)
            {
                lastGroundCollider = null;
                lastGroundMover = null;
                lastGroundIce = null;
                lastGroundBounce = null;
                lastGroundCrumble = null;
                return false;
            }

            if (hit.collider != lastGroundCollider)
            {
                lastGroundCollider = hit.collider;
                lastGroundMover = hit.collider.GetComponentInParent<IPlatformMotion>();
                lastGroundIce = hit.collider.GetComponent<IceSurface>();
                lastGroundBounce = hit.collider.GetComponent<BounceSurface>();
                lastGroundCrumble = hit.collider.GetComponent<CrumbleSurface>();
            }
            mover = lastGroundMover;
            return true;
        }
    }
}
