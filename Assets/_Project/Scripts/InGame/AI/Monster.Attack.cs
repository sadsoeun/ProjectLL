using UnityEngine;

namespace MyGame
{
    /// <summary>Attack pattern of a monster (MonsterTable column "attack"; needs canAttack).</summary>
    public enum MonsterAttack
    {
        None = 0,
        /// <summary>Short dash forward (bite, tackle).</summary>
        Lunge = 1,
        /// <summary>Long rolling dash until a wall or ledge (roll, charge).</summary>
        Charge = 2,
        /// <summary>Jumps in an arc toward the player's position and lands (pounce).</summary>
        Leap = 3,
        /// <summary>Stands and strikes the area in front (punch, sting, spear).</summary>
        Melee = 4,
        /// <summary>Hits the floor: a shockwave runs along the ground both ways (jump over it).</summary>
        Slam = 5,
        /// <summary>Fires one projectile at the player (projectilePrefab).</summary>
        Shoot = 6,
    }

    /// <summary>
    /// Attacks of ground monsters. Every attack runs the same readable sequence so it is never a surprise to the player:
    ///   Alert   - the monster startles like a cartoon (hop + squash/stretch), "!!" pops over its head with surprise
    ///             lines and a sound. Nothing hurts yet.
    ///   Windup  - Anticipate clip, trembling, the "!!" pulses; dust is kicked up for dashes. Nothing hurts yet.
    ///   Strike  - the attack itself (kind-specific).
    ///   Recover - the monster stands still (an opening to stomp it), then waits attackInterval before the next attack.
    /// Like fly / burrow this is a timed physical sequence, so it lives here and simply overrides the graph: walk and
    /// turn commands from the graph are ignored while an attack runs. Tuning is Inspector-visible; trigger distance and
    /// cooldown come from the table (attackRange, attackInterval).
    /// </summary>
    public sealed partial class Monster
    {
        private enum AttackPhase { Ready, Alert, Windup, Strike, Recover }

        public const string JumpState = "Jump";
        public const string RollState = "Roll";
        public const string IdleState = "Idle";
        public const string MoveState = "Move";

        [Header("Attack (table columns canAttack / attack / attackRange / attackInterval)")]
        [SerializeField] private MonsterAttack attack = MonsterAttack.None;
        [Tooltip("Cartoon startle with the '!!' icon (seconds).")]
        [SerializeField, Min(0.2f)] private float alertSeconds = 0.5f;
        [Tooltip("Wind-up after the startle (seconds). Alert + windup is the whole warning time.")]
        [SerializeField, Min(0.1f)] private float windupSeconds = 0.45f;
        [Tooltip("Standing still after the strike (seconds): the opening to stomp it.")]
        [SerializeField, Min(0f)] private float recoverSeconds = 0.6f;
        [Tooltip("The player must be within this height above the feet (and not more than 0.8 m below) to trigger.")]
        [SerializeField, Min(0.5f)] private float attackVerticalReach = 1.8f;
        [Tooltip("Height of the startle hop (m, scaled by the body height).")]
        [SerializeField, Min(0f)] private float startleHop = 0.32f;

        [Header("Attack: Lunge / Charge")]
        [SerializeField, Min(0.5f)] private float lungeDistance = 2.4f;
        [SerializeField, Min(1f)] private float lungeSpeed = 7.5f;
        [SerializeField, Min(1f)] private float chargeSpeed = 6.5f;
        [SerializeField, Min(1f)] private float chargeMaxDistance = 7f;

        [Header("Attack: Leap")]
        [SerializeField, Min(0.3f)] private float leapHeight = 1.7f;
        [SerializeField, Min(0.2f)] private float leapSeconds = 0.7f;
        [SerializeField, Min(0.5f)] private float leapMaxDistance = 4.5f;

