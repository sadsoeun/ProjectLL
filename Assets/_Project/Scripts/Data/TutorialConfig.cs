using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>Control hint shown in a prompt. Resolved to the live binding of the active device (InputModeService).</summary>
    public enum TutorialGlyph { None, Move, Jump, Run, Dodge, MapStep, Confirm, GoldenLeaf, Cancel, Check }

    /// <summary>When a step may appear.</summary>
    public enum TutorialStart
    {
        /// <summary>As soon as gameplay starts (and the player is still before endX).</summary>
        StageStart,
        /// <summary>When the player's x reaches startX.</summary>
        ReachX,
        /// <summary>When the checkpoint <c>targetId</c> is activated (StageRoot.CheckpointActivated).</summary>
        CheckpointActivated,
        /// <summary>Boss fight running and the boss is calm: walking between attacks (never during a telegraph, an attack or the roar),
        /// in phase <c>bossPhase</c> or later.</summary>
        BossCalm,
        /// <summary>The boss is groggy with its weak point open.</summary>
        BossStunned,
        /// <summary>The boss appears: during its entrance roar (before the first attack). If that moment is missed
        /// (player in the air the whole roar), it falls back to the first calm moment like <c>BossCalm</c>.</summary>
        BossIntro,
    }

    /// <summary>What completes the practice phase. Passing endX always closes the step as well (it never blocks the course).</summary>
    public enum TutorialGoal
    {
        /// <summary>Visible for goalValue seconds; any move input ends it early.</summary>
        IntroTimer,
        /// <summary>Visible for goalValue seconds.</summary>
        ShowFor,
        /// <summary>Player moved goalValue metres from where the practice began.</summary>
        MoveDistance,
        /// <summary>Run held while moving faster than walk speed on the ground for goalValue seconds.</summary>
        RunFor,
        /// <summary>After a real jump (PlayerMotor.Jumped), grounded inside the zone (zoneMinX..zoneMaxX, feet &gt;= zoneMinFeetY).</summary>
        LandInZone,
        /// <summary>Real collect of special slot <c>specialSlot</c> (StageRoot.SpecialCollected).</summary>
        CollectSpecial,
        /// <summary>Real stomp damage on the enemy placement <c>targetId</c> (StageRoot.EnemyStomped).</summary>
        StompEnemy,
        /// <summary>StageRoot.PerfectDodged (not the dodge press). Optional: endX is the exit.</summary>
        PerfectDodge,
        /// <summary>No practice: the step is done when its explanation closes (e.g. checkpoint, explained after the real activation).</summary>
        ExplainOnly,
        /// <summary>The boss went groggy while the player is alive: its attack was survived (jumped, dodged or simply avoided).</summary>
        BossStunned,
        /// <summary>A real head stomp took boss HP (BossController.Damaged).</summary>
        BossDamaged,
    }

    /// <summary>What an explanation page highlights.</summary>
    public enum TutorialSpotKind
    {
        /// <summary>The player character (circle).</summary>
        Player,
        /// <summary>Fixed world point <c>point</c> (circle).</summary>
        WorldPoint,
        /// <summary>Enemy placement <c>spotId</c>, offset by <c>point</c> (circle). Falls back to the player if missing.</summary>
        Enemy,
        /// <summary>Checkpoint <c>spotId</c>, offset by <c>point</c> (circle).</summary>
        Checkpoint,
        /// <summary>Special collectible of slot <c>spotSlot</c> (circle).</summary>
        Special,
        /// <summary>HUD element: child path <c>spotId</c> under the HUD canvas, or under a root object named by its first segment (e.g. "BossHud/Panel") (rounded rectangle; radius = padding in pixels).</summary>
        HudElement,
        /// <summary>The stage boss, offset by <c>point</c> from its body centre (circle). Falls back to the player if there is none.</summary>
        Boss,
    }

    /// <summary>One explanation page: one highlight and one bubble.</summary>
    [Serializable]
    public sealed class TutorialExplainPage
    {
        public string titleKey;
        public string bodyKey;
        public TutorialGlyph[] glyphs = new TutorialGlyph[0];
        public TutorialSpotKind spot = TutorialSpotKind.Player;
        [Tooltip("Enemy / checkpoint id, or HUD child path (HudElement).")]
        public string spotId;
        public int spotSlot;
        [Tooltip("WorldPoint: the point. Enemy / Checkpoint / Player: offset from the target (metres).")]
        public Vector2 point;
        [Tooltip("Spotlight radius in metres (world targets) or padding in reference pixels (HudElement).")]
        public float radius = 1.7f;
    }

    [Serializable]
    public sealed class TutorialStepDef
    {
        [Tooltip("Persistent id stored in SaveData.tutorial.seen. Never rename once shipped.")]
        public string id;
        [Tooltip("Practice prompt main line (LocalizationTable key).")]
        public string textKey;
        [Tooltip("Optional second, smaller line.")]
        public string subKey;
        public TutorialGlyph[] glyphs = new TutorialGlyph[0];

        [Header("Explanation (phase A: game frozen, spotlight + bubble, advanced with Confirm). Empty = none")]
        public TutorialExplainPage[] explain = new TutorialExplainPage[0];

        [Header("Start")]
        public TutorialStart start = TutorialStart.ReachX;
        public float startX;
        [Tooltip("From this x on, the step may start even inside the pause after the previous completion (0 = never). " +
                 "Use it where waiting would let the player walk past the lesson (leaf ledge) or into the practice enemy.")]
        public float urgentX;
        [Tooltip("May start inside the pause after the previous completion (short windows such as the boss stun).")]
        public bool ignoreGap;
        [Tooltip("BossCalm: lowest boss phase index (0 = from the start of the fight).")]
        public int bossPhase;
        [Tooltip("Only start once this step id has been seen (e.g. run after move). Empty = no requirement.")]
        public string afterStep;
        [Tooltip("Checkpoint id (CheckpointActivated) or enemy placement id (StompEnemy / PerfectDodge practice enemy).")]
        public string targetId;

        [Header("Completion (phase B: practice)")]
        public TutorialGoal goal = TutorialGoal.MoveDistance;
        [Tooltip("Seconds (IntroTimer, ShowFor, RunFor) or metres (MoveDistance).")]
        public float goalValue = 3f;
        public float zoneMinX, zoneMaxX, zoneMinFeetY;
        public int specialSlot;
        [Tooltip("Passing this x closes the step (marked seen) without the action.")]
        public float endX = 1000f;

        [Header("Optional helper (PerfectDodge)")]
        [Tooltip("Shown once in place of the prompt after this many failed tries (0 = never).")]
        public int helperAfterFailures;
        public string helperKey;

        public bool HasExplain => explain != null && explain.Length > 0;
    }

    [Serializable]
    public sealed class StageTutorialDef
    {
        public string stageId;
        public TutorialStepDef[] steps = new TutorialStepDef[0];
    }

    /// <summary>Small practice prompt (phase B).</summary>
    [Serializable]
    public sealed class TutorialViewSettings
    {
        [Min(0.01f)] public float fadeInSeconds = 0.2f;
        [Min(0.01f)] public float fadeOutSeconds = 0.35f;
        [Tooltip("Slide distance of the entrance (reference pixels, 1920x1080).")]
        public float slidePixels = 10f;
        [Tooltip("Stage: gap below the HUD (bottom of the golden-leaf slots). The prompt sits top-centre, over the sky, " +
                 "so it never covers the character or the landing spots.")]
        public float stageGapBelowHud = 20f;
        [Tooltip("Stage: top offset used when the HUD slots are not found.")]
        public float stageFallbackTop = 230f;
        [Tooltip("Gap above the world map stage-info panel.")]
        public float worldMapGap = 22f;
        [Min(200f)] public float maxWidth = 1080f;
        [Min(16)] public int mainFontSize = 32;
        [Min(14)] public int subFontSize = 24;
        [Min(24f)] public float glyphHeight = 50f;
        public Color background = new Color(.018f, .019f, .022f, .95f);
        public Color frame = new Color(.79f, .65f, .42f, .75f);
        public Color doneAccent = new Color(1f, .86f, .5f, 1f);
    }

    /// <summary>Explanation overlay (phase A): dim layer with one highlight hole, gold ring and a bubble.</summary>
    [Serializable]
    public sealed class TutorialExplainSettings
    {
        [Header("Overlay")]
        [Range(0f, 1f)] public float overlayAlpha = 0.72f;
        [Tooltip("Soft edge of the highlight hole (reference pixels).")]
        [Min(0f)] public float featherPixels = 26f;
        [Tooltip("Smallest circle radius (reference pixels), whatever the world radius projects to.")]
        [Min(20f)] public float minSpotPixels = 84f;
        [Tooltip("Corner radius of the rounded-rect highlight (HUD elements).")]
        [Min(0f)] public float rectCornerPixels = 18f;
        [Min(0.01f)] public float fadeInSeconds = 0.3f;
        [Min(0.01f)] public float fadeOutSeconds = 0.25f;
        [Tooltip("How fast the highlight glides to the next page's target (higher = faster).")]
        [Min(1f)] public float spotFollow = 12f;

        [Header("Ring")]
        public Color ringColor = new Color(1f, .80f, .40f, 1f);
        [Min(1f)] public float ringWidth = 4f;
        [Tooltip("Gap between the hole edge and the ring at rest.")]
        public float ringGap = 8f;
        [Tooltip("The ring starts this far outside and shrinks onto the target.")]
        [Min(0f)] public float ringStartOffset = 170f;
        [Min(0.05f)] public float ringShrinkSeconds = 0.5f;
        [Tooltip("Gentle pulse after the shrink (pixels / cycles per second).")]
        public float ringPulsePixels = 4f;
        public float ringPulseSpeed = 0.9f;

        [Header("Bubble")]
        [Min(300f)] public float bubbleWidth = 700f;
        [Tooltip("Gap between the highlight and the bubble.")]
        [Min(0f)] public float bubbleGap = 34f;
        [Min(16)] public int titleFontSize = 36;
        [Min(14)] public int bodyFontSize = 28;
        [Min(12)] public int hintFontSize = 22;
        [Min(24f)] public float glyphHeight = 48f;
        public Color bubbleColor = new Color(.018f, .019f, .022f, .97f);
        public Color titleColor = new Color(1f, .80f, .45f, 1f);

        [Header("Pacing")]
        [Tooltip("Confirm is ignored until a page was readable this long (stops mashing through).")]
        [Min(0f)] public float minReadSeconds = 0.8f;
        [Tooltip("Hold Cancel this long to skip the whole tutorial.")]
        [Min(0.2f)] public float skipHoldSeconds = 1.0f;
    }

    /// <summary>
    /// Tutorial data. Loaded from Resources/Tutorial/TutorialConfig; placement is keyed by stage id and lives outside the
    /// stage scenes, so a stage rebuild never removes it.
    /// </summary>
    [CreateAssetMenu(menuName = "MyGame/Tutorial Config", fileName = "TutorialConfig")]
    public sealed class TutorialConfig : ScriptableObject
    {
        public const string ResourcePath = "Tutorial/TutorialConfig";

        public TutorialViewSettings view = new TutorialViewSettings();
        public TutorialExplainSettings explain = new TutorialExplainSettings();

        [Header("Stage pacing")]
        [Tooltip("An unfinished practice prompt can be replaced by the next startable step only after it was visible this long.")]
        [Min(0f)] public float minVisibleSeconds = 1.0f;
        [Tooltip("No new step starts for this long after one completes, so explanations never chain instantly.")]
        [Min(0f)] public float minGapSeconds = 1.5f;
        [Tooltip("The practice prompt shows the success line this long after the real action.")]
        [Min(0f)] public float successHoldSeconds = 1.2f;
        public string successKey = "tutorial.success";
        [Tooltip("Enemy steps wait while the practice enemy is closer than this (metres), so the explanation never opens on top of it.")]
        [Min(0f)] public float minEnemyDistance = 3f;
        [Tooltip("A dodge with no perfect dodge within this many seconds counts as a failed try (PerfectDodge step).")]
        [Min(0.1f)] public float dodgeFailWindow = 0.6f;
        [Tooltip("Dodges count as tries only within this distance of the practice enemy.")]
        [Min(0.5f)] public float dodgeTryRadius = 5f;
        [Tooltip("Boss lessons open only while the boss still walks for at least this long before its next attack.")]
        [Min(0f)] public float bossCalmSeconds = 0.6f;
        [Tooltip("...and while the player is at least this far from the boss body (metres).")]
        [Min(0f)] public float bossMinGap = 1.0f;
        [Tooltip("BossIntro lessons open this long into the entrance roar (the HP bar is up and the camera has reached the arena).")]
        [Min(0f)] public float bossIntroDelay = 0.5f;
        public StageTutorialDef[] stages = new StageTutorialDef[0];

        [Header("World map")]
        public string mapMoveId = "map_move";
        public string mapMoveKey = "tutorial.map_move";
        public TutorialExplainPage mapMoveExplain = new TutorialExplainPage();
        public string mapEnterId = "map_enter";
        public string mapEnterKey = "tutorial.map_enter";
        public TutorialExplainPage mapEnterExplain = new TutorialExplainPage();
        [Tooltip("Delay after the map becomes idle before an explanation appears.")]
        [Min(0f)] public float mapIdleDelay = 0.8f;

        private static TutorialConfig cached;

        public static TutorialConfig Load()
        {
            if (cached != null)
                return cached;
            cached = Resources.Load<TutorialConfig>(ResourcePath);
            if (cached == null)
            {
                Debug.LogWarning($"[Tutorial] Resources/{ResourcePath} missing; using built-in defaults.");
                cached = CreateInstance<TutorialConfig>();
                cached.ApplyDefaults();
            }
            return cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => cached = null;

        public StageTutorialDef FindStage(string stageId)
        {
            if (stages == null || string.IsNullOrEmpty(stageId))
                return null;
            foreach (var s in stages)
                if (s != null && s.stageId == stageId)
                    return s;
            return null;
        }

        private static TutorialExplainPage Page(string name, TutorialSpotKind spot, float radius, params TutorialGlyph[] glyphs)
            => new TutorialExplainPage { titleKey = $"tutorial.ex.{name}.t", bodyKey = $"tutorial.ex.{name}.b", spot = spot, radius = radius, glyphs = glyphs };

        /// <summary>
        /// Default W1_S1 course (Data/StageLayouts/W1_S1.json): spawn x2, coins 5-15, steps 20-29, leaf ledge 30-33,
        /// CP1 36, stomp enemy C_en1 39.5-42, gaps 44 / 52, CP2 65, floating platforms 68-89, CP3 91, dodge enemy E_en2 95-101, goal 116.
        /// </summary>
        public void ApplyDefaults()
        {
            view = new TutorialViewSettings();
            explain = new TutorialExplainSettings();
            minVisibleSeconds = 1.0f;
            minGapSeconds = 1.5f;
            successHoldSeconds = 1.2f;
            successKey = "tutorial.success";
            minEnemyDistance = 3f;
            dodgeFailWindow = 0.6f;
            dodgeTryRadius = 5f;
            bossCalmSeconds = 0.6f;
            bossMinGap = 1.0f;
            bossIntroDelay = 0.5f;
            mapMoveId = "map_move"; mapMoveKey = "tutorial.map_move";
            mapEnterId = "map_enter"; mapEnterKey = "tutorial.map_enter";
            mapEnterExplain = Page("map_enter", TutorialSpotKind.WorldPoint, 1.2f, TutorialGlyph.Confirm);
            mapMoveExplain = Page("map_move", TutorialSpotKind.WorldPoint, 1.2f, TutorialGlyph.MapStep);
            mapIdleDelay = 0.8f;

            var jumpPage = Page("jump", TutorialSpotKind.WorldPoint, 2.6f, TutorialGlyph.Jump);
            jumpPage.point = new Vector2(21.5f, 1.3f);
            var leafWorld = Page("leaf", TutorialSpotKind.Special, 1.5f);
            leafWorld.spotSlot = 0;
            var leafHud = Page("leaf_hud", TutorialSpotKind.HudElement, 18f);
            leafHud.spotId = "SpecialSlots";
            var checkpoint = Page("checkpoint", TutorialSpotKind.Checkpoint, 1.9f);
            checkpoint.spotId = "W1_S1_CP2"; checkpoint.point = new Vector2(0f, 1.5f);
            var stomp = Page("stomp", TutorialSpotKind.Enemy, 1.5f, TutorialGlyph.Jump);
            stomp.spotId = "W1_S1_C_en1"; stomp.point = new Vector2(0f, 0.6f);
            var dodgeEnemy = Page("dodge", TutorialSpotKind.Enemy, 1.5f, TutorialGlyph.Dodge);
            dodgeEnemy.spotId = "W1_S1_E_en2"; dodgeEnemy.point = new Vector2(0f, 0.6f);

            var bossIntro = Page("boss", TutorialSpotKind.Boss, 2.7f);
            var bossHp = Page("boss_hp", TutorialSpotKind.HudElement, 14f);
            bossHp.spotId = "BossHud/Panel";
            var bossCharge = Page("boss_charge", TutorialSpotKind.Boss, 2.7f, TutorialGlyph.Jump, TutorialGlyph.Dodge);
            var bossStun = Page("boss_stun", TutorialSpotKind.Boss, 2.7f, TutorialGlyph.Jump);
            var bossRain = Page("boss_rain", TutorialSpotKind.Boss, 2.7f);

            stages = new[]
            {
                new StageTutorialDef
                {
                    stageId = "W1_S1",
                    steps = new[]
                    {
                        new TutorialStepDef { id = "w1s1_move", textKey = "tutorial.move", glyphs = new[] { TutorialGlyph.Move },
                            explain = new[] { Page("move", TutorialSpotKind.Player, 1.7f, TutorialGlyph.Move) },
                            start = TutorialStart.StageStart, goal = TutorialGoal.MoveDistance, goalValue = 3f, endX = 12f },
                        new TutorialStepDef { id = "w1s1_run", textKey = "tutorial.run", subKey = "tutorial.run_sub",
                            glyphs = new[] { TutorialGlyph.Run },
                            explain = new[] { Page("run", TutorialSpotKind.Player, 1.7f, TutorialGlyph.Run) },
                            start = TutorialStart.ReachX, startX = 0f, afterStep = "w1s1_move",
                            goal = TutorialGoal.RunFor, goalValue = 0.35f, endX = 19.5f },
                        new TutorialStepDef { id = "w1s1_jump", textKey = "tutorial.jump", subKey = "tutorial.jump_sub",
                            glyphs = new[] { TutorialGlyph.Jump }, explain = new[] { jumpPage },
                            start = TutorialStart.ReachX, startX = 14f, urgentX = 18f,
                            goal = TutorialGoal.LandInZone, zoneMinX = 20f, zoneMaxX = 29f, zoneMinFeetY = 0.9f, endX = 30f },
                        new TutorialStepDef { id = "w1s1_leaf", textKey = "tutorial.leaf", glyphs = new[] { TutorialGlyph.GoldenLeaf },
                            explain = new[] { leafWorld, leafHud },
                            start = TutorialStart.ReachX, startX = 23f, urgentX = 26f, afterStep = "w1s1_jump",
                            goal = TutorialGoal.CollectSpecial, specialSlot = 0, endX = 35f },
                        new TutorialStepDef { id = "w1s1_stomp", textKey = "tutorial.stomp", glyphs = new[] { TutorialGlyph.Jump },
                            explain = new[] { stomp },
                            start = TutorialStart.ReachX, startX = 33f, urgentX = 34.5f, targetId = "W1_S1_C_en1",
                            goal = TutorialGoal.StompEnemy, endX = 44.5f },
                        // Explained at the second flag: the first one (x36) sits 3 m before the stomp enemy, too close for two stops.
                        new TutorialStepDef { id = "w1s1_checkpoint", textKey = "tutorial.checkpoint", explain = new[] { checkpoint },
                            start = TutorialStart.CheckpointActivated, targetId = "W1_S1_CP2",
                            goal = TutorialGoal.ExplainOnly, endX = 68f },
                        new TutorialStepDef { id = "w1s1_dodge", textKey = "tutorial.dodge", subKey = "tutorial.dodge_sub",
                            glyphs = new[] { TutorialGlyph.Dodge },
                            explain = new[] { dodgeEnemy, Page("dodge_timing", TutorialSpotKind.Player, 1.7f, TutorialGlyph.Dodge) },
                            start = TutorialStart.ReachX, startX = 91.2f, targetId = "W1_S1_E_en2",
                            goal = TutorialGoal.PerfectDodge, endX = 104f,
                            helperAfterFailures = 3, helperKey = "tutorial.dodge_help" },
                    },
                },
                // W1_B boss 9001 (Sprout King): walk -> telegraph -> charge into the wall -> groggy (head stomp = damage);
                // from phase 2 (index 1) acorns also rain on shadow markers. Lessons open only at calm moments.
                new StageTutorialDef
                {
                    stageId = "W1_B",
                    steps = new[]
                    {
                        new TutorialStepDef { id = "w1b_boss", textKey = "tutorial.boss_charge", subKey = "tutorial.boss_charge_sub",
                            glyphs = new[] { TutorialGlyph.Jump, TutorialGlyph.Dodge },
                            explain = new[] { bossIntro, bossHp, bossCharge },
                            start = TutorialStart.BossIntro, goal = TutorialGoal.BossStunned },
                        new TutorialStepDef { id = "w1b_stun", textKey = "tutorial.boss_stomp", subKey = "tutorial.boss_stomp_sub",
                            glyphs = new[] { TutorialGlyph.Jump }, explain = new[] { bossStun },
                            start = TutorialStart.BossStunned, afterStep = "w1b_boss", ignoreGap = true,
                            goal = TutorialGoal.BossDamaged },
                        new TutorialStepDef { id = "w1b_rain", textKey = "tutorial.boss_rain", explain = new[] { bossRain },
                            start = TutorialStart.BossCalm, bossPhase = 1, goal = TutorialGoal.ExplainOnly },
                    },
                },
            };
        }
    }
}
