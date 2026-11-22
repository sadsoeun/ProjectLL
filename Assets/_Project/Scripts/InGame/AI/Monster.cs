using Unity.Behavior;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Body of every monster. Decisions live in the monster's Behavior graph (BehaviorGraphAgent on the same object):
    /// the graph nodes (MonsterNodes.cs) only ask this component to walk, turn and look. Physics, senses, contact
    /// and the stage lifecycle stay here.
    ///
    /// Data: the "Table data" fields carry the same names as the columns of Data/Tables/MonsterTable.csv
    /// (contract: Docs/monster-table-schema.md) and are written by Tools/MyGame/Apply Monster Table, so edit the
    /// CSV, not these fields. Stage placement may override patrolRange / moveSpeed per instance.
    ///
    /// Movement: kinematic ground walker on a fixed Y. The requested velocity is applied in FixedUpdate and is
    /// cancelled at walls and ledges, so a monster never walks off its floor.
    /// Contact: falling feet above the top on a stompable monster = stomp (1 damage, player bounces);
    /// any other contact = PlayerHit when contactKills is set. Shots (FireAt -> MonsterProjectile) always kill.
    /// Defeated monsters stay defeated after deaths and return only on Retry.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider), typeof(BehaviorGraphAgent))]
    public sealed partial class Monster : MonoBehaviour, IStageElement
    {
        public enum Mode { Patrol, Chase, Reset }

        /// <summary>Name of the GameObject blackboard variable the graph reads the target from.</summary>
        public const string TargetVariable = "Target";

        [Header("Table data (written by Tools/MyGame/Apply Monster Table)")]
        [SerializeField] private int monsterId;
        [SerializeField, Min(1)] private int hp = 1;
        [Tooltip("Patrol speed (m/s).")]
        [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
        [SerializeField, Min(0f)] private float chaseSpeed;
        [Tooltip("The patrol zone runs from the start X to start X + patrolRange.")]
        [SerializeField, Min(0f)] private float patrolRange = 6f;
        [Tooltip("Spotting distance while patrolling. 0 = blind.")]
        [SerializeField, Min(0f)] private float viewDistance;
        [Tooltip("Half of the view cone in the XY plane, measured from the facing direction.")]
        [SerializeField, Range(0f, 180f)] private float viewHalfAngle;
        [Tooltip("While chasing, the target is lost beyond this distance.")]
        [SerializeField, Min(0f)] private float loseDistance;
        [Tooltip("Data only: no monster jumps yet (the table validator warns if an implemented row enables it).")]
        [SerializeField] private bool canJump;
        [SerializeField, Min(0f)] private float jumpSpeed;
        [SerializeField, Min(0f)] private float jumpInterval;
        [Tooltip("Enables the attack in the \"attack\" column (Monster.Attack.cs).")]
        [SerializeField] private bool canAttack;
        [SerializeField, Min(0f)] private float attackRange;
        [SerializeField, Min(0f)] private float attackInterval;
        [SerializeField, Min(0)] private int attackDamage;
        [Tooltip("Landing on it from above damages it. Off = the stomp hurts the player instead.")]
        [SerializeField] private bool stompable = true;
        [Tooltip("Touching it (other than a stomp) hurts the player.")]
        [SerializeField] private bool contactKills = true;
        [SerializeField, Min(0)] private int rewardCoins;

        [Header("Behavior tuning (prefab)")]
        [Tooltip("Seconds the monster stands still after turning around at the end of its zone.")]
        [SerializeField, Min(0f)] private float turnPause = 0.4f;
        [Tooltip("Stops closing in when the target is this close horizontally (avoids jitter under the player).")]
        [SerializeField, Min(0f)] private float chaseStopDistance = 0.2f;
        [Tooltip("The sight line may be blocked this long before the chase gives up (a jump over a crate is not a loss).")]
        [SerializeField, Min(0f)] private float loseSightGrace = 0.3f;
        [Tooltip("Eye height above the feet; the sight line starts here.")]
        [SerializeField, Min(0f)] private float eyeHeight = 0.65f;
        [Tooltip("Layers that block sight and movement. Triggers are always ignored.")]
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Header("Hit box / stomp")]
        [Tooltip("Height of the hit box above the feet; the stomp test compares the player's feet with this.")]
        [SerializeField, Min(0.1f)] private float topHeight = 1f;
        [Tooltip("The feet may be this far below the monster top and still count as a stomp.")]
        [SerializeField, Min(0f)] private float stompTolerance = 0.5f;
        [Tooltip("Half width of the body; walls are probed from here.")]
        [SerializeField, Min(0.05f)] private float halfWidth = 0.45f;
        [Tooltip("How far ahead of the body the floor is probed for a ledge.")]
        [SerializeField, Min(0f)] private float ledgeLookAhead = 0.15f;
        [Tooltip("After a stomp that does not defeat it, touching it from the side is harmless this long (no unfair hit right after a bounce). Stomps from above still count.")]
        [SerializeField, Min(0f)] private float hurtInvulnerability = 0.6f;

        [Header("Visual")]
        [SerializeField] private Transform visual;
        [Tooltip("Renderers that switch to the alert material while chasing (the eyes).")]
        [SerializeField] private Renderer[] alertRenderers;
        [SerializeField] private Material alertMaterial;
        [SerializeField, Min(0.01f)] private float squashSeconds = 0.35f;
        [SerializeField, Min(1f)] private float squashWiden = 1.35f;
        [Tooltip("Optional burst played when defeated. Must not be a child of the visual.")]
        [SerializeField] private ParticleSystem defeatBurst;
        [Tooltip("Optional animated model (params: float Speed, triggers Hit / Defeat). When set, its clips replace the scale squash.")]
        [SerializeField] private Animator animator;
        [Tooltip("Extra yaw toward the side camera so the face reads in three-quarter view (0 = pure profile).")]
        [SerializeField, Range(0f, 60f)] private float faceCameraAngle;
        [Tooltip("With an animator: seconds the Defeat clip plays before the monster is hidden.")]
        [SerializeField, Min(0f)] private float defeatHideDelay = 1f;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int HitParam = Animator.StringToHash("Hit");
        private static readonly int DefeatParam = Animator.StringToHash("Defeat");

        [Header("Projectile")]
        [Tooltip("Shot used by FireAt. Empty = this monster cannot shoot.")]
        [SerializeField] private MonsterProjectile projectilePrefab;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 6f;

        [Header("Target")]
        [Tooltip("Optional fixed target (test scenes). When empty, the stage player is used.")]
        [SerializeField] private GameObject targetOverride;
        [SerializeField] private bool logModeChanges;

        private StageRoot stage;
        private Rigidbody body;
        private Collider hitBox;
        private BehaviorGraphAgent agent;
        private Vector3 start;
        private float desiredSpeedX;
        private int facing = 1;
        private int currentHp;
        private float invulnerableUntil = -1f;
        private bool defeated;
        private float defeatedAt;
        private Vector3 visualScale = Vector3.one;
        private Material[] normalMaterials;
        private GameObject boundTarget;
        private readonly System.Collections.Generic.List<MonsterProjectile> projectiles = new();

        public int MonsterId => monsterId;
        public Mode CurrentMode { get; private set; } = Mode.Patrol;
        public bool IsDefeated => defeated;
        public int CurrentHp => currentHp;
        public int Facing => facing;
        public float PatrolMinX => start.x;
        public float PatrolMaxX => start.x + patrolRange;
        public float MoveSpeed => moveSpeed;
        public float ChaseSpeed => chaseSpeed;
        public float ChaseStopDistance => chaseStopDistance;
        public float TurnPause => turnPause;
        public float LoseSightGrace => loseSightGrace;
        /// <summary>True when the last physics step could not move forward (wall or ledge ahead).</summary>
        public bool BlockedAhead { get; private set; }

        private Vector3 Eye => body.position + Vector3.up * eyeHeight;
        private bool Invulnerable => Time.time < invulnerableUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            hitBox = GetComponent<Collider>();
            agent = GetComponent<BehaviorGraphAgent>();
            start = transform.position;
            currentHp = hp;
            if (visual != null)
                visualScale = visual.localScale;
            if (alertRenderers != null)
            {
                normalMaterials = new Material[alertRenderers.Length];
                for (int i = 0; i < alertRenderers.Length; i++)
                    normalMaterials[i] = alertRenderers[i] != null ? alertRenderers[i].sharedMaterial : null;
            }
            InitMovement();
            ResetAttack();
        }

        public void Bind(StageRoot owner) => stage = owner;

        public void ResetForRespawn()
        {
            ClearProjectiles();
            if (defeated)
            {
                FinishDefeat(); // stays defeated for the whole attempt
                return;
            }
            ResetToStart();
        }

        public void ResetForAttempt()
        {
            ClearProjectiles();
            defeated = false;
            gameObject.SetActive(true);
            hitBox.enabled = true;
            if (visual != null)
                visual.gameObject.SetActive(true);
            if (defeatBurst != null)
                defeatBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            agent.enabled = true;
            ResetToStart();
        }

        /// <summary>Back to the start pose with full HP; the graph starts again from its first node.</summary>
        private void ResetToStart()
        {
            desiredSpeedX = 0f;
            BlockedAhead = false;
            currentHp = hp;
            ResetAttack();
            invulnerableUntil = -1f;
            if (visual != null)
                visual.localScale = visualScale;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.Rebind(); // back to Idle with pending triggers cleared
                animator.Update(0f);
            }
            transform.position = start;
            body.position = start;
            Face(1);
            SetMode(Mode.Patrol);
            ResetMovement();
            if (agent.enabled && Application.isPlaying)
                agent.Restart();
        }

        private void Update()
        {
            BindTarget();
            if (defeated)
                UpdateSquash();
            else if (!AttackBusy && visual != null && visual.localScale != visualScale)
                UpdateHurtPulse();
            UpdateMovement();
            UpdateAttack();
            if (animator != null && !defeated && animator.isActiveAndEnabled)
                animator.SetFloat(SpeedParam, AttackBusy ? AttackAnimatorSpeed : Mathf.Abs(desiredSpeedX));
        }

        /// <summary>Pushes the current target into the graph blackboard whenever it changes.</summary>
        private void BindTarget()
        {
            if (!agent.enabled)
                return; // a hidden burrower / dormant graph is bound again when it is enabled
            GameObject target = targetOverride != null ? targetOverride
                : stage != null && stage.Player != null ? stage.Player.gameObject : null;
            if (target == boundTarget)
                return;
            if (agent.SetVariableValue(TargetVariable, target))
                boundTarget = target;
        }

        // ---------------------------------------------------------------- commands used by the graph nodes

        /// <summary>Walks in the given direction (-1 / +1) at the given speed until told otherwise.</summary>
        public void Walk(int direction, float speed)
        {
            if (defeated || MotionLocked)
                return;
            if (direction != 0)
                Face(direction);
            desiredSpeedX = direction == 0 ? 0f : Mathf.Sign(direction) * speed;
        }

        public void Stop() => desiredSpeedX = 0f;

        /// <summary>Fires one shot from the eye toward the target's body centre. False when it cannot shoot.</summary>
        public bool FireAt(GameObject target)
        {
            if (defeated || projectilePrefab == null || !TryGetAimPoint(target, out Vector3 aim))
                return false;
            Vector3 dir = aim - Eye;
            dir.z = 0f;
            if (dir.sqrMagnitude < 0.0001f)
                return false;
            Face(dir.x >= 0f ? 1 : -1);
            var shot = Instantiate(projectilePrefab, Eye, Quaternion.identity, transform.parent);
            shot.Launch(this, stage, dir, projectileSpeed);
            projectiles.Add(shot);
            return true;
        }

        /// <summary>Called by a shot that removes itself.</summary>
        public void ForgetProjectile(MonsterProjectile shot) => projectiles.Remove(shot);

        private void ClearProjectiles()
        {
            var live = projectiles.ToArray();
            projectiles.Clear();
            foreach (var shot in live)
            {
                if (shot != null)
                    Destroy(shot.gameObject);
            }
        }

        public void Face(int direction)
        {
            if (direction == 0 || (AttackBusy && !facingUnlocked))
                return;
            int next = direction > 0 ? 1 : -1;
            if (next != facing)
                BlockedAhead = false; // the probe result was for the other direction
            facing = next;
            if (visual != null)
                visual.localRotation = Quaternion.Euler(0f, facing * (90f + faceCameraAngle), 0f);
        }

        public void SetMode(Mode mode)
        {
            if (mode == CurrentMode)
                return;
            CurrentMode = mode;
            if (alertRenderers != null && alertMaterial != null)
            {
                for (int i = 0; i < alertRenderers.Length; i++)
                {
                    if (alertRenderers[i] != null)
                        alertRenderers[i].sharedMaterial = mode == Mode.Chase ? alertMaterial : normalMaterials[i];
                }
            }
            if (logModeChanges)
                Debug.Log($"[Monster] {name}: {mode} (t={Time.time:F2}, x={transform.position.x:F2})", this);
        }

        // ---------------------------------------------------------------- senses

        /// <summary>Patrol check: inside the view distance, inside the view cone, and an unblocked sight line.</summary>
        public bool CanSpot(GameObject target)
        {
            if (viewDistance <= 0f || !TryGetAimPoint(target, out Vector3 aim))
                return false;
            Vector3 toTarget = aim - Eye;
            toTarget.z = 0f;
            if (toTarget.sqrMagnitude > viewDistance * viewDistance)
                return false;
            if (Vector3.Angle(Vector3.right * facing, toTarget) > viewHalfAngle)
                return false;
            return HasLineOfSight(target, aim);
        }

        /// <summary>Chase check: inside the lose distance and an unblocked sight line (no cone; the monster faces the target).</summary>
        public bool CanTrack(GameObject target)
        {
            if (!TryGetAimPoint(target, out Vector3 aim))
                return false;
            Vector3 toTarget = aim - Eye;
            toTarget.z = 0f;
            if (toTarget.sqrMagnitude > loseDistance * loseDistance)
                return false;
            return HasLineOfSight(target, aim);
        }

        private static bool TryGetAimPoint(GameObject target, out Vector3 aim)
        {
            aim = default;
            if (target == null || !target.activeInHierarchy)
                return false;
            // Aim at the body centre, not the pivot (the player pivot is the capsule centre, others may be the feet).
            aim = target.TryGetComponent(out Collider c) && c.enabled ? c.bounds.center : target.transform.position;
            return true;
        }

        /// <summary>The first solid collider on the eye-to-target line must belong to the target.</summary>
        private bool HasLineOfSight(GameObject target, Vector3 aim)
        {
            if (!Physics.Linecast(Eye, aim, out RaycastHit hit, obstacleMask, QueryTriggerInteraction.Ignore))
                return true;
            return IsPartOf(hit.collider, target);
        }

        private static bool IsPartOf(Collider c, GameObject target)
        {
            var rb = c.attachedRigidbody;
            Transform t = rb != null ? rb.transform : c.transform;
            return t == target.transform || t.IsChildOf(target.transform);
        }

        // ---------------------------------------------------------------- physics

        private void FixedUpdate()
        {
            if (defeated)
                return;
            if (movement == MonsterMovement.Fly)
            {
                FlyFixedStep();
                return;
            }
            if (AttackFixedStep())
                return;
            float step = desiredSpeedX * Time.fixedDeltaTime;
            BlockedAhead = step != 0f && IsBlocked(step > 0f ? 1 : -1, Mathf.Abs(step));
            if (step == 0f || BlockedAhead)
                return;
            body.MovePosition(body.position + Vector3.right * step);
        }

        /// <summary>A wall in front of the body, or no floor just past its front edge.</summary>
        private bool IsBlocked(int direction, float distance)
        {
            Vector3 dir = Vector3.right * direction;
            Vector3 mid = body.position + Vector3.up * (topHeight * 0.5f);
            if (Physics.Raycast(mid, dir, out RaycastHit wall, halfWidth + distance, obstacleMask, QueryTriggerInteraction.Ignore)
                && !PlayerMotor.TryGetFromCollider(wall.collider, out _))
                return true;

            Vector3 probe = body.position + dir * (halfWidth + ledgeLookAhead + distance) + Vector3.up * 0.2f;
            return !Physics.Raycast(probe, Vector3.down, 0.6f, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        // ---------------------------------------------------------------- contact / damage / defeat

        private void OnTriggerEnter(Collider other) => Contact(other);
        private void OnTriggerStay(Collider other) => Contact(other);

        private void Contact(Collider other)
        {
            if (stage != null && stage.PlayerDead)
                return; // the player died first: the falling body neither stomps nor gets hit again
            if (defeated || stage == null || !PlayerMotor.TryGetFromCollider(other, out var player))
                return;

            // The overlap was detected from the pre-step pose, so judge the feet height at the step start.
            float feetY = Mathf.Max(player.FeetY, player.StepStartFeetY);
            bool feetAbove = feetY >= transform.position.y + topHeight - stompTolerance;
            if (stompable && feetAbove)
            {
                // From above a stompable monster is never lethal (same rule as the boss): every landing bounces and
                // counts as a stomp, also while it still flashes from the last one, so a multi-HP monster can be
                // stomped again and again without dropping through it. Rising out of the bounce is harmless.
                if (player.Velocity.y >= 0f || player.RecentlyBounced)
                    return; // rising, or this landing already bounced
                player.Bounce();
                TakeStomp();
                return;
            }
            if (Invulnerable)
                return; // flashing after a stomp: touching it is harmless
            if (contactKills)
            {
                stage.NotifyPlayerHit(dodgeable: true);
            }
        }

        private void TakeStomp()
        {
            if (AttackBusy)
            {
                CancelAttack(); // a stomp interrupts the attack (reward for reading the warning)
                nextAttackTime = Time.time + attackInterval;
            }
            currentHp--;
            stage?.NotifyEnemyStomped(this);
            if (currentHp <= 0)
            {
                Defeat();
                return;
            }
            invulnerableUntil = Time.time + hurtInvulnerability;
            if (animator != null)
                animator.SetTrigger(HitParam);
            else if (visual != null)
                visual.localScale = new Vector3(visualScale.x * 1.15f, visualScale.y * 0.6f, visualScale.z * 1.15f);
        }

        /// <summary>Springs the visual back after a non-lethal stomp.</summary>
        private void UpdateHurtPulse()
        {
            visual.localScale = Vector3.MoveTowards(visual.localScale, visualScale, 4f * Time.deltaTime);
        }

        private void Defeat()
        {
            ClearProjectiles();
            CancelAttack();
            defeated = true;
            defeatedAt = Time.time;
            desiredSpeedX = 0f;
            hitBox.enabled = false;
            if (agent.enabled)
                agent.End();
            agent.enabled = false;
            OnDefeatedMovement();
            SetMode(Mode.Patrol);
            if (defeatBurst != null)
                defeatBurst.Play(true);
            if (animator != null)
            {
                animator.SetFloat(SpeedParam, 0f);
                animator.SetTrigger(DefeatParam);
            }
            if (stage != null)
            {
                for (int i = 0; i < rewardCoins; i++)
                    stage.NotifyCoin();
            }
        }

        private void UpdateSquash()
        {
            if (visual == null || !visual.gameObject.activeSelf)
                return;
            float t = Time.time - defeatedAt;
            if (animator != null)
            {
                if (t >= defeatHideDelay)
                    FinishDefeat(); // the Defeat clip does the squash
                return;
            }
            float k =Mathf.Clamp01(t / (squashSeconds * 0.35f));
            var s = visualScale;
            s.y = Mathf.Lerp(visualScale.y, visualScale.y * 0.15f, k);
            s.x = Mathf.Lerp(visualScale.x, visualScale.x * squashWiden, k);
            s.z = Mathf.Lerp(visualScale.z, visualScale.z * squashWiden, k);
            visual.localScale = s;
            if (t >= squashSeconds)
                FinishDefeat();
        }

        /// <summary>Stage clear: the monster and its projectiles leave the screen at once (Retry brings them back).</summary>
        public void HideForClear()
        {
            ClearProjectiles();
            FinishDefeat();
        }

        /// <summary>Hides the monster; the root stays active so a playing burst is not cut off.</summary>
        private void FinishDefeat()
        {
            defeated = true;
            hitBox.enabled = false;
            agent.enabled = false;
            if (visual != null)
                visual.gameObject.SetActive(false);
            else
                gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = Application.isPlaying ? start : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(origin + Vector3.up * 0.05f, origin + Vector3.right * patrolRange + Vector3.up * 0.05f);
            if (viewDistance <= 0f)
                return;

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 fwd = Vector3.right * (Application.isPlaying ? facing : 1);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, 0f, viewHalfAngle) * fwd * viewDistance);
            Gizmos.DrawLine(eye, eye + Quaternion.Euler(0f, 0f, -viewHalfAngle) * fwd * viewDistance);
        }
    }
}
