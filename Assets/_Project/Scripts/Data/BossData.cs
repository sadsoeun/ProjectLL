using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>The attacks a boss phase can chain. Each kind is implemented once in BossController and shared by all bosses.</summary>
    public enum BossAttackKind
    {
        /// <summary>Telegraph, then a run across the arena at a lowered hit height (jump over it or stand on a ledge).</summary>
        Charge = 0,
        /// <summary>Telegraph, then shockwaves travel left and right along the floor (jumpable).</summary>
        Slam = 1,
        /// <summary>Shadow markers appear, then objects fall onto them (acorns / sandstone chunks).</summary>
        Rain = 2,
        /// <summary>Long charge-up, a slam into the floor, then lava columns erupt cell by cell outward from the boss (step into a cell
        /// that has just erupted). Uses <see cref="BossData.eruptionPrefab"/>.</summary>
        Eruption = 3,
    }

    /// <summary>Tuning of one boss phase. The phases split the HP evenly (3 phases over 10 HP: hits 0-3, 4-6, 7-9).</summary>
    [Serializable]
    public sealed class BossPhase
    {
        [Tooltip("Walking speed toward the player between attacks (m/s).")]
        [Min(0f)] public float moveSpeed = 2.5f;
        [Tooltip("Seconds of walking before the next attack of the chain starts.")]
        [Min(0f)] public float walkSeconds = 1.5f;
        [Tooltip("Seconds the boss is stunned (weak point open) after the whole chain.")]
        [Min(0.5f)] public float stunSeconds = 1.5f;
        [Tooltip("Attacks of one cycle, in order. After the last one the boss is stunned.")]
        public BossAttackKind[] attacks = { BossAttackKind.Charge };
        [Tooltip("Number of falling objects for a Rain attack in this phase.")]
        [Min(1)] public int rainCount = 3;
    }

    /// <summary>
    /// Data of one boss (Data/Bosses/Boss_{bossId}.asset). Everything a boss fight needs is tunable here; the behaviour is the
    /// shared BossController. The stage layout only names the boss (bossId) and the arena; see Docs/stage-layout-schema.md and
    /// Docs/boss-framework.md. HP = number of head stomps; the HUD shows it as a percentage. Every stun window can take
    /// several stomps in a row (each one plays the Hit flinch), and the phases are spread evenly over the HP.
    /// </summary>
    [CreateAssetMenu(menuName = "MyGame/Boss Data", fileName = "Boss_9000")]
    public sealed class BossData : ScriptableObject
    {
        [Header("Identity")]
        public int bossId = 9001;
        [Tooltip("Localization key of the name shown on the HP bar: boss.<id>.name")]
        public string nameKey = "boss.9001.name";
        [Tooltip("Gameplay prefab (BossController + model). Empty = the builder uses a generated capsule placeholder.")]
        public GameObject prefab;

        [Header("Body")]
        [Tooltip("Head stomps needed to defeat the boss (shown as a percentage on the HP bar).")]
        [Min(1)] public int maxHp = 10;
        [Min(0.5f)] public float bodyWidth = 3f;
        [Min(0.5f)] public float bodyHeight = 3.5f;
        [Tooltip("The player's feet must be at most this far below the weak point to count as a head stomp.")]
        [Min(0f)] public float stompTolerance = 0.7f;
        [Tooltip("Runtime scale of the model (the `visual` child), applied on Awake so a re-import of the prefab keeps it. bodyWidth / bodyHeight are NOT scaled: set them for the scaled model.")]
        [Min(0.1f)] public float visualScale = 1f;
        [Tooltip("0 = off. Otherwise a stomp only damages when the player's feet are at least this far above the arena floor (so it needs the pillars); below that, touching the boss is a plain knockback.")]
        [Min(0f)] public float minStompFeetHeight = 0f;
        [Tooltip("0 = off. While Stunned, the trigger box is this tall (m above the floor, width unchanged) so the stomp lands on the sagging body, not on the standing-height box.")]
        [Min(0f)] public float stunnedBoxHeight = 0f;
        [Tooltip("0 = off. Every bounce on the boss (any state) also pushes the player off the body at least this fast (m/s, sized up so they land clear of it): one stomp per climb, no bounce loop on top.")]
        [Min(0f)] public float bounceOffSpeed = 0f;

        [Header("Body contact (knockback instead of death; the Charge attack still kills)")]
        [Tooltip("Horizontal push speed away from the boss (m/s).")]
        [Min(0f)] public float knockbackSpeed = 9f;
        [Tooltip("Upward speed of the push hop (m/s).")]
        [Min(0f)] public float knockbackLift = 6f;
        [Tooltip("Seconds without steering while pushed.")]
        [Min(0.05f)] public float knockbackSeconds = 0.35f;
        [Tooltip("After any body-contact knockback, plain body contact does nothing for this long (attacks and hazards still hit). Stops knock-back loops.")]
        [Min(0f)] public float contactGraceSeconds = 0.8f;
        [Tooltip("Corner escape: extra free space (m) beyond the boss that the knockback must reach. When the wall behind the player leaves less room, the push goes OVER the boss instead.")]
        [Min(0f)] public float cornerMargin = 0.5f;
        [Tooltip("Corner escape: how far (m) above the top of the body the player's feet must pass.")]
        [Min(0f)] public float cornerClearance = 0.6f;
        [Tooltip("Corner escape: upper limit of the launch speed (m/s) of the hop over the boss.")]
        [Min(5f)] public float cornerMaxLaunchSpeed = 26f;
        [Tooltip("The boss stops walking when the gap between the body edge and the player is this small (m), instead of pushing into the player.")]
        [Min(0.1f)] public float approachGap = 0.6f;

        [Header("Timing")]
        [Tooltip("Roar at the start of the fight; the boss is invulnerable and the player can move.")]
        [Min(0.5f)] public float introSeconds = 1.5f;
        [Tooltip("Telegraph before Charge / Slam (readable warning).")]
        [Min(0.6f)] public float anticipateSeconds = 0.9f;
        [Tooltip("Hit flinch after a damaging stomp: the Hit clip plays, then the boss returns to the stun pose.")]
        [Min(0.1f)] public float hitFlinchSeconds = 0.4f;
        [Tooltip("Minimum time between two damaging stomps (one bounce cannot count twice).")]
        [Min(0.05f)] public float stompCooldownSeconds = 0.3f;
        [Min(0.5f)] public float defeatSeconds = 2.5f;
        [Tooltip("The fight starts when the player is this far inside the arena (past arenaMinX).")]
        [Min(1f)] public float engageDistance = 2.5f;

        [Header("Charge")]
        [Min(1f)] public float chargeSpeed = 11f;
        [Tooltip("Lethal height of the body while charging (the boss lowers its head); a full jump clears it.")]
        [Min(0.5f)] public float chargeHitHeight = 2f;
        [Min(0f)] public float chargeEndPause = 0.7f;

        [Header("Slam (shockwave)")]
        [Tooltip("Seconds from the start of the Attack clip to the impact.")]
        [Min(0.1f)] public float slamImpactDelay = 0.45f;
        [Min(1f)] public float waveSpeed = 7f;
        [Min(0f)] public float slamRecoverSeconds = 0.8f;
        public GameObject wavePrefab;

        [Header("Rain (falling objects with shadow markers)")]
        [Tooltip("Seconds the shadow marker is visible before the object falls.")]
        [Min(0.6f)] public float markerSeconds = 1f;
        [Min(0f)] public float rainInterval = 0.25f;
        [Min(1f)] public float fallSpeed = 18f;
        [Min(3f)] public float fallHeight = 12f;
        [Min(1f)] public float rainMinSpacing = 2.4f;
        public GameObject fallingPrefab;
        public GameObject markerPrefab;

        [Header("Eruption (charge-up, floor slam, lava columns erupting outward cell by cell)")]
        [Tooltip("Charge-up (glow builds) before the floor slam.")]
        [Min(0.6f)] public float eruptionChargeSeconds = 1.6f;
        [Tooltip("Distance between eruption cells (m).")]
        [Min(1f)] public float eruptionSpacing = 2.4f;
        [Tooltip("Lethal width of one column (m); a little less than the spacing.")]
        [Min(0.5f)] public float eruptionWidth = 2f;
        [Min(1f)] public float eruptionHeight = 3.6f;
        [Tooltip("Delay between one cell and the next one outward (s): the eruption front moves at spacing / step.")]
        [Min(0.1f)] public float eruptionStepSeconds = 0.6f;
        [Tooltip("Glowing crack before a cell erupts (s).")]
        [Min(0.2f)] public float eruptionWarnSeconds = 0.8f;
        [Tooltip("Seconds a column is lethal.")]
        [Min(0.1f)] public float eruptionActiveSeconds = 0.32f;
        public GameObject eruptionPrefab;

        [Header("Pillars (optional gimmick: rising basalt pillars the player stands on; all off by default)")]
        [Tooltip("Visual prefab, pivot at the TOP-surface centre, column extending ~7 m downward, renderers only. Empty = primitive placeholder. The code adds the colliders.")]
        public GameObject pillarPrefab;
        [Tooltip("Two pillars rise beside the stunned boss as a staircase (low, then high next to the boss). The stun clock starts after they are up.")]
        public bool pillarsOnStun;
        [Tooltip("Two safe-island pillars rise during the Eruption attack; the eruption skips their cells.")]
        public bool pillarsOnEruption;
        [Tooltip("Width (X) of a stun pillar's top (m).")]
        [Min(0.5f)] public float pillarWidth = 2.4f;
        [Tooltip("Top height above the floor of the low (outer) stun pillar (m).")]
        [Min(0.5f)] public float pillarLowHeight = 2f;
        [Tooltip("Top height above the floor of the high stun pillar next to the boss (m).")]
        [Min(0.5f)] public float pillarHighHeight = 3.6f;
        [Tooltip("Gap between the boss body and the high pillar (m). Keep it wider than the player so standing on the pillar never touches the boss.")]
        [Min(0.3f)] public float pillarBossGap = 0.8f;
        [Tooltip("Gap between the two stun pillars (m).")]
        [Min(0.3f)] public float pillarSpacing = 1f;
        [Tooltip("Cracks / rumble before the pillar rises (s).")]
        [Min(0.1f)] public float pillarTelegraphSeconds = 0.5f;
        [Min(0.1f)] public float pillarRiseSeconds = 0.5f;
        [Min(0.1f)] public float pillarSinkSeconds = 0.4f;
        [Tooltip("Shake before the pillar sinks (s).")]
        [Min(0.1f)] public float pillarWarnSeconds = 0.4f;
        [Tooltip("Eruption safe islands: top height above the floor (m). The cell does not erupt, so any reachable height works.")]
        [Min(0.5f)] public float eruptionPillarHeight = 2.4f;
        [Tooltip("Eruption safe islands: width (m); keep it at most the eruption width so the neighbouring columns cannot reach the top.")]
        [Min(0.5f)] public float eruptionPillarWidth = 2f;
        [Tooltip("Eruption cell indices (0 = next to the boss, on the player's side) that get a safe-island pillar instead of a column.")]
        public int[] eruptionPillarCells = { 1, 3 };
        [Tooltip("Optional puff at the foot of a pillar when it rises / sinks.")]
        public GameObject pillarDustPrefab;

        [Header("Fight feedback")]
        [Tooltip("This boss's own stomp-hit sound. Empty = the shared BossHit sound of the SfxBank.")]
        public AudioClip hitClip;
        [Range(0f, 1f)] public float hitVolume = 0.9f;
        [Tooltip("Optional burst played at the boss position when it is defeated.")]
        public GameObject defeatFxPrefab;

        [Header("Defeat pop (the body swells, bursts into smoke and stars, and is gone)")]
        [Tooltip("Seconds after the last stomp at which the body bursts (the Defeat clip plays until then). Kept below defeatSeconds, so the empty arena is seen before the stage clear.")]
        [Min(0f)] public float defeatPopDelay = 1.6f;
        [Tooltip("Length of the burst (s).")]
        [Min(0.2f)] public float defeatPopSeconds = 0.7f;
        [Tooltip("Smoke puff colour (the stars use stunStarColor).")]
        public Color defeatPopColor = new Color(1f, 0.97f, 0.9f, 1f);
        [Min(0)] public int defeatPopPuffs = 12;
        [Min(0)] public int defeatPopStars = 7;

        [Header("Stun feedback (stars orbiting the head with a trail)")]
        [Tooltip("Transparent URP Particles/Unlit material (vertex colour) used by the stars and their trails.")]
        public Material stunStarMaterial;
        [Min(1)] public int stunStarCount = 3;
        public Color stunStarColor = new Color(1f, 0.88f, 0.25f, 1f);
        [Min(0.05f)] public float stunStarSize = 0.45f;
        [Tooltip("Orbit radius around the weak point (m).")]
        [Min(0.1f)] public float stunStarRadius = 0.9f;
        [Tooltip("Orbit height above the weak point (m).")]
        public float stunStarHeight = 0.6f;
        [Tooltip("Orbit speed (degrees per second).")]
        public float stunStarSpinDegrees = 300f;
        [Tooltip("Length of the afterimage trail (seconds).")]
        [Min(0f)] public float stunTrailSeconds = 0.25f;

        /// <summary>Phase index for a number of hits taken: the phases split maxHp evenly.</summary>
        public int PhaseIndexFor(int hitsTaken)
        {
            if (phases == null || phases.Length == 0)
                return 0;
            int index = Mathf.FloorToInt(hitsTaken * phases.Length / (float)Mathf.Max(1, maxHp));
            return Mathf.Clamp(index, 0, phases.Length - 1);
        }

        /// <summary>The phase for a number of hits taken.</summary>
        public BossPhase PhaseFor(int hitsTaken)
        {
            if (phases == null || phases.Length == 0)
                return Fallback;
            return phases[PhaseIndexFor(hitsTaken)];
        }

        [Header("Phases (spread evenly over the HP)")]
        public BossPhase[] phases =
        {
            new BossPhase { moveSpeed = 2.5f, walkSeconds = 1.5f, stunSeconds = 1.5f, attacks = new[] { BossAttackKind.Charge }, rainCount = 3 },
            new BossPhase { moveSpeed = 3.2f, walkSeconds = 1.2f, stunSeconds = 1.2f, attacks = new[] { BossAttackKind.Rain, BossAttackKind.Charge }, rainCount = 4 },
            new BossPhase { moveSpeed = 4f, walkSeconds = 0.9f, stunSeconds = 1f, attacks = new[] { BossAttackKind.Charge, BossAttackKind.Rain, BossAttackKind.Charge }, rainCount = 5 },
        };

        private static readonly BossPhase Fallback = new BossPhase();

        /// <summary>Content rules (programming / data errors): returns null when valid, else a description.</summary>
        public string Validate()
        {
            if (bossId < 9001 || bossId > 9999) return $"bossId {bossId} must be in 9001-9999.";
            if (nameKey != $"boss.{bossId}.name") return $"nameKey must be 'boss.{bossId}.name' (got '{nameKey}').";
            if (maxHp < 1) return "maxHp must be >= 1.";
            if (phases == null || phases.Length == 0) return "at least one phase is required.";
            foreach (var p in phases)
            {
                if (p == null || p.attacks == null || p.attacks.Length == 0) return "every phase needs at least one attack.";
                if (p.stunSeconds < 0.5f) return "stunSeconds must be >= 0.5.";
            }
            return null;
        }
    }
}
