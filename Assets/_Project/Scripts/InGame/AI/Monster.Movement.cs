using UnityEngine;

namespace MyGame
{
    /// <summary>How a monster moves (MonsterTable column "movement").</summary>
    public enum MonsterMovement
    {
        /// <summary>Walks on the floor (patrol / chase graphs). Default.</summary>
        Ground = 0,
        /// <summary>Hovers at spawn height + hoverHeight, patrols in the air and dives at a player below it.</summary>
        Fly = 1,
        /// <summary>Starts hidden underground, shows a telegraph when the player comes near, then emerges.</summary>
        Burrow = 2,
    }

    /// <summary>
    /// Fly and burrow movement of Monster (the Ground mode is unchanged and lives in Monster.cs). The Behavior graph
    /// stays the decision maker for patrolling (a flyer patrols in the air through the same Walk commands; a burrowed
    /// slime starts its patrol graph when it has emerged). The dive and the emerge sequence are small state machines here,
    /// because they are timed physical sequences rather than decisions. All tuning is Inspector-visible.
    /// </summary>
    public sealed partial class Monster
    {
        private enum FlyPhase { Hover, Anticipate, Dive, Return }
        private enum BurrowPhase { Hidden, Telegraph, Rising, Up, Sinking, Cooldown, Roaming }

        public const string FlyState = "Fly";
        public const string AnticipateState = "Anticipate";
        public const string AttackState = "Attack";
        public const string BurrowState = "Burrow";

        [Header("Movement (table column movement)")]
        [SerializeField] private MonsterMovement movement = MonsterMovement.Ground;

        [Header("Fly (movement = fly)")]
        [Tooltip("Hover height above the placement point (m).")]
        [SerializeField, Min(0f)] private float hoverHeight = 2.5f;
        [SerializeField, Min(0f)] private float bobAmplitude = 0.2f;
        [SerializeField, Min(0.1f)] private float bobSpeed = 2.2f;
        [Tooltip("The dive starts when the player is below the monster and at most this far away horizontally.")]
        [SerializeField, Min(0.5f)] private float diveTriggerDistance = 5f;
        [Tooltip("Telegraph before the dive (the monster freezes and shakes).")]
        [SerializeField, Min(0.1f)] private float diveAnticipateSeconds = 0.5f;
        [SerializeField, Min(1f)] private float diveSpeed = 11f;
        [SerializeField, Min(0.5f)] private float diveReturnSpeed = 4.5f;
        [Tooltip("Pause between two dives (hovering).")]
        [SerializeField, Min(0f)] private float diveCooldown = 2f;

        [Header("Burrow (movement = burrow)")]
        [Tooltip("Horizontal distance at which the telegraph starts.")]
        [SerializeField, Min(0.5f)] private float emergeDistance = 4f;
        [Tooltip("Seconds of dust mound and shaking before it emerges. Nothing kills during this time.")]
        [SerializeField, Min(0.6f)] private float telegraphSeconds = 0.8f;
        [Tooltip("How deep the hidden visual sits below the floor.")]
        [SerializeField, Min(0.1f)] private float burrowDepth = 1.6f;
        [SerializeField, Min(0.05f)] private float riseSeconds = 0.35f;
        [Tooltip("Not stompable burrowers (worms) stay up this long, sink back and repeat.")]
        [SerializeField, Min(0.1f)] private float surfaceSeconds = 1.2f;
        [SerializeField, Min(0.05f)] private float sinkSeconds = 0.4f;
        [SerializeField, Min(0f)] private float burrowCooldown = 2f;
        [Tooltip("Dust mound shown during the telegraph (SD_FX03_BurrowEmerge). Optional.")]
        [SerializeField] private GameObject emergeFxPrefab;

        private FlyPhase flyPhase;
        private float flyTimer;
        private float bobClock;
        private float nextDiveTime;
        private Vector3 diveTarget;
        private bool flyStarted;

        private BurrowPhase burrowPhase;
        private float burrowTimer;
        private GameObject moundInstance;
        private Vector3 visualBasePosition;

        public MonsterMovement Movement => movement;
        public bool IsDiving => movement == MonsterMovement.Fly && flyPhase == FlyPhase.Dive;
        /// <summary>True while the emerge telegraph runs (test hook; nothing is lethal then).</summary>
        public bool IsTelegraphing => movement == MonsterMovement.Burrow && burrowPhase == BurrowPhase.Telegraph;
        public bool IsBurrowedHidden => movement == MonsterMovement.Burrow
            && (burrowPhase == BurrowPhase.Hidden || burrowPhase == BurrowPhase.Telegraph || burrowPhase == BurrowPhase.Cooldown);

        /// <summary>Graph walk commands are ignored while the body is in a scripted motion.</summary>
        private bool MotionLocked =>
            (movement == MonsterMovement.Fly && flyPhase != FlyPhase.Hover)
            || (movement == MonsterMovement.Burrow && burrowPhase != BurrowPhase.Roaming);