        [Header("Attack: Melee")]
        [Tooltip("Reach of the strike in front of the body (m).")]
        [SerializeField, Min(0.3f)] private float meleeReach = 1.3f;
        [Tooltip("Time into the Attack clip when the strike lands (s).")]
        [SerializeField, Min(0f)] private float meleeDelay = 0.15f;
        [SerializeField, Min(0.05f)] private float meleeActiveSeconds = 0.22f;

        [Header("Attack: Slam")]
        [SerializeField, Min(0.5f)] private float slamRadius = 4.5f;
        [SerializeField, Min(1f)] private float slamWaveSpeed = 6.5f;
        [Tooltip("Time into the Attack clip when the fists hit the floor (s).")]
        [SerializeField, Min(0f)] private float slamDelay = 0.3f;
        [Tooltip("The wave hurts only a player whose feet are below this height above the floor (jump over it).")]
        [SerializeField, Min(0.1f)] private float slamWaveHeight = 0.5f;

        [Header("Attack: Shoot")]
        [SerializeField, Min(0f)] private float shootDelay = 0.18f;

        [Header("Attack FX (Prefabs/Monsters/Common/FX)")]
        [SerializeField] private CartoonFx alertFxPrefab;
        [SerializeField] private CartoonFx shockLinesFxPrefab;
        [SerializeField] private CartoonFx swooshFxPrefab;
        [SerializeField] private CartoonFx shockwaveFxPrefab;
        [SerializeField] private CartoonFx dustFxPrefab;

        private AttackPhase attackPhase;
        private float attackTimer;
        private float nextAttackTime;
        private bool attackHitDone;
        private bool strikeHitLanded;
        private float attackTravel;
        private Vector3 leapFrom, leapTo;
        private float nextDustTime;
        private CartoonFx alertFx;
        private bool facingUnlocked;
        private Vector3 attackVisualOffset;
        private Vector3 attackVisualScale = Vector3.one;

        public MonsterAttack Attack => attack;
        public bool AttackBusy => attackPhase != AttackPhase.Ready;
        /// <summary>True during the startle and the wind-up (test hook: the warning is showing, nothing hurts yet).</summary>
        public bool IsAttackTelegraphing => attackPhase == AttackPhase.Alert || attackPhase == AttackPhase.Windup;
        public bool IsAttackStriking => attackPhase == AttackPhase.Strike;

        private bool AttackEnabled => canAttack && attack != MonsterAttack.None && movement == MonsterMovement.Ground;
        private float BodyScale => Mathf.Clamp(topHeight, 0.5f, 2.5f);

        private GameObject AttackTarget => targetOverride != null ? targetOverride
            : stage != null && stage.Player != null ? stage.Player.gameObject : null;

        private void ResetAttack()
        {
            CancelAttack();
            nextAttackTime = Time.time + 1f;
        }

        private void CancelAttack()
        {
            attackPhase = AttackPhase.Ready;
            attackTimer = 0f;
            if (alertFx != null)
                Destroy(alertFx.gameObject);
            alertFx = null;
            attackVisualOffset = Vector3.zero;
            attackVisualScale = Vector3.one;
            if (visual != null && movement == MonsterMovement.Ground)
            {
                visual.localPosition = visualBasePosition;
                visual.localScale = visualScale;
            }
        }

        // ---------------------------------------------------------------- decision (Update)

