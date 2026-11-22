using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame.EditorTools
{
    /// <summary>
    /// World-space (X/Y plane) geometry of layout elements: bounds, picking, handles and surface lookup. Display sizes
    /// match the prefabs made by StageLayoutBuilder (checkpoint trigger 1.5 x 4, faller 1.4, goal 1.5 x 3, mover
    /// thickness 0.5, spike height 0.5, coin radius 0.5, special cube 0.8). Pure static; only Vector2 / Rect are used.
    /// </summary>
    internal static class LayoutGeometry
    {
        public const float SurfaceMaxDrop = 1.0f;       // how far below the cursor a surface is still found (7.4)
        public const float SurfaceRiseAllowance = 0.25f; // ... and how far above it
        public const float SurfaceXTolerance = 0.05f;    // same tolerance as the validator's Supported()

        public const float CoinRadius = 0.5f;
        public const float SpecialSize = 0.8f;
        public const float CheckpointHeight = 3f;
        public const float CheckpointPickHalfWidth = 0.4f;
        public const float FallerSize = 1.4f;
        public const float GoalWidth = 1.5f;
        public const float GoalHeight = 3f;
        public const float MoverThickness = 0.5f;
        public const float SpikeHeight = 0.5f;
        public const float SpikeUnit = 0.5f;
        public const float SpikeTrapHeight = 0.6f;
        public const float SwitchWidth = 1f;
        public const float SwitchHeight = 0.3f;
        public const float GateWidth = 1f;
        public const float BossWidth = 3.5f;
        public const float BossHeight = 3.5f;
        public const float EnemySize = 1f;
        public const float SpawnWidth = 0.8f;
        public const float SpawnHeight = 2f;
        public const float SectionLabelHeight = 1.2f;   // pickable strip at the top of a section band
        public const float DecorationMarkerHalfWidth = 0.4f; // pick marker of a decoration (the art extent is drawn separately)
        public const float DecorationMarkerHeight = 0.8f;

        public struct HandleInfo
        {
            public LayoutHandle Kind;
            public Vector2 Position;

            public HandleInfo(LayoutHandle kind, Vector2 position)
            {
                Kind = kind;
                Position = position;
            }
        }

        private static Rect FromMinMax(float xMin, float yMin, float xMax, float yMax) => new Rect(xMin, yMin, xMax - xMin, yMax - yMin);

        private static Rect Centered(float cx, float cy, float w, float h) => new Rect(cx - w * 0.5f, cy - h * 0.5f, w, h);

        // ------------------------------------------------------------------ stage extents

        /// <summary>Highest y reached by blocks, spikes, movers and fallers (at least cameraMinY).</summary>
        public static float HighestPoint(StageLayoutData d)
        {
            float top = d.cameraMinY;
            foreach (var b in d.blocks ?? Array.Empty<LayoutBlock>()) top = Mathf.Max(top, b.y);
            foreach (var s in d.spikes ?? Array.Empty<LayoutSpike>()) top = Mathf.Max(top, s.y + SpikeHeight);
            foreach (var m in d.movers ?? Array.Empty<LayoutMover>()) top = Mathf.Max(top, m.y + (m.axis == "y" ? Mathf.Max(0f, m.distance) : 0f));
            foreach (var f in d.fallers ?? Array.Empty<LayoutFaller>()) top = Mathf.Max(top, f.y + FallerSize);
            foreach (var t in d.spikeTraps ?? Array.Empty<LayoutSpikeTrap>()) top = Mathf.Max(top, t.y + SpikeTrapHeight);
            foreach (var g in d.gates ?? Array.Empty<LayoutGate>()) top = Mathf.Max(top, g.y + g.h);
            if (d.HasBoss) top = Mathf.Max(top, d.boss.y + BossHeight);
            return top;
        }

        /// <summary>Top of the section bands (their ID labels sit just below it).</summary>
        public static float SectionBandTop(StageLayoutData d) => HighestPoint(d) + 6f;

        /// <summary>Whole stage: x 0..length, y killY..just above the highest element. Used by "2D view".</summary>
        public static Rect StageBounds(StageLayoutData d)
        {
            float yMax = HighestPoint(d) + 6f;
            float yMin = Mathf.Min(d.killY, yMax - 1f);
            return FromMinMax(0f, yMin, Mathf.Max(d.length, 1f), yMax);
        }

        // ------------------------------------------------------------------ bounds

        /// <summary>World bounds (xMin..xMax, yMin..yMax) of an element as drawn / picked. Sections return their label strip.</summary>
        public static Rect GetBounds(StageLayoutData d, LayoutElementRef r)
        {
            Assert.IsTrue(r.IsValid(d), "GetBounds: invalid reference " + r);
            switch (r.kind)
            {
                case LayoutElementKind.Spawn:
                    return FromMinMax(d.spawn.x - SpawnWidth * 0.5f, d.spawn.y, d.spawn.x + SpawnWidth * 0.5f, d.spawn.y + SpawnHeight);
                case LayoutElementKind.Goal:
                    return FromMinMax(d.goal.x - GoalWidth * 0.5f, d.goal.y, d.goal.x + GoalWidth * 0.5f, d.goal.y + GoalHeight);
                case LayoutElementKind.Section:
                {
                    var s = d.sections[r.index];
                    float top = SectionBandTop(d);
                    return FromMinMax(s.startX, top - SectionLabelHeight, s.endX, top);
                }
                case LayoutElementKind.Block:
                {
                    var b = d.blocks[r.index];
                    return FromMinMax(b.x, b.y - b.h, b.x + b.w, b.y);
                }
                case LayoutElementKind.Checkpoint:
                {
                    var c = d.checkpoints[r.index];
                    return FromMinMax(c.x - CheckpointPickHalfWidth, c.y, c.x + CheckpointPickHalfWidth, c.y + CheckpointHeight);
                }
                case LayoutElementKind.Coin:
                {
                    var c = d.coins[r.index];
                    return Centered(c.x, c.y, CoinRadius * 2f, CoinRadius * 2f);
                }
                case LayoutElementKind.Special:
                {
                    var s = d.specials[r.index];
                    return Centered(s.x, s.y, SpecialSize, SpecialSize);
                }
                case LayoutElementKind.Spike:
                {
                    var s = d.spikes[r.index];
                    return FromMinMax(s.x, s.y, s.x + s.w, s.y + SpikeHeight);
                }
                case LayoutElementKind.Mover:
                {
                    var m = d.movers[r.index];
                    return FromMinMax(m.x, m.y - MoverThickness, m.x + m.w, m.y);
                }
                case LayoutElementKind.Enemy:
                {
                    var e = d.enemies[r.index];
                    return FromMinMax(e.x - EnemySize * 0.5f, e.y, e.x + EnemySize * 0.5f, e.y + EnemySize);
                }
                case LayoutElementKind.Decoration:
                {
                    var dec = d.decorations[r.index];
                    return DecorationMarker(dec.x, dec.y);
                }
                case LayoutElementKind.SpikeTrap:
                {
                    var t = d.spikeTraps[r.index];
                    return FromMinMax(t.x, t.y, t.x + t.w, t.y + SpikeTrapHeight);
                }
                case LayoutElementKind.Switch:
                {
                    var s = d.switches[r.index];
                    return FromMinMax(s.x - SwitchWidth * 0.5f, s.y, s.x + SwitchWidth * 0.5f, s.y + SwitchHeight);
                }
                case LayoutElementKind.Gate:
                {
                    var g = d.gates[r.index];
                    return FromMinMax(g.x - GateWidth * 0.5f, g.y, g.x + GateWidth * 0.5f, g.y + g.h);
                }
                case LayoutElementKind.Boss:
                    return FromMinMax(d.boss.x - BossWidth * 0.5f, d.boss.y, d.boss.x + BossWidth * 0.5f, d.boss.y + BossHeight);
                default:
                {
                    var f = d.fallers[r.index];
                    return Centered(f.x, f.y, FallerSize, FallerSize);
                }
            }
        }

        /// <summary>Small box above the pivot of a decoration: what is picked and outlined (the prop's own extent can be huge).</summary>
        public static Rect DecorationMarker(float x, float y) =>
            FromMinMax(x - DecorationMarkerHalfWidth, y, x + DecorationMarkerHalfWidth, y + DecorationMarkerHeight);

        /// <summary>Bounds of what a palette tool would place at <paramref name="p"/> (placement preview).</summary>
        public static Rect DefaultBounds(MapPaletteTool tool, Vector2 p)
        {
            switch (tool)
            {
                case MapPaletteTool.Ground: return FromMinMax(p.x, p.y - 6f, p.x + 10f, p.y);
                case MapPaletteTool.Platform: return FromMinMax(p.x, p.y - 1f, p.x + 3f, p.y);
                case MapPaletteTool.Wall: return FromMinMax(p.x, p.y - 2f, p.x + 1f, p.y);
                case MapPaletteTool.Checkpoint: return FromMinMax(p.x - CheckpointPickHalfWidth, p.y, p.x + CheckpointPickHalfWidth, p.y + CheckpointHeight);
                case MapPaletteTool.Coin: return Centered(p.x, p.y, CoinRadius * 2f, CoinRadius * 2f);
                case MapPaletteTool.Special: return Centered(p.x, p.y, SpecialSize, SpecialSize);
                case MapPaletteTool.Spike: return FromMinMax(p.x, p.y, p.x + 2f, p.y + SpikeHeight);
                case MapPaletteTool.Mover: return FromMinMax(p.x, p.y - MoverThickness, p.x + 3f, p.y);
                case MapPaletteTool.Enemy: return FromMinMax(p.x - EnemySize * 0.5f, p.y, p.x + EnemySize * 0.5f, p.y + EnemySize);
                case MapPaletteTool.Faller: return Centered(p.x, p.y, FallerSize, FallerSize);
                case MapPaletteTool.Section: return FromMinMax(p.x, p.y, p.x + 10f, p.y + SectionLabelHeight);
                case MapPaletteTool.Decoration: return DecorationMarker(p.x, p.y);
                case MapPaletteTool.SpikeTrap: return FromMinMax(p.x, p.y, p.x + 3f, p.y + SpikeTrapHeight);
                case MapPaletteTool.Switch: return FromMinMax(p.x - SwitchWidth * 0.5f, p.y, p.x + SwitchWidth * 0.5f, p.y + SwitchHeight);
                case MapPaletteTool.Gate: return FromMinMax(p.x - GateWidth * 0.5f, p.y, p.x + GateWidth * 0.5f, p.y + LayoutGate.DefaultHeight);
                case MapPaletteTool.Boss: return FromMinMax(p.x - BossWidth * 0.5f, p.y, p.x + BossWidth * 0.5f, p.y + BossHeight);
                default: return new Rect(p.x, p.y, 0f, 0f);
            }
        }

        // ------------------------------------------------------------------ surfaces

        /// <summary>
        /// Highest block top at <paramref name="x"/> (+-0.05) that lies at most <paramref name="maxDrop"/> below
        /// <paramref name="y"/> or 0.25 above it. Blocks only: the validator accepts nothing else as a surface.
        /// </summary>
        public static bool TrySurfaceBelow(StageLayoutData d, float x, float y, float maxDrop, out float top)
        {
            top = 0f;
            bool found = false;
            foreach (var b in d.blocks ?? Array.Empty<LayoutBlock>())
            {
                if (x < b.x - SurfaceXTolerance || x > b.x + b.w + SurfaceXTolerance)
                    continue;
                if (b.y > y + SurfaceRiseAllowance || b.y < y - maxDrop)
                    continue;
                if (!found || b.y > top)
                {
                    top = b.y;
                    found = true;
                }
            }
            return found;
        }

        // ------------------------------------------------------------------ picking

        /// <summary>Pick priority, lower wins (design 7.3): coin/special, then enemy-like, spike/mover, block, section.</summary>
        public static int PickPriority(LayoutElementKind kind)
        {
            switch (kind)
            {
                case LayoutElementKind.Coin:
                case LayoutElementKind.Special:
                    return 1;
                case LayoutElementKind.Enemy:
                case LayoutElementKind.Faller:
                case LayoutElementKind.Checkpoint:
                case LayoutElementKind.Boss:
                case LayoutElementKind.Spawn:
                case LayoutElementKind.Goal:
                    return 2;
                case LayoutElementKind.SpikeTrap:
                case LayoutElementKind.Switch:
                case LayoutElementKind.Gate:
                case LayoutElementKind.Spike:
                case LayoutElementKind.Mover:
                    return 3;
                case LayoutElementKind.Block:
                    return 4;
                case LayoutElementKind.Section:
                    return 5;
                case LayoutElementKind.Decoration:
                    return 6; // art only: every gameplay element wins over it
                default:
                    return 9;
            }
        }

        /// <summary>The element under <paramref name="p"/> (bounds grown by <paramref name="tolerance"/>), or None.</summary>
        public static LayoutElementRef Pick(StageLayoutData d, Vector2 p, float tolerance)
        {
            var best = LayoutElementRef.None;
            int bestPriority = int.MaxValue;
            float bestArea = float.MaxValue;

            void Consider(LayoutElementKind kind, int index)
            {
                var r = new LayoutElementRef(kind, index);
                var rect = GetBounds(d, r);
                rect.xMin -= tolerance; rect.xMax += tolerance; rect.yMin -= tolerance; rect.yMax += tolerance;
                if (p.x < rect.xMin || p.x > rect.xMax || p.y < rect.yMin || p.y > rect.yMax)
                    return;
                int pr = PickPriority(kind);
                float area = rect.width * rect.height;
                if (pr < bestPriority || (pr == bestPriority && area < bestArea))
                {
                    best = r;
                    bestPriority = pr;
                    bestArea = area;
                }
            }

            Consider(LayoutElementKind.Spawn, 0);
            Consider(LayoutElementKind.Goal, 0);
            for (int i = 0; i < Len(d.sections); i++) Consider(LayoutElementKind.Section, i);
            for (int i = 0; i < Len(d.blocks); i++) Consider(LayoutElementKind.Block, i);
            for (int i = 0; i < Len(d.checkpoints); i++) Consider(LayoutElementKind.Checkpoint, i);
            for (int i = 0; i < Len(d.coins); i++) Consider(LayoutElementKind.Coin, i);
            for (int i = 0; i < Len(d.specials); i++) Consider(LayoutElementKind.Special, i);
            for (int i = 0; i < Len(d.spikes); i++) Consider(LayoutElementKind.Spike, i);
            for (int i = 0; i < Len(d.movers); i++) Consider(LayoutElementKind.Mover, i);
            for (int i = 0; i < Len(d.enemies); i++) Consider(LayoutElementKind.Enemy, i);
            for (int i = 0; i < Len(d.fallers); i++) Consider(LayoutElementKind.Faller, i);
            for (int i = 0; i < Len(d.decorations); i++) Consider(LayoutElementKind.Decoration, i);
            for (int i = 0; i < Len(d.spikeTraps); i++) Consider(LayoutElementKind.SpikeTrap, i);
            for (int i = 0; i < Len(d.switches); i++) Consider(LayoutElementKind.Switch, i);
            for (int i = 0; i < Len(d.gates); i++) Consider(LayoutElementKind.Gate, i);
            if (d.HasBoss) Consider(LayoutElementKind.Boss, 0);
            return best;
        }

        private static int Len<T>(T[] a) => a?.Length ?? 0;

        // ------------------------------------------------------------------ handles

        /// <summary>Fills <paramref name="into"/> with the handles of the element (design 7.4). Cleared first.</summary>
        public static void GetHandles(StageLayoutData d, LayoutElementRef r, List<HandleInfo> into)
        {
            into.Clear();
            if (!r.IsValid(d))
                return;
            switch (r.kind)
            {
                case LayoutElementKind.Block:
                {
                    var b = d.blocks[r.index];
                    into.Add(new HandleInfo(LayoutHandle.Left, new Vector2(b.x, b.y - b.h * 0.5f)));
                    into.Add(new HandleInfo(LayoutHandle.Right, new Vector2(b.x + b.w, b.y - b.h * 0.5f)));
                    into.Add(new HandleInfo(LayoutHandle.Top, new Vector2(b.x + b.w * 0.5f, b.y)));
                    into.Add(new HandleInfo(LayoutHandle.Bottom, new Vector2(b.x + b.w * 0.5f, b.y - b.h)));
                    break;
                }
                case LayoutElementKind.Spike:
                {
                    var s = d.spikes[r.index];
                    into.Add(new HandleInfo(LayoutHandle.Left, new Vector2(s.x, s.y + SpikeHeight * 0.5f)));
                    into.Add(new HandleInfo(LayoutHandle.Right, new Vector2(s.x + s.w, s.y + SpikeHeight * 0.5f)));
                    break;
                }
                case LayoutElementKind.Mover:
                {
                    var m = d.movers[r.index];
                    into.Add(new HandleInfo(LayoutHandle.Right, new Vector2(m.x + m.w, m.y - MoverThickness * 0.5f)));
                    into.Add(new HandleInfo(LayoutHandle.MoverEnd, MoverGhostCenter(m)));
                    break;
                }
                case LayoutElementKind.Enemy:
                {
                    var e = d.enemies[r.index];
                    into.Add(new HandleInfo(LayoutHandle.PatrolEnd, new Vector2(e.x + e.range, e.y + EnemySize * 0.5f)));
                    break;
                }
                case LayoutElementKind.Faller:
                {
                    var f = d.fallers[r.index];
                    into.Add(new HandleInfo(LayoutHandle.TriggerWidth, new Vector2(f.x + f.triggerHalfWidth, f.y)));
                    break;
                }
                case LayoutElementKind.SpikeTrap:
                {
                    var t = d.spikeTraps[r.index];
                    into.Add(new HandleInfo(LayoutHandle.Left, new Vector2(t.x, t.y + SpikeTrapHeight * 0.5f)));
                    into.Add(new HandleInfo(LayoutHandle.Right, new Vector2(t.x + t.w, t.y + SpikeTrapHeight * 0.5f)));
                    break;
                }
                case LayoutElementKind.Gate:
                {
                    var g = d.gates[r.index];
                    into.Add(new HandleInfo(LayoutHandle.Top, new Vector2(g.x, g.y + g.h)));
                    break;
                }
                case LayoutElementKind.Boss:
                {
                    into.Add(new HandleInfo(LayoutHandle.Left, new Vector2(d.boss.arenaMinX, d.boss.y + 1f)));
                    into.Add(new HandleInfo(LayoutHandle.Right, new Vector2(d.boss.arenaMaxX, d.boss.y + 1f)));
                    break;
                }
                case LayoutElementKind.Section:
                {
                    var s = d.sections[r.index];
                    float y = SectionBandTop(d) - SectionLabelHeight * 0.5f;
                    into.Add(new HandleInfo(LayoutHandle.Left, new Vector2(s.startX, y)));
                    into.Add(new HandleInfo(LayoutHandle.Right, new Vector2(s.endX, y)));
                    break;
                }
            }
        }

        /// <summary>Centre of the mover's far-end ghost (it travels <c>distance</c> along its axis).</summary>
        public static Vector2 MoverGhostCenter(LayoutMover m)
        {
            if (m.axis == "y")
                return new Vector2(m.x + m.w * 0.5f, m.y + m.distance - MoverThickness * 0.5f);
            return new Vector2(m.x + m.distance + m.w * 0.5f, m.y - MoverThickness * 0.5f);
        }

        /// <summary>Converts a world point under a handle drag into the coordinate SetEdge expects for that handle.</summary>
        public static float HandleValueFromWorld(StageLayoutData d, LayoutElementRef r, LayoutHandle handle, Vector2 world)
        {
            Assert.IsTrue(r.IsValid(d), "HandleValueFromWorld: invalid reference " + r);
            switch (handle)
            {
                case LayoutHandle.Top:
                case LayoutHandle.Bottom:
                    return world.y;
                case LayoutHandle.MoverEnd:
                {
                    var m = d.movers[r.index];
                    return m.axis == "y" ? world.y + MoverThickness * 0.5f : world.x - m.w * 0.5f;
                }
                default:
                    return world.x;
            }
        }
    }
}