        /// <summary>Not stompable burrowers go up and down again; stompable ones stay out and patrol.</summary>
        private bool BurrowRepeats => !stompable;

        private void InitMovement()
        {
            if (visual != null)
                visualBasePosition = visual.localPosition;
            ResetMovement();
        }

        /// <summary>Back to the initial movement state (called from Awake and ResetToStart, after the base pose is set).</summary>
        private void ResetMovement()
        {
            flyPhase = FlyPhase.Hover;
            flyStarted = false;
            DestroyMound();
            if (visual != null)
                visual.localPosition = visualBasePosition;

            switch (movement)
            {
                case MonsterMovement.Fly:
                {
                    bobClock = 0f;
                    nextDiveTime = Time.time + 1f;
                    Vector3 p = start + Vector3.up * hoverHeight;
                    transform.position = p;
                    body.position = p;
                    PlayState(FlyState, 0f);
                    break;
                }
                case MonsterMovement.Burrow:
                {
                    burrowPhase = BurrowPhase.Hidden;
                    burrowTimer = 0f;
                    hitBox.enabled = false;
                    agent.enabled = false;
                    if (visual != null)
                        visual.gameObject.SetActive(false);
                    break;
                }
            }
        }

        private void OnDefeatedMovement()
        {
            DestroyMound();
            if (movement == MonsterMovement.Burrow)
                burrowPhase = BurrowPhase.Roaming;
        }

        private void UpdateMovement()
        {
            if (defeated)
                return;
            if (movement == MonsterMovement.Burrow)
                UpdateBurrow();
            else if (movement == MonsterMovement.Fly)
                UpdateFlyVisual();
        }

        private void PlayState(string stateName, float fade)
        {
            if (animator == null || !animator.isActiveAndEnabled)
                return;
            int hash = Animator.StringToHash(stateName);
            if (!animator.HasState(0, hash))
                return;
            if (fade <= 0f)
                animator.Play(hash, 0, 0f);
            else
                animator.CrossFadeInFixedTime(hash, fade, 0);
        }

        // ---------------------------------------------------------------- fly

        private bool TryGetDiveTarget(Vector3 from, out Vector3 target)
        {
            target = default;
            if (stage == null || stage.Player == null || !TryGetAimPoint(stage.Player.gameObject, out Vector3 aim))
                return false;
            if (Mathf.Abs(aim.x - from.x) > diveTriggerDistance || aim.y > from.y - 0.5f)
                return false;
            // The pivot is the feet, so aim the hit box centre at the player's centre; never dive into the floor.
            target = aim - Vector3.up * (topHeight * 0.5f);
            target.z = 0f;
            if (Physics.Raycast(target + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, obstacleMask, QueryTriggerInteraction.Ignore)
                && !PlayerMotor.TryGetFromCollider(hit.collider, out _))
                target.y = Mathf.Max(target.y, hit.point.y);
            return true;
        }

        private void FlyFixedStep()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 pos = body.position;
            bobClock += dt;
            float hoverY = start.y + hoverHeight + Mathf.Sin(bobClock * bobSpeed) * bobAmplitude;

            switch (flyPhase)
            {
                case FlyPhase.Hover:
                    pos.x += desiredSpeedX * dt;
                    pos.y = Mathf.MoveTowards(pos.y, hoverY, 6f * dt);
                    if (Time.time >= nextDiveTime && TryGetDiveTarget(pos, out _))
                    {
                        flyPhase = FlyPhase.Anticipate;
                        flyTimer = diveAnticipateSeconds;
                        Sfx.Play(SfxId.BatDive);
                        desiredSpeedX = 0f;
                        PlayState(AnticipateState, 0.08f);
                        SpawnAlertIcon(diveAnticipateSeconds + 0.1f);
                    }
                    break;

                case FlyPhase.Anticipate:
                    pos.y = Mathf.MoveTowards(pos.y, hoverY, 6f * dt);
                    flyTimer -= dt;
                    if (flyTimer <= 0f)
                    {
                        // The target is the player's position at dive time (dodgeable by moving after the telegraph).
                        if (!TryGetDiveTarget(pos, out diveTarget))
                            diveTarget = new Vector3(pos.x, start.y, 0f);
                        Face(diveTarget.x >= pos.x ? 1 : -1);
                        flyPhase = FlyPhase.Dive;
                        PlayState(AttackState, 0.05f);
                    }
                    break;

                case FlyPhase.Dive:
                {
                    Vector3 to = diveTarget - pos;
                    float step = diveSpeed * dt;
                    if (to.magnitude <= step)
                    {
                        pos = diveTarget;
                        flyPhase = FlyPhase.Return;
                    }
                    else
                    {
                        pos += to.normalized * step;
                    }
                    break;
                }

                case FlyPhase.Return:
                {
                    pos.y = Mathf.MoveTowards(pos.y, hoverY, diveReturnSpeed * dt);
                    float homeX = Mathf.Clamp(pos.x, PatrolMinX, PatrolMaxX);
                    pos.x = Mathf.MoveTowards(pos.x, homeX, diveReturnSpeed * dt);
                    if (Mathf.Abs(pos.y - hoverY) < 0.05f)
                    {
                        flyPhase = FlyPhase.Hover;
                        nextDiveTime = Time.time + diveCooldown;
                        Face(pos.x >= (PatrolMinX + PatrolMaxX) * 0.5f ? -1 : 1);
                        PlayState(FlyState, 0.15f);
                    }
                    break;
                }
            }
            pos.z = 0f;
            body.MovePosition(pos);
        }