        private void UpdateAttack()
        {
            if (defeated || !AttackEnabled)
                return;
            float dt = Time.deltaTime;
            attackTimer += dt;
            attackVisualOffset = Vector3.zero;
            attackVisualScale = Vector3.one;

            switch (attackPhase)
            {
                case AttackPhase.Ready:
                    if (Time.time >= nextAttackTime && TryGetAttackDirection(out int dir))
                        BeginAlert(dir);
                    return;

                case AttackPhase.Alert:
                    StartleMotion(attackTimer);
                    if (attackTimer >= alertSeconds)
                        BeginWindup();
                    break;

                case AttackPhase.Windup:
                {
                    float k = Mathf.Clamp01(attackTimer / windupSeconds);
                    attackVisualOffset.x = Mathf.Sin(attackTimer * 75f) * 0.045f * (0.4f + k) * BodyScale;
                    attackVisualScale = new Vector3(1f + 0.06f * k, 1f - 0.08f * k, 1f + 0.06f * k); // crouch
                    if ((attack == MonsterAttack.Charge || attack == MonsterAttack.Lunge) && Time.time >= nextDustTime)
                    {
                        nextDustTime = Time.time + 0.12f;
                        CartoonFx.Spawn(dustFxPrefab, transform.position - Vector3.right * (facing * halfWidth), 0.45f * BodyScale, 0.45f);
                    }
                    if (attackTimer >= windupSeconds)
                        BeginStrike();
                    break;
                }

                case AttackPhase.Strike:
                    UpdateStrike();
                    break;

                case AttackPhase.Recover:
                {
                    float k = Mathf.Clamp01(attackTimer / 0.18f);
                    attackVisualScale = Vector3.Lerp(new Vector3(1.15f, 0.82f, 1.15f), Vector3.one, k); // landing squash settles
                    if (attackTimer >= recoverSeconds)
                        EndAttack();
                    break;
                }
            }
            ApplyAttackVisual();
        }

        private void ApplyAttackVisual()
        {
            if (visual == null)
                return;
            visual.localPosition = visualBasePosition + attackVisualOffset;
            visual.localScale = Vector3.Scale(visualScale, attackVisualScale);
        }

        /// <summary>Player in range, at about the same height, with a clear sight line. Returns the side it is on.</summary>
        private bool TryGetAttackDirection(out int dir)
        {
            dir = facing;
            GameObject target = AttackTarget;
            if (target == null || !TryGetAimPoint(target, out Vector3 aim))
                return false;
            float dx = aim.x - body.position.x;
            float feet = TargetFeetY(target, aim);
            float dy = feet - body.position.y;
            if (Mathf.Abs(dx) > attackRange || dy < -0.8f || dy > attackVerticalReach)
                return false;
            if (!HasLineOfSight(target, aim))
                return false;
            dir = dx >= 0f ? 1 : -1;
            return true;
        }

        private static float TargetFeetY(GameObject target, Vector3 aim)
        {
            if (PlayerMotor.TryGetFromCollider(target.GetComponent<Collider>(), out var motor))
                return motor.FeetY;
            return target.TryGetComponent(out Collider c) && c.enabled ? c.bounds.min.y : aim.y;
        }

        private void FaceForAttack(int dir)
        {
            facingUnlocked = true;
            Face(dir);
            facingUnlocked = false;
        }

        private void BeginAlert(int dir)
        {
            attackPhase = AttackPhase.Alert;
            attackTimer = 0f;
            desiredSpeedX = 0f;
            FaceForAttack(dir);
            PlayState(IdleState, 0.05f);
            SpawnAlertIcon(alertSeconds + windupSeconds + 0.12f);
        }

        /// <summary>"!!" over the head + surprise lines + sound (also used by the flyer's dive warning).</summary>
        private void SpawnAlertIcon(float seconds)
        {
            Sfx.Play(SfxId.MonsterAlert);
            float s = BodyScale;
            Vector3 head = transform.position + Vector3.up * (topHeight + 0.55f * s) + Vector3.back * 0.3f;
            if (alertFx != null)
                Destroy(alertFx.gameObject);
            alertFx = CartoonFx.Spawn(alertFxPrefab, head, 0.75f * Mathf.Sqrt(s), seconds, 1, transform);
            CartoonFx.Spawn(shockLinesFxPrefab, transform.position + Vector3.up * (topHeight * 0.55f) + Vector3.back * 0.2f,
                Mathf.Max(0.8f, topHeight * 1.05f), 0.4f, 1, transform);
        }

