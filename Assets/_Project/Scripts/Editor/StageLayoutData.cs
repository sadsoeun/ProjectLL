using System;

namespace MyGame.EditorTools
{
    // Plain data classes for Docs/stage-layout-schema.md (v1). Parsed with JsonUtility, so everything is a
    // public field and arrays are plain arrays. Unknown JSON fields are ignored. The optional "section" field
    // on elements is accepted for grouping (the contract only defines it for blocks).

    [Serializable]
    internal sealed class LayoutPoint
    {
        public float x;
        public float y;
    }

    [Serializable]
    internal sealed class LayoutSection
    {
        public string id;
        public float startX;
        public float endX;
        public string note;
    }

    [Serializable]
    internal sealed class LayoutBlock
    {
        public string id;
        public string section;
        public string kind; // ground | platform | wall
        /// <summary>Surface values besides empty (normal): ice = slippery (v1.5); bounce = launch pad, crumble = falls away
        /// after being stood on, conveyor_r / conveyor_l = belt carrying toward +X / -X (v1.6).</summary>
        public static readonly string[] Surfaces = { "ice", "bounce", "crumble", "conveyor_r", "conveyor_l" };

        public string surface; // optional: "" = normal, else one of Surfaces
        public float x;
        public float y;
        public float w;
        public float h;
    }

    [Serializable]
    internal sealed class LayoutCheckpoint
    {
        public string id;
        public string section;
        public float x;
        public float y;
    }

    [Serializable]
    internal sealed class LayoutCoin
    {
        public string section;
        public float x;
        public float y;
    }

    [Serializable]
    internal sealed class LayoutSpecial
    {
        public string id;
        public string section;
        public int slot;
        public float x;
        public float y;
    }

    [Serializable]
    internal sealed class LayoutSpike
    {
        public string id;
        public string section;
        public float x;
        public float y;
        public float w;
    }

    [Serializable]
    internal sealed class LayoutMover
    {
        public string id;
        public string section;
        public float x;
        public float y;
        public float w;
        public string axis; // "x" | "y"
        public float distance;
        public float speed;
        public float wait;
    }

    [Serializable]
    internal sealed class LayoutEnemy
    {
        /// <summary>Monster placed when monsterId is omitted (0): the patroller, as before contract v1.2.</summary>
        public const int DefaultMonsterId = 1001;

        public string id;
        public string section;
        /// <summary>Row id in Data/Tables/MonsterTable.csv (must be Implemented). 0 / omitted = DefaultMonsterId.</summary>
        public int monsterId;
        public float x;
        public float y;
        public float range;
        /// <summary>Optional patrol speed override for this instance; 0 / omitted = the table's moveSpeed.</summary>
        public float speed;

        public int ResolvedMonsterId => monsterId != 0 ? monsterId : DefaultMonsterId;
    }

    [Serializable]
    internal sealed class LayoutFaller
    {
        public string id;
        public string section;
        public float x;
        public float y;
        public float triggerHalfWidth;
        public float delay;
        public float respawn;
    }

    /// <summary>Timed spike trap (contract v1.4): warn -> up (kills) -> down, repeating every `period` seconds.</summary>
    [Serializable]
    internal sealed class LayoutSpikeTrap
    {
        public const float DefaultPeriod = 3f;
        public const float DefaultUpTime = 1f;
        public const float DefaultWarn = 0.5f;

        public string id;
        public string section;
        public float x;     // left edge
        public float y;     // surface the trap sits on
        public float w;     // width in metres (the trap is built from 1.5 m units)
        public float period = DefaultPeriod;
        public float upTime = DefaultUpTime;
        public float warn = DefaultWarn;
        public float offset;
    }

    /// <summary>Pressure plate (contract v1.4). Stepping on it raises `channel`; gates with the same channel open.</summary>
    [Serializable]
    internal sealed class LayoutSwitch
    {
        public string id;
        public string section;
        public float x;     // centre
        public float y;     // surface
        public int channel;
        public bool once = true;
    }

    /// <summary>Sliding gate (contract v1.4): a 1 m wide solid wall that sinks into the ground when its channel is raised.</summary>
    [Serializable]
    internal sealed class LayoutGate
    {
        public const float DefaultHeight = 3f;
        public const float DefaultOpenSeconds = 0.8f;
        public const float Width = 1f;

        public string id;
        public string section;
        public float x;     // centre
        public float y;     // surface
        public float h = DefaultHeight;
        public int channel;
        public float openSeconds = DefaultOpenSeconds;
    }

    /// <summary>Boss of a boss stage (contract v1.4). bossId 0 = no boss. Data: Data/Bosses/Boss_{bossId}.asset (BossData).</summary>
    [Serializable]
    internal sealed class LayoutBoss
    {
        public int bossId;
        public float x;     // centre of the boss on the arena floor
        public float y;     // arena floor surface
        public float arenaMinX;
        public float arenaMaxX;

        public bool IsSet => bossId != 0;
    }

    /// <summary>Art prop placed in the scene by the builder (contract v1.3). Not a gameplay element: it has no section.</summary>
    [Serializable]
    internal sealed class LayoutDecoration
    {
        /// <summary>z value meaning "not given": the prop table's defaultZ is used. Not written to JSON.</summary>
        public const float ZUnset = -9999f;

        public string id;
        /// <summary>propId in Data/Tables/PropTable.csv (must be an Implemented row).</summary>
        public string prop;
        public float x;
        public float y;
        public float z = ZUnset;
        /// <summary>Rotation around the Y axis in degrees.</summary>
        public float rotY;
        /// <summary>Uniform scale; must be > 0.</summary>
        public float scale = 1f;

        public bool HasZ => z != ZUnset;

        public float ResolveZ(float defaultZ) => HasZ ? z : defaultZ;
    }

    [Serializable]
    internal sealed class StageLayoutData
    {
        public int version;
        public string stageId;
        public float length;
        public float killY;
        public float cameraMinY;
        public LayoutPoint spawn;
        public LayoutPoint goal;
        public LayoutSection[] sections;
        public LayoutBlock[] blocks;
        public LayoutCheckpoint[] checkpoints;
        public LayoutCoin[] coins;
        public LayoutSpecial[] specials;
        public LayoutSpike[] spikes;
        public LayoutMover[] movers;
        public LayoutEnemy[] enemies;
        public LayoutFaller[] fallers;
        /// <summary>Optional (v1.3): name of a StageThemeKit asset (Data/Themes/{theme}.asset). Empty = primitive visuals.</summary>
        public string theme;
        /// <summary>Optional (v1.3): art props. Missing = none.</summary>
        public LayoutDecoration[] decorations;
        /// <summary>Optional: monster ids shown on the world-map stage card (1-2, unique across stages; each must be placed
        /// in the stage). Missing = every monster of the stage. Read by WorldMapEncounterBaker.</summary>
        public int[] featuredMonsters;
        /// <summary>Optional (v1.4): timed spike traps. Missing = none.</summary>
        public LayoutSpikeTrap[] spikeTraps;
        /// <summary>Optional (v1.4): pressure plates. Missing = none.</summary>
        public LayoutSwitch[] switches;
        /// <summary>Optional (v1.4): sliding gates. Missing = none.</summary>
        public LayoutGate[] gates;
        /// <summary>Optional (v1.4): boss. Missing or bossId 0 = no boss.</summary>
        public LayoutBoss boss;

        public bool HasBoss => boss != null && boss.IsSet;
    }
}