        private void UpdateFlyVisual()
        {
            if (!flyStarted)
            {
                flyStarted = true;
                PlayState(FlyState, 0f); // the Fly loop replaces Idle / Move for flyers
            }
            if (visual == null)
                return;
            // Anticipation shake (the visual jitters sideways; the hit box stays put).
            Vector3 p = visualBasePosition;
            if (flyPhase == FlyPhase.Anticipate)
                p.x += Mathf.Sin(Time.time * 90f) * 0.06f;
            visual.localPosition = p;
        }

        // ---------------------------------------------------------------- burrow

        private bool PlayerNear()
        {
            if (stage == null || stage.Player == null || !stage.Player.gameObject.activeInHierarchy)
                return false;
            Vector3 p = stage.Player.position;
            return Mathf.Abs(p.x - start.x) <= emergeDistance && p.y > start.y - 1f && p.y < start.y + 5f;
        }

        private void UpdateBurrow()
        {
            float dt = Time.deltaTime;
            switch (burrowPhase)
            {
                case BurrowPhase.Hidden:
                    if (PlayerNear())
                        BeginTelegraph();
                    break;

                case BurrowPhase.Telegraph:
                {
                    burrowTimer += dt;
                    float k = Mathf.Clamp01(burrowTimer / telegraphSeconds);
                    if (moundInstance != null)
                    {
                        float shake = Mathf.Sin(burrowTimer * 70f) * 0.05f * k;
                        moundInstance.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, k);
                        moundInstance.transform.position = new Vector3(start.x + shake, start.y, 0f);
                    }
                    if (burrowTimer >= telegraphSeconds)
                        BeginRise();
                    break;
                }

                case BurrowPhase.Rising:
                {
                    burrowTimer += dt;
                    float k = Mathf.Clamp01(burrowTimer / riseSeconds);
                    SetBurrowOffset(1f - k * k * (3f - 2f * k));
                    if (k >= 1f)
                    {
                        burrowTimer = 0f;
                        if (BurrowRepeats)
                        {
                            burrowPhase = BurrowPhase.Up;
                        }
                        else
                        {
                            // A slime has emerged: from now on it patrols like any other monster.
                            burrowPhase = BurrowPhase.Roaming;
                            boundTarget = null;
                            agent.enabled = true;
                            agent.Restart();
                        }
                        DestroyMound();
                    }
                    break;
                }

                case BurrowPhase.Up:
                    burrowTimer += dt;
                    if (burrowTimer >= surfaceSeconds)
                    {
                        burrowPhase = BurrowPhase.Sinking;
                        burrowTimer = 0f;
                        hitBox.enabled = false;
                        PlayState(BurrowState, 0.05f);
                    }
                    break;

                case BurrowPhase.Sinking:
                {
                    burrowTimer += dt;
                    float k = Mathf.Clamp01(burrowTimer / sinkSeconds);
                    SetBurrowOffset(k);
                    if (k >= 1f)
                    {
                        if (visual != null)
                            visual.gameObject.SetActive(false);
                        burrowPhase = BurrowPhase.Cooldown;
                        burrowTimer = 0f;
                    }
                    break;
                }

                case BurrowPhase.Cooldown:
                    burrowTimer += dt;
                    if (burrowTimer >= burrowCooldown)
                        burrowPhase = BurrowPhase.Hidden;
                    break;
            }
        }

        private void BeginTelegraph()
        {
            Sfx.Play(SfxId.Burrow);
            burrowPhase = BurrowPhase.Telegraph;
            burrowTimer = 0f;
            if (emergeFxPrefab != null)
            {
                moundInstance = Instantiate(emergeFxPrefab, new Vector3(start.x, start.y, 0f), Quaternion.identity, transform.parent);
                moundInstance.transform.localScale = Vector3.one * 0.35f;
            }
        }

        private void BeginRise()
        {
            burrowPhase = BurrowPhase.Rising;
            burrowTimer = 0f;
            hitBox.enabled = true;     // the hit box (and with it stomp / contact) exists only from here on
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                SetBurrowOffset(1f);
            }
            PlayState(BurrowRepeats ? AttackState : "Idle", 0f);
        }

        /// <summary>0 = fully risen, 1 = fully buried.</summary>
        private void SetBurrowOffset(float buried)
        {
            if (visual != null)
                visual.localPosition = visualBasePosition + Vector3.down * (burrowDepth * buried);
        }

        private void DestroyMound()
        {
            if (moundInstance != null)
                Destroy(moundInstance);
            moundInstance = null;
        }
    }
}