        private float AttackAnimatorSpeed =>
            attackPhase == AttackPhase.Strike && attack == MonsterAttack.Charge ? chargeSpeed
            : attackPhase == AttackPhase.Strike && attack == MonsterAttack.Lunge ? lungeSpeed : 0f;

        /// <summary>Cartoon startle: a quick hop with stretch, a squash on landing, then a settle.</summary>
        private void StartleMotion(float t)
        {
            const float hop = 0.26f;
            float s = BodyScale;
            if (t < hop)
            {
                float k = t / hop;
                attackVisualOffset.y = Mathf.Sin(k * Mathf.PI) * startleHop * s;
                float stretch = Mathf.Sin(k * Mathf.PI);
                attackVisualScale = new Vector3(1f - 0.12f * stretch, 1f + 0.2f * stretch, 1f - 0.12f * stretch);
            }
            else
            {
                float k = Mathf.Clamp01((t - hop) / 0.16f);
                float squash = Mathf.Sin(k * Mathf.PI);
                attackVisualScale = new Vector3(1f + 0.18f * squash, 1f - 0.22f * squash, 1f + 0.18f * squash);
            }
        }

        private void BeginWindup()
        {
            attackPhase = AttackPhase.Windup;
            attackTimer = 0f;
            nextDustTime = 0f;
            PlayState(AnticipateState, 0.08f);
            if (attack == MonsterAttack.Leap)
                Sfx.Play(SfxId.Jump);
        }

        private void BeginStrike()
        {
            attackPhase = AttackPhase.Strike;
            attackTimer = 0f;
            attackHitDone = false;
            strikeHitLanded = false;
            attackTravel = 0f;
            if (alertFx != null)
                alertFx.Finish();
            switch (attack)
            {
                case MonsterAttack.Lunge:
                    PlayState(AttackState, 0.05f);
                    Sfx.Play(SfxId.MonsterSwing);
                    break;
                case MonsterAttack.Charge:
                    PlayState(HasState(RollState) ? RollState : MoveState, 0.05f);
                    Sfx.Play(SfxId.BossCharge);
                    break;
                case MonsterAttack.Leap:
                {
                    PlayState(HasState(JumpState) ? JumpState : AttackState, 0.05f);
                    leapFrom = body.position;
                    leapTo = FindLeapLanding();
                    FaceForAttack(leapTo.x >= leapFrom.x ? 1 : -1);
                    break;
                }
                case MonsterAttack.Melee:
                case MonsterAttack.Slam:
                case MonsterAttack.Shoot:
                    PlayState(AttackState, 0.05f);
                    break;
            }
        }

