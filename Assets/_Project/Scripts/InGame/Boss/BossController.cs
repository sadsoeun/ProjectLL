using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// One boss fight, shared by all bosses; the per-boss behaviour is the BossData asset.
    ///
    /// Flow: Dormant (boss idles until the player is `engageDistance` inside the arena) -> Engage (camera locks to the arena, the
    /// entrance gate closes, HP bar appears) -> Intro (Roar, invulnerable) -> loop { Walk toward the player -> the attacks of the
    /// current phase, one at a time: attack (Charge / Slam / Rain, each with an Anticipate telegraph) -> Stunned (weak point
    /// open, stars orbit the head) }. Every attack (gimmick) ends in a stun; a Charge ends by crashing into the arena wall.
    /// The chain index continues after the stun and wraps; when the stun ends in a new phase, that phase's chain starts over.
    /// Every head stomp while Stunned costs 1 HP and bounces the player, so a stun window can take several stomps in a row.
    /// Each one plays the Hit flinch; a short cooldown stops one bounce from counting twice. A perfect dodge during the fight
    /// makes the boss groggy at once (attack broken, hazards cleared) and holds that stun open until its first stomp lands.
    /// The phases are spread evenly over the HP (BossData.PhaseIndexFor).
    /// A head stomp at any other time only bounces the player (no damage either way, like hopping over a monster).
    /// The Charge (low band) and the hazards kill; plain body contact only pushes the player back (Knockback). At 0 HP: Defeat clip, then StageRoot.NotifyGoal() (the normal clear flow).
    /// A death during the fight keeps the boss HP and the fight (camera lock, closed gate): the player respawns inside the
    /// arena (IRespawnOverride) and the boss backs off to the far wall. Retry / Game Over resets the boss to full HP.
    ///
    /// Layout: this component sits on the boss root (kinematic Rigidbody + trigger BoxCollider). The model lives under
    /// `visual` (faces +Z like monsters; yaw is set from the facing). All timing runs on the fixed step.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class BossController : MonoBehaviour, IStageElement, IRespawnOverride
    {
        public enum State { Dormant, Intro, Walk, AnticipateCharge, Charge, ChargeEnd, AnticipateSlam, Slam, SlamRecover, Rain, Stunned, Hit, Defeat, Done, AnticipateEruption, Eruption }

        /// <summary>Channel the arena entrance gate listens to (inverted gate: closed while the fight runs). Do not use it in layouts.</summary>
        public const int ArenaChannel = 9999;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Header("Data (written by the stage builder)")]
        [SerializeField] private BossData data;
        [SerializeField] private float arenaMinX;
        [SerializeField] private float arenaMaxX;

        [Header("Model")]
        [SerializeField] private Transform visual;
        [SerializeField] private Animator animator;
        [Tooltip("Head top. The stomp height follows this transform (it drops while stunned).")]
        [SerializeField] private Transform weakPoint;
        [Tooltip("Renderers that glow while the weak point is open (core / crest / crown gem).")]
        [SerializeField] private Renderer[] glowRenderers;
        [Tooltip("Tint the base colour too (placeholder without emission map).")]
        [SerializeField] private bool tintBase;
        [Range(0f, 60f)]
        [SerializeField] private float faceCameraAngle = 25f;

        private StageRoot stage;
        private Rigidbody body;
        private BoxCollider box;
        private Vector3 start;
        private int hp;
        private State state = State.Dormant;
        private float timer;
        private int attackIndex;
        private int facing = -1;
        private float chargeDir;
        private bool invulnerable;
        private bool gateHeld;
        private float flinchTimer;   // Hit flinch inside the stun (stun clock paused)
        private float stompCooldown; // no damage until this runs out
        private int stunPhase;       // phase index when the current stun began
        private bool dodgeEarned;    // perfect dodge: the stun waits for the first stomp
        private BossHud hud;
        private BossStunFx stunFx;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<BossPillar> pillars = new List<BossPillar>(); // rising basalt pillars (stun staircase / eruption islands)
        private readonly List<int> eruptionPillarCells = new List<int>();   // cell indices (on eruptionSide) that hold a pillar instead of a column
        private int eruptionSide = -1;
        private float contactGrace;  // > 0: plain body contact is ignored (after a knockback)
        private float cornerFlight;  // > 0: the player was flung over the boss out of a corner (no stomp, no push)
        private PlayerMotor playerMotor;
        private MaterialPropertyBlock block;
        private Vector3 baseBoxSize, baseBoxCenter;
        private Vector3 baseVisualScale = Vector3.one;
        private bool popped;         // defeat: the body has burst and is hidden
        private const float PopSwellSeconds = 0.25f; // the body swells this long before it bursts

        // rain bookkeeping
        private readonly List<float> rainX = new List<float>();
        private int rainSpawned;
        private float rainClock;
        private readonly List<GameObject> rainMarkers = new List<GameObject>();

        // eruption bookkeeping
        private bool eruptionFired;
        private float eruptionClock;
        private float eruptionDuration;

        public State Current => state;
        public int Hp => hp;
        public int MaxHp => data != null ? data.maxHp : 0;
        public bool IsEngaged => state != State.Dormant;
        public BossData Data => data;
        public float ArenaMinX => arenaMinX;
        public float ArenaMaxX => arenaMaxX;

        // Read-only hooks for the boss tutorial (StageTutorialDirector). They change nothing in the fight.
        /// <summary>A head stomp took HP (after the HP was reduced).</summary>
        public event System.Action Damaged;
        /// <summary>Index into BossData.phases for the current HP.</summary>
        public int CurrentPhaseIndex => data != null ? PhaseIndex : 0;
        /// <summary>World height of the body (for framing the boss on screen).</summary>
        public float BodyHeight => data != null ? data.bodyHeight : 0f;
        /// <summary>Horizontal distance from the body edge to a world x (negative = inside the body).</summary>
        public float EdgeDistance(float worldX) => Mathf.Abs(worldX - transform.position.x) - (data != null ? data.bodyWidth * 0.5f : 0f);
        /// <summary>Walking between attacks, with at least <paramref name="seconds"/> left before the next attack starts. (Every attack ends in the stun, so no hazard is in flight while walking.)</summary>
        public bool IsCalmFor(float seconds) => state == State.Walk && timer >= seconds;
        /// <summary>In the entrance roar (invulnerable, no attack yet) for at least <paramref name="seconds"/>: the HP bar is up and the camera is on the arena.</summary>
        public bool IsIntroFor(float seconds) => state == State.Intro && data != null && data.introSeconds - timer >= Mathf.Min(seconds, data.introSeconds * 0.5f);
        /// <summary>Groggy with the weak point open (a head stomp deals damage).</summary>
        public bool IsStunOpen => state == State.Stunned && !invulnerable;
        private float HalfWidth => data.bodyWidth * 0.5f;
        private float FloorY => start.y;
        private int HitsTaken => data.maxHp - hp;
        private BossPhase Phase => data.PhaseFor(HitsTaken);
        private int PhaseIndex => data.PhaseIndexFor(HitsTaken);

        /// <summary>World Y the head stomp is measured against: the weak point (follows the animated bone).</summary>
        public float TopY => weakPoint != null ? weakPoint.position.y : transform.position.y + data.bodyHeight;

        private void Awake()
        {
            if (data == null)
            {
                Debug.LogError("[Boss] BossController.data is not assigned (the stage builder wires it).", this);
                enabled = false;
                return;
            }
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            block = new MaterialPropertyBlock();
            start = transform.position;
            if (visual != null && !Mathf.Approximately(data.visualScale, 1f))
                visual.localScale *= data.visualScale; // runtime scale: re-imports regenerate the prefab, the data keeps the value
            if (visual != null)
                baseVisualScale = visual.localScale;
            ApplyBodySize();
            baseBoxSize = box.size;
            baseBoxCenter = box.center;
            hp = data.maxHp;
            Face(-1);
            if (data.stunStarMaterial != null)
                stunFx = BossStunFx.Create(transform, weakPoint, data);
            else
                Debug.LogError($"[Boss {data.bossId}] stunStarMaterial is not assigned; the stun has no star effect.", this);
        }

        private void ApplyBodySize()
        {
            box.size = new Vector3(data.bodyWidth, data.bodyHeight, 4f);
            box.center = new Vector3(0f, data.bodyHeight * 0.5f, 0f);
        }

        /// <summary>Trigger box for the current state: the lower stunned box (BossData.stunnedBoxHeight) while Stunned, else the full body.</summary>
        private void ApplyStateBox()
        {
            if (state == State.Stunned && data.stunnedBoxHeight > 0f)
            {
                float h = Mathf.Min(data.stunnedBoxHeight, baseBoxSize.y);
                box.size = new Vector3(baseBoxSize.x, h, baseBoxSize.z);
                box.center = new Vector3(baseBoxCenter.x, baseBoxCenter.y - (baseBoxSize.y - h) * 0.5f, baseBoxCenter.z); // same bottom
            }
            else
            {
                box.size = baseBoxSize;
                box.center = baseBoxCenter;
            }
        }

        public void Bind(StageRoot owner)
        {
            if (stage != null)
                stage.PerfectDodged -= OnPerfectDodged;
            stage = owner;
            if (stage != null)
                stage.PerfectDodged += OnPerfectDodged;
        }

        /// <summary>
        /// A perfect dodge during the fight breaks the attack: the boss goes groggy at once (hazards in flight vanish) and
        /// the stun stays open until the first stomp lands. In a boss fight the "Now!" cut-in replaces the dodge's time stop
        /// (both own Time.timeScale, so the time stop is cancelled first).
        /// </summary>
        private void OnPerfectDodged()
        {
            if (!FightInProgress || state == State.Intro)
                return;
            dodgeEarned = true;
            if (state == State.Stunned)
                return; // already groggy: this stun is now held open too
            ClearSpawned();
            SetGlow(0f);
            TimeStop.Cancel();
            EnterStun(cutIn: true);
            if (stunFx != null)
                stunFx.SetVisible(true);
        }

        public void ResetForAttempt() => ResetBoss();

        /// <summary>True while a fight is running (engaged and not yet defeated): deaths keep the fight going.</summary>
        private bool FightInProgress => state != State.Dormant && state != State.Defeat && state != State.Done;

        public void ResetForRespawn()
        {
            if (data == null)
                return;
            if (!FightInProgress)
            {
                ResetBoss();
                return;
            }
            // Keep HP, HUD, camera lock and the closed gate. Clear the hazards in flight and give the player room:
            // the boss returns to the far wall and roars (invulnerable) before the chain restarts.
            ClearSpawned();
            SetGlow(0f);
            if (stunFx != null) stunFx.SetVisible(false);
            box.size = baseBoxSize;
            box.center = baseBoxCenter;
            var p = body.position;
            p.x = arenaMaxX - HalfWidth;
            transform.position = p;
            body.position = p;
            Face(-1);
            attackIndex = 0;
            flinchTimer = 0f;
            dodgeEarned = false;
            stompCooldown = 0f;
            contactGrace = 0f;
            cornerFlight = 0f;
            invulnerable = true;
            state = State.Intro;
            timer = data.introSeconds;
            Play("Idle", 0f);
        }

        /// <summary>During the fight the player respawns just inside the arena entrance (the gate stays closed behind).</summary>
        public bool TryGetRespawnPosition(out Vector3 position)
        {
            position = default;
            if (data == null || !FightInProgress)
                return false;
            position = new Vector3(arenaMinX + data.engageDistance, FloorY + 1.2f, 0f);
            return true;
        }

        private void ResetBoss()
        {
            if (data == null)
                return;
            if (stunFx != null) stunFx.SetVisible(false);
            ClearSpawned();
            ReleaseArena();
            if (hud != null)
                Destroy(hud.gameObject);
            hud = null;
            hp = data.maxHp;
            state = State.Dormant;
            timer = 0f;
            attackIndex = 0;
            flinchTimer = 0f;
            dodgeEarned = false;
            stompCooldown = 0f;
            contactGrace = 0f;
            cornerFlight = 0f;
            invulnerable = false;
            box.enabled = true;
            box.size = baseBoxSize;
            box.center = baseBoxCenter;
            popped = false;
            if (visual != null)
            {
                visual.gameObject.SetActive(true);
                visual.localScale = baseVisualScale;
            }
            transform.position = start;
            body.position = start;
            Face(-1);
            SetGlow(0f);
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            Play("Idle", 0f);
        }

        // ---------------------------------------------------------------- tick

        private void FixedUpdate()
        {
            if (stage == null || stage.Player == null)
                return;
            float dt = Time.fixedDeltaTime;
            if (contactGrace > 0f)
                contactGrace -= dt;
            if (cornerFlight > 0f)
                cornerFlight -= dt;
            switch (state)
            {
                case State.Dormant:
                    if (stage.Player.position.x >= arenaMinX + data.engageDistance && stage.Player.position.x <= arenaMaxX)
                        Engage();
                    break;

                case State.Intro:
                    timer -= dt;
                    FacePlayer();
                    if (timer <= 0f)
                        EnterWalk(Phase.walkSeconds);
                    break;

                case State.Walk:
                    timer -= dt;
                    WalkTowardPlayer(Phase.moveSpeed, dt);
                    if (timer <= 0f)
                    {
                        if (attackIndex >= Phase.attacks.Length)
                            attackIndex = 0; // the chain loops
                        BeginAttack(Phase.attacks[attackIndex]);
                    }
                    break;

                case State.AnticipateCharge:
                case State.AnticipateSlam:
                    timer -= dt;
                    SetGlow(0.5f + 0.5f * Mathf.Sin(Time.time * 30f)); // flicker = telegraph
                    if (timer <= 0f)
                    {
                        SetGlow(0f);
                        if (state == State.AnticipateCharge)
                            BeginCharge();
                        else
                            BeginSlam();
                    }
                    break;

                case State.Charge:
                    StepCharge(dt);
                    break;

                case State.ChargeEnd:
                    timer -= dt;
                    if (timer <= 0f)
                        FinishAttack();
                    break;

                case State.Slam:
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        SpawnWaves();
                        Sfx.Play(SfxId.BossSlam);
                        state = State.SlamRecover;
                        timer = data.slamRecoverSeconds;
                    }
                    break;

                case State.SlamRecover:
                    timer -= dt;
                    if (timer <= 0f)
                        FinishAttack();
                    break;

                case State.Rain:
                    StepRain(dt);
                    break;

                case State.AnticipateEruption:
                    timer -= dt;
                    {
                        // charge-up: the glow builds and flickers faster toward the slam
                        float k = 1f - Mathf.Clamp01(timer / data.eruptionChargeSeconds);
                        SetGlow(Mathf.Lerp(0.25f, 1f, k) * (0.75f + 0.25f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 40f, k))));
                    }
                    if (timer <= 0f)
                    {
                        SetGlow(1f);
                        BeginEruptionSlam();
                    }
                    break;

                case State.Eruption:
                    StepEruption(dt);
                    break;

                case State.Stunned:
                    if (stompCooldown > 0f)
                        stompCooldown -= dt;
                    if (flinchTimer > 0f)
                    {
                        // Hit flinch, then the boss sags back into the stun pose. The stun clock keeps running.
                        flinchTimer -= dt;
                        if (flinchTimer <= 0f)
                            Play("Stunned", 0.12f);
                    }
                    timer -= dt;
                    if (data.pillarsOnStun)
                        TickStunPillars();
                    SetGlow(flinchTimer > 0f ? Mathf.Clamp01(flinchTimer / data.hitFlinchSeconds) : 0.7f + 0.3f * Mathf.Sin(Time.time * 8f));
                    if (dodgeEarned)
                        timer = Mathf.Max(timer, 0f); // earned by a perfect dodge: held open until the first stomp
                    else if (timer <= 0f && flinchTimer <= 0f)
                    {
                        SetGlow(0f);
                        SinkPillars(0f);
                        // The opening is over. In a new phase its chain starts over; otherwise it continues.
                        if (PhaseIndex != stunPhase)
                            attackIndex = 0;
                        EnterWalk(Phase.walkSeconds);
                    }
                    break;

                case State.Defeat:
                    timer -= dt;
                    StepDefeatPop();
                    if (timer <= 0f)
                    {
                        state = State.Done;              // inert; the stage clear takes over
                        box.enabled = false;
                        stage.NotifyGoal();
                    }
                    break;
            }
            if (stunFx != null)
                stunFx.SetVisible(state == State.Stunned);
        }

        // ---------------------------------------------------------------- flow

        private void Engage()
        {
            if (stage.CameraRig != null)
                stage.CameraRig.LockArena(arenaMinX, arenaMaxX);
            if (!gateHeld)
            {
                stage.AddChannelHold(ArenaChannel);
                gateHeld = true;
            }
            hud = BossHud.Create(data.nameKey, data.maxHp);
            hud.SetHp(hp);
            state = State.Intro;
            timer = data.introSeconds;
            Sfx.Play(SfxId.BossRoar);
            invulnerable = true;
            Play("Roar", 0.1f);
        }

        private void ReleaseArena()
        {
            if (stage != null && stage.CameraRig != null)
                stage.CameraRig.UnlockArena();
            if (gateHeld && stage != null)
                stage.RemoveChannelHold(ArenaChannel);
            gateHeld = false;
        }

        private void EnterWalk(float seconds)
        {
            state = State.Walk;
            ApplyStateBox(); // the stunned box goes back to the full body
            timer = seconds;
            invulnerable = false;
            Play("Move", 0.15f);
        }

        private void BeginAttack(BossAttackKind kind)
        {
            switch (kind)
            {
                case BossAttackKind.Charge:
                    FacePlayer();
                    state = State.AnticipateCharge;
                    timer = data.anticipateSeconds;
                    Play("Anticipate", 0.1f);
                    break;
                case BossAttackKind.Slam:
                    state = State.AnticipateSlam;
                    timer = data.anticipateSeconds;
                    Play("Anticipate", 0.1f);
                    break;
                case BossAttackKind.Rain:
                    BeginRain();
                    break;
                case BossAttackKind.Eruption:
                    FacePlayer();
                    SpawnEruptionPillars();
                    state = State.AnticipateEruption;
                    timer = data.eruptionChargeSeconds;
                    Sfx.Play(SfxId.BossRoar);
                    Play("Anticipate", 0.1f);
                    break;
            }
        }

        /// <summary>Every attack (gimmick) ends in the stun: the only window in which the boss can be damaged.</summary>
        private void FinishAttack() => EnterStun(cutIn: true);

        private void EnterStun(bool cutIn)
        {
            attackIndex++;
            state = State.Stunned;
            ApplyStateBox();
            SinkPillars(0f); // eruption islands go down when the attack is over
            eruptionPillarCells.Clear();
            timer = Phase.stunSeconds;
            if (data.pillarsOnStun && SpawnStunPillars())
                timer += data.pillarTelegraphSeconds + data.pillarRiseSeconds; // the open window starts when the staircase is up
            stunPhase = PhaseIndex;
            flinchTimer = 0f;
            stompCooldown = 0f; // dodgeEarned carries over: a dodge during this attack holds this stun open
            Play("Stunned", 0.12f);
            if (cutIn && stage != null)
                NowCutIn.Play(stage.Player); // "Now!" cut-in: a short time stop on the player's eyes
        }

        // ---------------------------------------------------------------- movement

        private void Face(int direction)
        {
            facing = direction >= 0 ? 1 : -1;
            if (visual != null)
                visual.localRotation = Quaternion.Euler(0f, facing * (90f + faceCameraAngle), 0f);
        }

        private void FacePlayer()
        {
            if (stage.Player != null)
                Face(stage.Player.position.x >= body.position.x ? 1 : -1);
        }

        private void MoveX(float x)
        {
            var p = body.position;
            p.x = Mathf.Clamp(x, arenaMinX + HalfWidth, arenaMaxX - HalfWidth);
            body.MovePosition(p);
        }

        private void WalkTowardPlayer(float speed, float dt)
        {
            float dx = stage.Player.position.x - body.position.x;
            Face(dx >= 0f ? 1 : -1);
            // Stop at a small gap instead of pushing into the player (a player against the wall is never pressed into it).
            float gap = Mathf.Abs(dx) - HalfWidth - data.approachGap;
            if (gap > 0f)
                MoveX(body.position.x + facing * Mathf.Min(speed * dt, gap));
        }

        private void BeginCharge()
        {
            chargeDir = facing;
            state = State.Charge;
            Sfx.Play(SfxId.BossCharge);
            // The trigger keeps its full height so a head stomp is still detected mid-charge; the lower
            // lethal band (chargeHitHeight) is applied in Contact instead of by shrinking the box.
            Play(HasState("Charge") ? "Charge" : "Attack", 0.05f);
        }

        private void StepCharge(float dt)
        {
            float limit = chargeDir > 0f ? arenaMaxX - HalfWidth : arenaMinX + HalfWidth;
            float next = body.position.x + chargeDir * data.chargeSpeed * dt;
            bool done = chargeDir > 0f ? next >= limit : next <= limit;
            MoveX(done ? limit : next);
            if (!done)
                return;
            // Wall crash: the charge slams into the arena wall and goes straight into the stun.
            Sfx.Play(SfxId.BossSlam);
            FinishAttack();
            Play("Stunned", 0.05f);
        }

        // ---------------------------------------------------------------- slam

        private void BeginSlam()
        {
            state = State.Slam;
            timer = data.slamImpactDelay;
            Play(HasState("Slam") ? "Slam" : "Attack", 0.05f);
        }

        // ---------------------------------------------------------------- eruption

        private void BeginEruptionSlam()
        {
            state = State.Eruption;
            timer = data.slamImpactDelay;
            eruptionFired = false;
            eruptionClock = 0f;
            Play(HasState("Slam") ? "Slam" : "Attack", 0.05f);
        }

        private void StepEruption(float dt)
        {
            if (!eruptionFired)
            {
                timer -= dt;
                if (timer > 0f)
                    return;
                eruptionFired = true;
                SetGlow(0f);
                Sfx.Play(SfxId.BossSlam);
                eruptionDuration = SpawnEruption() + 0.2f;
                return;
            }
            eruptionClock += dt;
            if (eruptionClock >= eruptionDuration)
                FinishAttack();
        }

        /// <summary>Spawns the eruption cells outward on both sides of the boss; returns the seconds until the last one is over.</summary>
        private float SpawnEruption()
        {
            if (data.eruptionPrefab == null)
            {
                Debug.LogError($"[Boss {data.bossId}] eruptionPrefab is not assigned; the eruption has no lava columns.", this);
                return 0.5f;
            }
            float last = 0f;
            for (int dir = -1; dir <= 1; dir += 2)
            {
                for (int i = 0; ; i++)
                {
                    float x = EruptionCellX(dir, i);
                    if (x < arenaMinX + data.eruptionWidth * 0.25f || x > arenaMaxX - data.eruptionWidth * 0.25f)
                        break;
                    if (dir == eruptionSide && eruptionPillarCells.Contains(i))
                        continue; // safe island: a pillar stands here, the cell never erupts
                    float delay = i * data.eruptionStepSeconds;
                    var go = Instantiate(data.eruptionPrefab, new Vector3(x, FloorY, 0f), Quaternion.identity, transform.parent);
                    spawned.Add(go);
                    var cell = go.GetComponent<BossEruption>();
                    if (cell == null)
                    {
                        Debug.LogError($"[Boss {data.bossId}] eruptionPrefab has no BossEruption.", this);
                        continue;
                    }
                    cell.Init(stage, delay, data.eruptionWarnSeconds, data.eruptionActiveSeconds, data.eruptionWidth, data.eruptionHeight);
                    last = Mathf.Max(last, delay + data.eruptionWarnSeconds + data.eruptionActiveSeconds + 0.3f);
                }
            }
            return last;
        }

        private void SpawnWaves()
        {
            if (data.wavePrefab == null)
            {
                Debug.LogError($"[Boss {data.bossId}] wavePrefab is not assigned; the slam has no shockwave.", this);
                return;
            }
            for (int dir = -1; dir <= 1; dir += 2)
            {
                var go = Instantiate(data.wavePrefab, new Vector3(body.position.x + dir * HalfWidth, FloorY, 0f), Quaternion.identity, transform.parent);
                spawned.Add(go);
                var hazard = go.GetComponent<BossHazard>();
                if (hazard == null)
                {
                    Debug.LogError($"[Boss {data.bossId}] wavePrefab has no BossHazard.", this);
                    continue;
                }
                hazard.InitWave(stage, dir, data.waveSpeed, arenaMinX, arenaMaxX);
            }
        }

        // ---------------------------------------------------------------- rain

        private void BeginRain()
        {
            FacePlayer();
            state = State.Rain;
            rainClock = 0f;
            rainSpawned = 0;
            rainX.Clear();
            rainMarkers.Clear();
            int count = Mathf.Max(1, Phase.rainCount);
            float lo = arenaMinX + 1f, hi = arenaMaxX - 1f;
            // The first object targets the player's position at the start; the rest are spread over the arena.
            rainX.Add(Mathf.Clamp(stage.Player.position.x, lo, hi));
            for (int i = 1; i < count; i++)
            {
                float x = rainX[0];
                for (int tries = 0; tries < 12; tries++)
                {
                    x = Random.Range(lo, hi);
                    bool ok = true;
                    foreach (float other in rainX)
                        ok &= Mathf.Abs(other - x) >= data.rainMinSpacing;
                    if (ok)
                        break;
                }
                rainX.Add(x);
            }
            foreach (float x in rainX)
            {
                GameObject marker = null;
                if (data.markerPrefab != null)
                {
                    marker = Instantiate(data.markerPrefab, new Vector3(x, FloorY + 0.05f, 0f), Quaternion.identity, transform.parent);
                    spawned.Add(marker);
                }
                rainMarkers.Add(marker);
            }
            Play(HasState("Shake") ? "Shake" : "Attack", 0.1f);
        }

        private void StepRain(float dt)
        {
            rainClock += dt;
            // Markers swell while they wait (readable warning), then each object falls.
            for (int i = 0; i < rainMarkers.Count; i++)
            {
                var m = rainMarkers[i];
                if (m != null)
                    m.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(rainClock / data.markerSeconds));
            }
            while (rainSpawned < rainX.Count && rainClock >= data.markerSeconds + rainSpawned * data.rainInterval)
                SpawnFalling(rainSpawned++);
            float end = data.markerSeconds + (rainX.Count - 1) * data.rainInterval + data.fallHeight / data.fallSpeed + 0.6f;
            if (rainClock >= end)
                FinishAttack();
        }

        private void SpawnFalling(int index)
        {
            if (data.fallingPrefab == null)
            {
                Debug.LogError($"[Boss {data.bossId}] fallingPrefab is not assigned; the rain has no objects.", this);
                return;
            }
            float x = rainX[index];
            float landY = FloorY;
            Vector3 from = new Vector3(x, FloorY + data.fallHeight, 0f);
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, data.fallHeight + 5f, ~0, QueryTriggerInteraction.Ignore)
                && !PlayerMotor.TryGetFromCollider(hit.collider, out _))
                landY = hit.point.y;
            var go = Instantiate(data.fallingPrefab, from, Quaternion.identity, transform.parent);
            spawned.Add(go);
            var hazard = go.GetComponent<BossHazard>();
            if (hazard == null)
            {
                Debug.LogError($"[Boss {data.bossId}] fallingPrefab has no BossHazard.", this);
                return;
            }
            hazard.InitFall(stage, landY, data.fallSpeed, rainMarkers[index]);
            rainMarkers[index] = null; // now owned by the hazard
        }

        // ---------------------------------------------------------------- contact

        private void OnTriggerEnter(Collider other) => Contact(other);
        private void OnTriggerStay(Collider other) => Contact(other);

        private void Contact(Collider other)
        {
            if (stage == null || state == State.Dormant || state == State.Defeat || state == State.Done)
                return;
            if (stage.PlayerDead)
                return; // the player died first: the falling body neither stomps nor gets hit again
            if (!PlayerMotor.TryGetFromCollider(other, out var player))
                return;

            float feetY = Mathf.Max(player.FeetY, player.StepStartFeetY); // the overlap was detected from the pre-step pose
            // Flung over the boss out of a corner: the flight is harmless both ways (only the Charge attack still counts).
            if (cornerFlight > 0f && state != State.Charge)
                return;
            bool above = feetY >= TopY - data.stompTolerance;
            // Pillar fights: a stomp only counts from high up (the pillars); a low touch is a plain knockback.
            if (above && state == State.Stunned && data.minStompFeetHeight > 0f && feetY - FloorY < data.minStompFeetHeight)
                above = false;
            bool falling = player.Velocity.y < 0f || player.RecentlyBounced;
            if (above)
            {
                // From above the boss is never lethal: a stomp bounces the player like a monster hop, but only the
                // stunned (weak point open) boss takes damage. Rising out of the bounce through the head is harmless.
                if (!falling)
                    return;
                bool damage = state == State.Stunned && !invulnerable && stompCooldown <= 0f;
                if (data.bounceOffSpeed > 0f && player.RecentlyBounced)
                    return; // this landing already bounced and was pushed off
                player.Bounce();
                if (data.bounceOffSpeed > 0f)
                    BounceOff(player, other.bounds.extents.x);
                if (damage)
                    TakeHit();
                else
                    Sfx.Play(SfxId.Stomp);
                return;
            }
            // Groggy from a perfect dodge: the dodge may have ended inside the body, so touching it is harmless until the stomp.
            if (state == State.Stunned && dodgeEarned)
                return;
            if (state == State.Charge)
            {
                // The charge is the attack: only its low band hurts (a player clearing it passes harmlessly), and a
                // dodge through it counts as a perfect dodge.
                if (feetY >= transform.position.y + data.chargeHitHeight)
                    return;
                stage.NotifyPlayerHit(dodgeable: true);
                return;
            }
            // Plain body contact is not an attack: it pushes the player back instead of killing (dodging passes through).
            if (player.IsDodging || player.IsKnockedBack || contactGrace > 0f)
                return;
            float dx = player.transform.position.x - body.position.x;
            float dir = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : facing;
            float half = other.bounds.extents.x;
            // Room the push needs to get clear of the body vs the room the wall behind the player leaves.
            float wallRoom = WallDistance(new Vector3(player.transform.position.x, other.bounds.center.y, 0f), dir) - half;
            float neededRoom = HalfWidth + half + data.cornerMargin - Mathf.Abs(dx);
            if (wallRoom < neededRoom)
                KnockOverBoss(player, dir, half, feetY);
            else
            {
                player.Knockback(dir, data.knockbackSpeed, data.knockbackLift, data.knockbackSeconds);
                contactGrace = data.contactGraceSeconds;
            }
            Sfx.Play(SfxId.Stomp);
        }

        /// <summary>
        /// Bounce-off (BossData.bounceOffSpeed): after a bounce on the boss the player is pushed off the body toward the side they
        /// are on (the other side when a wall leaves no landing room), fast enough to clear the body before falling back to the
        /// bounce height. Steering is off for that flight, so standing still on top never loops.
        /// </summary>
        private void BounceOff(PlayerMotor player, float playerHalfWidth)
        {
            float px = player.transform.position.x;
            float dx = px - body.position.x;
            float dir = Mathf.Abs(dx) > 0.05f ? Mathf.Sign(dx) : -facing; // dead centre: off the back
            float clear = HalfWidth + playerHalfWidth + 0.3f;
            var from = new Vector3(px, player.transform.position.y + 1f, 0f);
            if (WallDistance(from, dir) - playerHalfWidth < clear - dir * dx + 0.2f)
                dir = -dir; // no room to land on this side
            float distance = Mathf.Max(0f, clear - dir * dx);
            float vy = Mathf.Max(1f, player.Velocity.y);
            float air = Mathf.Max(0.15f, player.AirTimeFor(vy));
            float speedX = Mathf.Max(data.bounceOffSpeed, distance / air);
            player.Knockback(dir, speedX, vy, air);
        }

        /// <summary>
        /// Distance from <paramref name="from"/> to the nearest solid obstacle along <paramref name="dir"/>, capped at the arena edge.
        /// The physical wall can stand inside arenaMinX / arenaMaxX (the closed entrance gate's face is half its thickness in).
        /// </summary>
        private float WallDistance(Vector3 from, float dir)
        {
            float edge = Mathf.Max(0f, dir < 0f ? from.x - arenaMinX : arenaMaxX - from.x);
            if (Physics.Raycast(from, new Vector3(dir < 0f ? -1f : 1f, 0f, 0f), out RaycastHit hit, edge, ~0, QueryTriggerInteraction.Ignore))
                return Mathf.Min(edge, hit.distance);
            return edge;
        }

        /// <summary>
        /// Corner escape: the wall behind the player leaves no room to be pushed clear, so the push goes OVER the boss toward the
        /// open side (a high hop that clears the body; steering stays off for the whole flight and contact is ignored meanwhile).
        /// </summary>
        private void KnockOverBoss(PlayerMotor player, float awayDir, float playerHalfWidth, float feetY)
        {
            float over = -awayDir;
            float playerX = player.transform.position.x;
            float targetX = body.position.x + over * (HalfWidth + playerHalfWidth + data.cornerMargin);
            targetX = Mathf.Clamp(targetX, arenaMinX + playerHalfWidth, arenaMaxX - playerHalfWidth);
            float rise = Mathf.Max(0.5f, FloorY + data.bodyHeight + data.cornerClearance - feetY);
            float launch = Mathf.Min(player.LaunchSpeedForHeight(rise), data.cornerMaxLaunchSpeed);
            float air = Mathf.Max(0.2f, player.AirTimeFor(launch, Mathf.Max(0f, feetY - FloorY)));
            float speedX = Mathf.Abs(targetX - playerX) / air;
            player.Knockback(over, speedX, launch, air);
            cornerFlight = air + 0.1f;
            contactGrace = Mathf.Max(data.contactGraceSeconds, air + 0.3f);
        }

        // ---------------------------------------------------------------- pillars

        private PlayerMotor Motor()
        {
            if (playerMotor == null && stage != null && stage.Player != null)
                playerMotor = stage.Player.GetComponent<PlayerMotor>();
            return playerMotor;
        }

        private float EruptionCellX(int dir, int index) => body.position.x + dir * (HalfWidth + data.eruptionSpacing * (index + 0.5f));

        private BossPillar AddPillar(float x, float width, float height)
        {
            var pillar = BossPillar.Create(transform.parent, data.pillarPrefab, data.pillarDustPrefab, Motor(), x, FloorY, width, height,
                data.pillarTelegraphSeconds, data.pillarRiseSeconds);
            pillars.Add(pillar);
            return pillar;
        }

        private bool FitsArena(float centerX, float width) =>
            centerX - width * 0.5f >= arenaMinX + 0.1f && centerX + width * 0.5f <= arenaMaxX - 0.1f;

        /// <summary>
        /// Stun staircase on the player's side: a high pillar beside the body and a low one further out (floor -> low -> high -> boss).
        /// Falls back to the other side when the player's side is too narrow. Returns false when no pillar fits.
        /// </summary>
        private bool SpawnStunPillars()
        {
            if (stage == null || stage.Player == null)
                return false;
            float w = data.pillarWidth;
            float highOffset = HalfWidth + data.pillarBossGap + w * 0.5f;
            float lowOffset = highOffset + w + data.pillarSpacing;
            float bx = body.position.x;
            int side = stage.Player.position.x >= bx ? 1 : -1;
            if (!FitsArena(bx + side * lowOffset, w))
            {
                int other = -side;
                if (FitsArena(bx + other * lowOffset, w) || !FitsArena(bx + side * highOffset, w))
                    side = other;
            }
            bool any = false;
            if (FitsArena(bx + side * highOffset, w))
            {
                AddPillar(bx + side * highOffset, w, data.pillarHighHeight);
                any = true;
            }
            if (FitsArena(bx + side * lowOffset, w))
            {
                AddPillar(bx + side * lowOffset, w, data.pillarLowHeight);
                any = true;
            }
            if (!any)
                Debug.LogWarning($"[Boss {data.bossId}] no room for the stun pillars (boss x {bx:F1}, arena {arenaMinX:F1}..{arenaMaxX:F1}).", this);
            return any;
        }

        /// <summary>Eruption safe islands: pillars on the player's side at the configured cells; those cells never erupt.</summary>
        private void SpawnEruptionPillars()
        {
            eruptionPillarCells.Clear();
            if (!data.pillarsOnEruption || data.eruptionPillarCells == null || stage == null || stage.Player == null)
                return;
            eruptionSide = stage.Player.position.x >= body.position.x ? 1 : -1;
            float w = data.eruptionPillarWidth;
            foreach (int i in data.eruptionPillarCells)
            {
                if (i < 0 || eruptionPillarCells.Contains(i))
                    continue;
                float x = EruptionCellX(eruptionSide, i);
                if (!FitsArena(x, w))
                    continue;
                AddPillar(x, w, data.eruptionPillarHeight);
                eruptionPillarCells.Add(i);
            }
        }

        /// <summary>
        /// The stun clock ends the staircase: it shakes for the last pillarWarnSeconds, then sinks. A stun held open by a perfect
        /// dodge keeps (or rebuilds) the staircase until the first stomp, which needs it.
        /// </summary>
        private void TickStunPillars()
        {
            if (dodgeEarned)
            {
                if (pillars.Count == 0)
                    SpawnStunPillars();
            }
            else if (pillars.Count > 0 && timer <= data.pillarWarnSeconds)
                SinkPillars(Mathf.Max(0f, timer));
        }

        /// <summary>Starts the sink of every pillar (a shake of warnSeconds first); they remove themselves when they are down.</summary>
        private void SinkPillars(float warnSeconds)
        {
            foreach (var pillar in pillars)
            {
                if (pillar != null)
                    pillar.BeginSink(warnSeconds, data.pillarSinkSeconds);
            }
            pillars.Clear();
        }

        private void TakeHit()
        {
            hp--;
            if (hud != null)
                hud.SetHp(hp);
            Damaged?.Invoke();
            if (hp <= 0)
            {
                box.size = baseBoxSize;
                box.center = baseBoxCenter;
                SetGlow(0f);
                invulnerable = true;
                state = State.Defeat;
                // The boss fell first: leftover hazards and pits no longer kill the player before the clear.
                stage.BlockPlayerDeath(new Vector3(arenaMinX + data.engageDistance, FloorY + 1.2f, 0f));
                popped = false;
                timer = data.defeatSeconds;
                Voice.PlayOr(VoiceSlot.BossDefeat, SfxId.BossDefeat); // the player's victory line on top, when they recorded one
                SinkPillars(0f); // a player on a pillar rides down instead of the pillar vanishing
                ClearSpawned();
                Play("Defeat", 0.05f);
                if (data.defeatFxPrefab != null)
                    spawned.Add(Instantiate(data.defeatFxPrefab, transform.position + Vector3.up * (data.bodyHeight * 0.5f), Quaternion.identity, transform.parent));
                ReleaseArena();
                return;
            }
            // Stay stunned: flinch, then more stomps can follow while the stun window lasts.
            dodgeEarned = false; // the guaranteed stomp is used up
            flinchTimer = data.hitFlinchSeconds;
            stompCooldown = data.stompCooldownSeconds;
            SetGlow(1f);
            Sfx.Play(data.hitClip, data.hitVolume, SfxId.BossHit);
            Play("Hit", 0f); // restart the flinch from frame 0 on every stomp
        }

        // ---------------------------------------------------------------- defeat pop

        /// <summary>
        /// Defeat: the Defeat clip plays, the body swells and glows for a moment, then bursts (BossDefeatBurst) and is gone.
        /// The rest of defeatSeconds shows the empty arena before the stage clear. Retry brings the body back (ResetBoss).
        /// </summary>
        private void StepDefeatPop()
        {
            if (popped)
                return;
            float popAt = Mathf.Clamp(data.defeatPopDelay, 0f, Mathf.Max(0f, data.defeatSeconds - 0.3f));
            float elapsed = data.defeatSeconds - timer;
            float swell = Mathf.Clamp01((elapsed - (popAt - PopSwellSeconds)) / PopSwellSeconds);
            if (swell > 0f)
            {
                if (visual != null)
                    visual.localScale = baseVisualScale * (1f + 0.18f * swell * swell);
                SetGlow(swell);
            }
            if (elapsed < popAt)
                return;

            popped = true;
            // Burst where the body is now (the Defeat clip may have laid it down), sized to what is on screen.
            Vector3 center = transform.position + Vector3.up * (data.bodyHeight * 0.5f);
            float radius = Mathf.Max(data.bodyWidth, data.bodyHeight) * 0.5f;
            if (visual != null)
            {
                bool any = false;
                var bounds = new Bounds(center, Vector3.zero);
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled)
                        continue;
                    if (any) bounds.Encapsulate(r.bounds);
                    else { bounds = r.bounds; any = true; }
                }
                if (any)
                {
                    center = new Vector3(bounds.center.x, bounds.center.y, transform.position.z);
                    radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.y), radius * 0.6f, radius * 1.6f);
                }
                SetGlow(0f);
                visual.localScale = baseVisualScale;
                visual.gameObject.SetActive(false);
            }
            var burst = BossDefeatBurst.Spawn(transform.parent, center, radius, data);
            if (burst != null)
                spawned.Add(burst.gameObject);
            Sfx.Play(SfxId.BossSlam);
        }

        // ---------------------------------------------------------------- helpers

        private bool HasState(string name) =>
            animator != null && animator.isActiveAndEnabled && animator.HasState(0, Animator.StringToHash(name));

        private void Play(string clip, float fade)
        {
            if (!HasState(clip))
                return;
            int hash = Animator.StringToHash(clip);
            if (fade <= 0f)
                animator.Play(hash, 0, 0f);
            else
                animator.CrossFadeInFixedTime(hash, fade, 0);
        }

        private void SetGlow(float amount)
        {
            if (glowRenderers == null)
                return;
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                var r = glowRenderers[i];
                if (r == null)
                    continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColorId, Color.white * (0.6f + 2.2f * amount));
                if (tintBase)
                    block.SetColor(BaseColorId, Color.Lerp(new Color(0.55f, 0.35f, 0.3f), new Color(1f, 0.85f, 0.3f), amount));
                r.SetPropertyBlock(block);
            }
        }

        private void ClearSpawned()
        {
            foreach (var go in spawned)
            {
                if (go != null)
                    Destroy(go);
            }
            spawned.Clear();
            foreach (var pillar in pillars)
            {
                if (pillar != null)
                    Destroy(pillar.gameObject);
            }
            pillars.Clear();
            eruptionPillarCells.Clear();
            rainMarkers.Clear();
            rainX.Clear();
        }

        private void OnDestroy()
        {
            if (stage != null)
                stage.PerfectDodged -= OnPerfectDodged;
            if (hud != null)
                Destroy(hud.gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            float y = Application.isPlaying ? start.y : transform.position.y;
            Gizmos.DrawLine(new Vector3(arenaMinX, y, 0f), new Vector3(arenaMaxX, y, 0f));
            Gizmos.DrawLine(new Vector3(arenaMinX, y, 0f), new Vector3(arenaMinX, y + 6f, 0f));
            Gizmos.DrawLine(new Vector3(arenaMaxX, y, 0f), new Vector3(arenaMaxX, y + 6f, 0f));
        }
    }
}
