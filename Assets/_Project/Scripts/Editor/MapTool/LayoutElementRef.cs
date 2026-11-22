using System;

namespace MyGame.EditorTools
{
    /// <summary>Kinds of layout element the map tool can select (one per array / single point of StageLayoutData).</summary>
    internal enum LayoutElementKind
    {
        None = 0,
        Section,
        Block,
        Checkpoint,
        Coin,
        Special,
        Spike,
        Mover,
        Enemy,
        Faller,
        Spawn,
        Goal,
        Decoration,
        SpikeTrap,
        Switch,
        Gate,
        Boss,
    }

    /// <summary>Palette entries of the window (a block kind counts as its own tool). None = the select tool.</summary>
    internal enum MapPaletteTool
    {
        Select = 0,
        Ground,
        Platform,
        Wall,
        Checkpoint,
        Coin,
        Special,
        Spike,
        Mover,
        Enemy,
        Faller,
        Section,
        Decoration,
        SpikeTrap,
        Switch,
        Gate,
        Boss,
    }

    /// <summary>Handles that resize or re-parameterise an element (design 7.4).</summary>
    internal enum LayoutHandle
    {
        None = 0,
        Left,          // block / spike: left edge; section: startX
        Right,         // block / spike / mover: right edge; section: endX
        Top,           // block: top edge (y)
        Bottom,        // block: bottom edge (y - h)
        PatrolEnd,     // enemy: far end of the patrol (x + range)
        MoverEnd,      // mover: ghost at the far end of its travel (axis direction only)
        TriggerWidth,  // faller: x + triggerHalfWidth
        // Left / Right on a spike trap: its edges; on a boss: arenaMinX / arenaMaxX. Top on a gate: its height.
    }

    /// <summary>Value-type reference to one element: (kind, index in its array). Spawn and Goal ignore the index.</summary>
    [Serializable]
    internal struct LayoutElementRef : IEquatable<LayoutElementRef>
    {
        public LayoutElementKind kind;
        public int index;

        public static readonly LayoutElementRef None = new LayoutElementRef();

        public LayoutElementRef(LayoutElementKind kind, int index)
        {
            this.kind = kind;
            this.index = index;
        }

        public bool IsNone => kind == LayoutElementKind.None;

        /// <summary>True if the reference points at an existing element of <paramref name="d"/>.</summary>
        public bool IsValid(StageLayoutData d)
        {
            if (d == null)
                return false;
            switch (kind)
            {
                case LayoutElementKind.Spawn: return d.spawn != null;
                case LayoutElementKind.Goal: return d.goal != null;
                case LayoutElementKind.Section: return InRange(d.sections);
                case LayoutElementKind.Block: return InRange(d.blocks);
                case LayoutElementKind.Checkpoint: return InRange(d.checkpoints);
                case LayoutElementKind.Coin: return InRange(d.coins);
                case LayoutElementKind.Special: return InRange(d.specials);
                case LayoutElementKind.Spike: return InRange(d.spikes);
                case LayoutElementKind.Mover: return InRange(d.movers);
                case LayoutElementKind.Enemy: return InRange(d.enemies);
                case LayoutElementKind.Faller: return InRange(d.fallers);
                case LayoutElementKind.Decoration: return InRange(d.decorations);
                case LayoutElementKind.SpikeTrap: return InRange(d.spikeTraps);
                case LayoutElementKind.Switch: return InRange(d.switches);
                case LayoutElementKind.Gate: return InRange(d.gates);
                case LayoutElementKind.Boss: return index == 0 && d.HasBoss;
                default: return false;
            }
        }

        private bool InRange<T>(T[] a) => a != null && index >= 0 && index < a.Length;

        public bool Equals(LayoutElementRef other) => kind == other.kind && index == other.index;
        public override bool Equals(object obj) => obj is LayoutElementRef r && Equals(r);
        public override int GetHashCode() => ((int)kind * 397) ^ index;
        public static bool operator ==(LayoutElementRef a, LayoutElementRef b) => a.Equals(b);
        public static bool operator !=(LayoutElementRef a, LayoutElementRef b) => !a.Equals(b);
        public override string ToString() => IsNone ? "none" : $"{kind}[{index}]";
    }
}