        private void UpdateStrike()
        {
            switch (attack)
            {
                case MonsterAttack.Melee:
                    if (!attackHitDone && attackTimer >= meleeDelay)
                    {
                        attackHitDone = true;
                        Sfx.Play(SfxId.MonsterSwing);
                        Vector3 at = transform.position + new Vector3(facing * (halfWidth + meleeReach * 0.35f), topHeight * 0.5f, -0.35f);
                        CartoonFx.Spawn(swooshFxPrefab, at, Mathf.Max(0.55f, meleeReach * 0.6f), meleeActiveSeconds + 0.15f, facing);
                    }
                    if (attackHitDone && attackTimer <= meleeDelay + meleeActiveSeconds && MeleeHitsPlayer())
                        HitPlayer();
                    if (attackTimer >= meleeDelay + meleeActiveSeconds + 0.2f)
                        BeginRecover();
                    break;

                case MonsterAttack.Slam:
                {
                    if (!attackHitDone && attackTimer >= slamDelay)
                    {
                        attackHitDone = true;
                        Sfx.Play(SfxId.BossSlam);
                        float life = slamRadius / slamWaveSpeed + 0.15f;
                        CartoonFx.SpawnRing(shockwaveFxPrefab, transform.position + Vector3.up * 0.02f, slamRadius, life);
                        CartoonFx.Spawn(dustFxPrefab, transform.position + Vector3.right * 0.6f, 0.8f * BodyScale, 0.6f);
                        CartoonFx.Spawn(dustFxPrefab, transform.position - Vector3.right * 0.6f, 0.8f * BodyScale, 0.6f);
                    }
                    if (attackHitDone)
                    {
                        float r = (attackTimer - slamDelay) * slamWaveSpeed;
                        if (r <= slamRadius && SlamWaveHitsPlayer(r))
                            HitPlayer();
                        if (r > slamRadius + 0.5f)
                            BeginRecover();
                    }
                    break;
                }

                case MonsterAttack.Shoot:
                    if (!attackHitDone && attackTimer >= shootDelay)
                    {
                        attackHitDone = true;
                        int keepFacing = facing;
                        FireAt(AttackTarget);
                        FaceForAttack(keepFacing);
                    }
                    if (attackTimer >= shootDelay + 0.35f)
                        BeginRecover();
                    break;

                case MonsterAttack.Charge:
                    if (Time.time >= nextDustTime)
                    {
                        nextDustTime = Time.time + 0.14f;
                        CartoonFx.Spawn(dustFxPrefab, transform.position - Vector3.right * (facing * halfWidth), 0.4f * BodyScale, 0.45f);
                    }
                    break;
                // Lunge and Leap move in FixedUpdate (AttackFixedStep) and end there.
            }
        }

        private void BeginRecover()
        {
            attackPhase = AttackPhase.Recover;
            attackTimer = 0f;
            PlayState(IdleState, 0.1f);
        }

        private void EndAttack()
        {
            attackPhase = AttackPhase.Ready;
            attackTimer = 0f;
            nextAttackTime = Time.time + attackInterval;
            attackVisualOffset = Vector3.zero;
            attackVisualScale = Vector3.one;
            ApplyAttackVisual();
        }

        private bool HasState(string stateName) =>
            animator != null && animator.isActiveAndEnabled && animator.HasState(0, Animator.StringToHash(stateName));

        // ---------------------------------------------------------------- hits

        /// <summary>One hit per strike at most (the stage side respawns the player).</summary>
        private void HitPlayer()
        {
            if (strikeHitLanded)
                return;
            strikeHitLanded = true;
            LastAttackHitTime = Time.time;
            if (stage != null)
                stage.NotifyPlayerHit(dodgeable: true);
            else if (logModeChanges)
                Debug.Log($"[Monster] {name}: {attack} hit the target (t={Time.time:F2})", this);
        }

        /// <summary>Test hook: time of the last Melee / Slam hit (-1 = never).</summary>
        public float LastAttackHitTime { get; private set; } = -1f;

        private bool MeleeHitsPlayer()
        {
            GameObject target = AttackTarget;
            if (target == null || !target.TryGetComponent(out Collider c) || !c.enabled)
                return false;
            float near = body.position.x + facing * (halfWidth * 0.5f);
            float far = body.position.x + facing * (halfWidth + meleeReach);
            var box = new Bounds(new Vector3((near + far) * 0.5f, body.position.y + topHeight * 0.5f, 0f),
                new Vector3(Mathf.Abs(far - near), topHeight * 1.1f, 2f));
            var b = c.bounds;
            b.center = new Vector3(b.center.x, b.center.y, 0f);
            return box.Intersects(b);
        }

        private bool SlamWaveHitsPlayer(float radius)
        {
            GameObject target = AttackTarget;
            if (target == null || !TryGetAimPoint(target, out Vector3 aim))
                return false;
            float feet = TargetFeetY(target, aim);
            if (feet > body.position.y + slamWaveHeight || feet < body.position.y - 0.6f)
                return false;
            float dx = Mathf.Abs(aim.x - body.position.x);
            return dx >= radius - 0.45f && dx <= radius + 0.2f;
        }

        // ---------------------------------------------------------------- motion (FixedUpdate)

        /// <summary>True while the attack drives the body (the normal walk step is skipped).</summary>
        private bool AttackFixedStep()
        {
            if (!AttackBusy)
                return false;
            if (attackPhase != AttackPhase.Strike)
                return true; // standing still during alert / wind-up / recover
            float dt = Time.fixedDeltaTime;
            switch (attack)
            {
                case MonsterAttack.Lunge:
                case MonsterAttack.Charge:
                {
                    float speed = attack == MonsterAttack.Lunge ? lungeSpeed : chargeSpeed;
                    float limit = attack == MonsterAttack.Lunge ? lungeDistance : chargeMaxDistance;
                    float step = speed * dt;
                    if (IsBlocked(facing, step) || attackTravel >= limit)
                    {
                        if (attack == MonsterAttack.Charge)
                        {
                            CartoonFx.Spawn(dustFxPrefab, transform.position + Vector3.right * (facing * halfWidth), 0.6f * BodyScale, 0.5f);
                            Sfx.Play(SfxId.Land);
                        }
                        BeginRecover();
                        return true;
                    }
                    attackTravel += step;
                    body.MovePosition(body.position + Vector3.right * (facing * step));
                    return true;
                }
                case MonsterAttack.Leap:
                {
                    float k = Mathf.Clamp01(attackTimer / leapSeconds);
                    Vector3 p = Vector3.Lerp(leapFrom, leapTo, k);
                    p.y += 4f * leapHeight * k * (1f - k);
                    body.MovePosition(p);
                    if (k >= 1f)
                    {
                        CartoonFx.Spawn(dustFxPrefab, leapTo, 0.7f * BodyScale, 0.5f);
                        Sfx.Play(SfxId.Land);
                        BeginRecover();
                    }
                    return true;
                }
            }
            return true;
        }

        /// <summary>Landing point: the player's X (clamped to leapMaxDistance) on the same floor height; never past a ledge or wall.</summary>
        private Vector3 FindLeapLanding()
        {
            Vector3 from = body.position;
            float targetX = from.x + facing * Mathf.Min(attackRange, leapMaxDistance);
            if (AttackTarget != null && TryGetAimPoint(AttackTarget, out Vector3 aim))
                targetX = Mathf.Clamp(aim.x, from.x - leapMaxDistance, from.x + leapMaxDistance);
            float dir = Mathf.Sign(targetX - from.x);
            float dist = Mathf.Abs(targetX - from.x);
            // A wall between here and the landing point shortens the jump.
            Vector3 mid = from + Vector3.up * (topHeight * 0.5f);
            if (dist > 0.01f && Physics.Raycast(mid, Vector3.right * dir, out RaycastHit wall, dist + halfWidth, obstacleMask, QueryTriggerInteraction.Ignore)
                && !PlayerMotor.TryGetFromCollider(wall.collider, out _) && !wall.collider.transform.IsChildOf(transform))
                dist = Mathf.Max(0f, wall.distance - halfWidth - 0.05f);
            // Step back until there is floor at the takeoff height under the whole body.
            for (; dist > 0.01f; dist -= 0.25f)
            {
                float x = from.x + dir * dist;
                if (HasFloorAt(x - halfWidth * 0.8f, from.y) && HasFloorAt(x + halfWidth * 0.8f, from.y))
                    break;
            }
            return new Vector3(from.x + dir * Mathf.Max(dist, 0f), from.y, from.z);
        }

        private bool HasFloorAt(float x, float y)
        {
            var probe = new Vector3(x, y + 0.3f, 0f);
            return Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 0.7f, obstacleMask, QueryTriggerInteraction.Ignore)
                   && !PlayerMotor.TryGetFromCollider(hit.collider, out _) && Mathf.Abs(hit.point.y - y) < 0.25f;
        }
    }
}
