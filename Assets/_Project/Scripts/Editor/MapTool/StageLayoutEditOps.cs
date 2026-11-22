using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Pure edit operations on StageLayoutData (no Undo, no UI). The controller wraps each call in an Undo
    /// record; the self check calls them directly. Recoverable refusals (no free slot, duplicate id, ...) return
    /// false with a message; calling with an invalid reference or a forbidden kind is a programming error (Assert).
    /// </summary>
    internal static class StageLayoutEditOps
    {
        public const float MinSize = 0.1f;
        public const float MinSpikeWidth = 0.5f;
        public const float MinMoverWidth = 0.5f;
        public const float MinEnemyRange = 0.5f;
        public const float MinTriggerHalfWidth = 0.1f;
        public const float MinDistance = 0.1f;
        public const float MinSpikeTrapWidth = 1f;
        public const float MinGateHeight = 0.5f;
        public const float DefaultArenaHalfWidth = 12f;
        public const int DefaultBossId = 9001;

        // ------------------------------------------------------------------ rounding

        /// <summary>Rounds to 0.01 m (D-07).</summary>
        public static float Round01(float v) => Mathf.Round(v * 100f) / 100f;

        /// <summary>Snaps to a multiple of <paramref name="grid"/> (grid &lt;= 0 only rounds to 0.01 m).</summary>
        public static float Snap(float v, float grid) => grid > 0f ? Round01(Mathf.Round(v / grid) * grid) : Round01(v);

        // ------------------------------------------------------------------ array helpers

        private static T[] Append<T>(T[] a, T item)
        {
            var result = new T[(a?.Length ?? 0) + 1];
            if (a != null)
                Array.Copy(a, result, a.Length);
            result[result.Length - 1] = item;
            return result;
        }

        private static T[] RemoveAt<T>(T[] a, int index)
        {
            Assert.IsTrue(a != null && index >= 0 && index < a.Length, "RemoveAt: index out of range.");
            var result = new T[a.Length - 1];
            Array.Copy(a, 0, result, 0, index);
            Array.Copy(a, index + 1, result, index, a.Length - index - 1);
            return result;
        }

        private static T Clone<T>(T src) where T : class => JsonUtility.FromJson<T>(JsonUtility.ToJson(src));

        // ------------------------------------------------------------------ template

        /// <summary>The new-layout template of MT-002 (validates without errors).</summary>
        public static StageLayoutData CreateTemplate(string stageId)
        {
            Assert.IsFalse(string.IsNullOrEmpty(stageId), "CreateTemplate needs a stageId.");
            var d = new StageLayoutData
            {
                version = 1,
                stageId = stageId,
                length = 60f,
                killY = -8f,
                cameraMinY = 2.5f,
                spawn = new LayoutPoint { x = 2f, y = 0f },
                goal = new LayoutPoint { x = 56f, y = 0f },
                sections = Array.Empty<LayoutSection>(),
                blocks = new[] { new LayoutBlock { id = stageId + "_ground1", section = "", kind = "ground", x = 0f, y = 0f, w = 60f, h = 6f } },
                checkpoints = Array.Empty<LayoutCheckpoint>(),
                coins = Array.Empty<LayoutCoin>(),
                specials = new LayoutSpecial[StageSession.SpecialSlotCount],
                spikes = Array.Empty<LayoutSpike>(),
                movers = Array.Empty<LayoutMover>(),
                enemies = Array.Empty<LayoutEnemy>(),
                fallers = Array.Empty<LayoutFaller>(),
                decorations = Array.Empty<LayoutDecoration>(),
                spikeTraps = Array.Empty<LayoutSpikeTrap>(),
                switches = Array.Empty<LayoutSwitch>(),
                gates = Array.Empty<LayoutGate>(),
                boss = new LayoutBoss(),
            };
            for (int i = 0; i < d.specials.Length; i++)
            {
                d.specials[i] = new LayoutSpecial
                {
                    id = $"{stageId}_SC{i + 1}", section = "", slot = i, x = 15f * (i + 1), y = 3f,
                };
            }
            return d;
        }

        // ------------------------------------------------------------------ ids / sections

        /// <summary>Every id used in the layout (sections, blocks, checkpoints, specials, spikes, movers, enemies, fallers).</summary>
        public static HashSet<string> AllIds(StageLayoutData d)
        {
            var ids = new HashSet<string>();
            void Add(string id) { if (!string.IsNullOrEmpty(id)) ids.Add(id); }
            foreach (var s in d.sections ?? Array.Empty<LayoutSection>()) Add(s.id);
            foreach (var b in d.blocks ?? Array.Empty<LayoutBlock>()) Add(b.id);
            foreach (var c in d.checkpoints ?? Array.Empty<LayoutCheckpoint>()) Add(c.id);
            foreach (var s in d.specials ?? Array.Empty<LayoutSpecial>()) Add(s.id);
            foreach (var s in d.spikes ?? Array.Empty<LayoutSpike>()) Add(s.id);
            foreach (var m in d.movers ?? Array.Empty<LayoutMover>()) Add(m.id);
            foreach (var e in d.enemies ?? Array.Empty<LayoutEnemy>()) Add(e.id);
            foreach (var f in d.fallers ?? Array.Empty<LayoutFaller>()) Add(f.id);
            foreach (var dec in d.decorations ?? Array.Empty<LayoutDecoration>()) Add(dec.id);
            foreach (var t in d.spikeTraps ?? Array.Empty<LayoutSpikeTrap>()) Add(t.id);
            foreach (var s in d.switches ?? Array.Empty<LayoutSwitch>()) Add(s.id);
            foreach (var g in d.gates ?? Array.Empty<LayoutGate>()) Add(g.id);
            return ids;
        }

        /// <summary>Id of the section with startX &lt;= x &lt; endX, or null.</summary>
        public static string SectionAt(StageLayoutData d, float x)
        {
            if (d.sections == null)
                return null;
            foreach (var s in d.sections)
            {
                if (x >= s.startX && x < s.endX)
                    return s.id;
            }
            return null;
        }

        private static string IdToken(LayoutElementKind kind, string blockKind, int monsterId)
        {
            switch (kind)
            {
                case LayoutElementKind.Block: return string.IsNullOrEmpty(blockKind) ? "ground" : blockKind;
                case LayoutElementKind.Checkpoint: return "CP";
                case LayoutElementKind.Special: return "SC";
                case LayoutElementKind.Spike: return "spk";
                case LayoutElementKind.Mover: return "mv";
                case LayoutElementKind.Enemy: return monsterId == 1002 ? "ch" : "en";
                case LayoutElementKind.Faller: return "fall";
                case LayoutElementKind.Decoration: return "dec";
                case LayoutElementKind.SpikeTrap: return "trap";
                case LayoutElementKind.Switch: return "sw";
                case LayoutElementKind.Gate: return "gate";
                default:
                    Assert.IsTrue(false, "NextId: kind has no id token: " + kind);
                    return "x";
            }
        }

        /// <summary>
        /// Next free id per design 7.6: {section-or-stageId}_{token}{n} with the smallest unused n. Checkpoints and
        /// specials always use the stageId as prefix.
        /// </summary>
        public static string NextId(StageLayoutData d, LayoutElementKind kind, string blockKind, float x, int monsterId = 0)
        {
            string stage = string.IsNullOrEmpty(d.stageId) ? "stage" : d.stageId;
            string prefix = stage;
            if (kind != LayoutElementKind.Checkpoint && kind != LayoutElementKind.Special && kind != LayoutElementKind.Decoration)
                prefix = SectionAt(d, x) ?? stage;
            string token = IdToken(kind, blockKind, monsterId);
            var ids = AllIds(d);
            for (int n = 1; ; n++)
            {
                string id = $"{prefix}_{token}{n}";
                if (!ids.Contains(id))
                    return id;
            }
        }

        /// <summary>{stageId}_{letter}: the letter after the highest one in use (A when none); falls back to the first free letter.</summary>
        public static string NextSectionId(StageLayoutData d)
        {
            string stage = string.IsNullOrEmpty(d.stageId) ? "stage" : d.stageId;
            var ids = AllIds(d);
            int highest = -1;
            for (int i = 0; i < 26; i++)
            {
                if (ids.Contains($"{stage}_{(char)('A' + i)}"))
                    highest = i;
            }
            for (int i = highest + 1; i < 26; i++)
            {
                string id = $"{stage}_{(char)('A' + i)}";
                if (!ids.Contains(id)) return id;
            }
            for (int i = 0; i < 26; i++)
            {
                string id = $"{stage}_{(char)('A' + i)}";
                if (!ids.Contains(id)) return id;
            }
            for (int n = 1; ; n++)
            {
                string id = $"{stage}_S{n}";
                if (!ids.Contains(id)) return id;
            }
        }

        private static int FreeSlot(StageLayoutData d)
        {
            var used = new bool[StageSession.SpecialSlotCount];
            foreach (var s in d.specials ?? Array.Empty<LayoutSpecial>())
            {
                if (s.slot >= 0 && s.slot < used.Length)
                    used[s.slot] = true;
            }
            for (int i = 0; i < used.Length; i++)
            {
                if (!used[i])
                    return i;
            }
            return -1;
        }

        // ------------------------------------------------------------------ placement snapping

        /// <summary>True for elements that stand on a block surface (spawn, goal, checkpoint, spike, enemy).</summary>
        public static bool IsStanding(LayoutElementKind kind) =>
            kind == LayoutElementKind.Spawn || kind == LayoutElementKind.Goal || kind == LayoutElementKind.Checkpoint
            || kind == LayoutElementKind.Spike || kind == LayoutElementKind.Enemy
            || kind == LayoutElementKind.SpikeTrap || kind == LayoutElementKind.Switch || kind == LayoutElementKind.Gate || kind == LayoutElementKind.Boss;

        /// <summary>
        /// Final position for a raw point: grid (or 0.01 m) rounding, then for standing kinds the y of the block surface
        /// below the point (within 1 m). <paramref name="probeDx"/> shifts the surface probe (spikes probe their centre).
        /// </summary>
        public static Vector2 SnapPlacement(StageLayoutData d, LayoutElementKind kind, Vector2 p, float grid, bool snap,
            bool surfaceSnap, float probeDx = 0f)
        {
            float x = snap ? Snap(p.x, grid) : Round01(p.x);
            float y = snap ? Snap(p.y, grid) : Round01(p.y);
            if (surfaceSnap && IsStanding(kind) && LayoutGeometry.TrySurfaceBelow(d, x + probeDx, p.y, LayoutGeometry.SurfaceMaxDrop, out float top))
                y = Round01(top);
            return new Vector2(x, y);
        }

        // ------------------------------------------------------------------ add / remove / duplicate

        /// <summary>
        /// Adds an element with the design 7.5 defaults at <paramref name="at"/> (already snapped). Returns false with a
        /// message when refused (special collectibles are limited to the slot count).
        /// </summary>
        public static bool TryAdd(StageLayoutData d, MapPaletteTool tool, Vector2 at, out LayoutElementRef created, out string message)
        {
            Assert.IsNotNull(d, "TryAdd needs data.");
            Assert.IsTrue(tool != MapPaletteTool.Select, "TryAdd: the select tool places nothing.");
            created = LayoutElementRef.None;
            message = null;
            float x = Round01(at.x);
            float y = Round01(at.y);
            string section = SectionAt(d, x) ?? "";

            switch (tool)
            {
                case MapPaletteTool.Ground:
                case MapPaletteTool.Platform:
                case MapPaletteTool.Wall:
                {
                    string kindName = tool == MapPaletteTool.Ground ? "ground" : tool == MapPaletteTool.Platform ? "platform" : "wall";
                    float w = tool == MapPaletteTool.Ground ? 10f : tool == MapPaletteTool.Platform ? 3f : 1f;
                    float h = tool == MapPaletteTool.Ground ? 6f : tool == MapPaletteTool.Platform ? 1f : 2f;
                    d.blocks = Append(d.blocks, new LayoutBlock
                    {
                        id = NextId(d, LayoutElementKind.Block, kindName, x), section = section, kind = kindName, x = x, y = y, w = w, h = h,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Block, d.blocks.Length - 1);
                    break;
                }
                case MapPaletteTool.Checkpoint:
                    d.checkpoints = Append(d.checkpoints, new LayoutCheckpoint
                    {
                        id = NextId(d, LayoutElementKind.Checkpoint, null, x), section = "", x = x, y = y,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Checkpoint, d.checkpoints.Length - 1);
                    break;
                case MapPaletteTool.Coin:
                    d.coins = Append(d.coins, new LayoutCoin { section = "", x = x, y = y });
                    created = new LayoutElementRef(LayoutElementKind.Coin, d.coins.Length - 1);
                    break;
                case MapPaletteTool.Special:
                {
                    int slot = FreeSlot(d);
                    if (slot < 0)
                    {
                        message = $"특수 수집품은 최대 {StageSession.SpecialSlotCount}개입니다 (빈 slot 없음).";
                        return false;
                    }
                    d.specials = Append(d.specials, new LayoutSpecial
                    {
                        id = NextId(d, LayoutElementKind.Special, null, x), section = "", slot = slot, x = x, y = y,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Special, d.specials.Length - 1);
                    break;
                }
                case MapPaletteTool.Spike:
                    d.spikes = Append(d.spikes, new LayoutSpike
                    {
                        id = NextId(d, LayoutElementKind.Spike, null, x), section = "", x = x, y = y, w = 2f,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Spike, d.spikes.Length - 1);
                    break;
                case MapPaletteTool.Mover:
                    d.movers = Append(d.movers, new LayoutMover
                    {
                        id = NextId(d, LayoutElementKind.Mover, null, x), section = "", x = x, y = y, w = 3f, axis = "x",
                        distance = 4f, speed = 2f, wait = 0.5f,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Mover, d.movers.Length - 1);
                    break;
                case MapPaletteTool.Enemy:
                    d.enemies = Append(d.enemies, new LayoutEnemy
                    {
                        id = NextId(d, LayoutElementKind.Enemy, null, x, 0), section = "", monsterId = 0, x = x, y = y, range = 6f, speed = 0f,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Enemy, d.enemies.Length - 1);
                    break;
                case MapPaletteTool.Faller:
                    d.fallers = Append(d.fallers, new LayoutFaller
                    {
                        id = NextId(d, LayoutElementKind.Faller, null, x), section = "", x = x, y = y,
                        triggerHalfWidth = 1.5f, delay = 0.4f, respawn = 3f,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Faller, d.fallers.Length - 1);
                    break;
                case MapPaletteTool.SpikeTrap:
                    d.spikeTraps = Append(d.spikeTraps, new LayoutSpikeTrap
                    {
                        id = NextId(d, LayoutElementKind.SpikeTrap, null, x), section = "", x = x, y = y, w = 3f,
                    });
                    created = new LayoutElementRef(LayoutElementKind.SpikeTrap, d.spikeTraps.Length - 1);
                    break;
                case MapPaletteTool.Switch:
                    d.switches = Append(d.switches, new LayoutSwitch
                    {
                        id = NextId(d, LayoutElementKind.Switch, null, x), section = "", x = x, y = y, channel = 1, once = true,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Switch, d.switches.Length - 1);
                    break;
                case MapPaletteTool.Gate:
                    d.gates = Append(d.gates, new LayoutGate
                    {
                        id = NextId(d, LayoutElementKind.Gate, null, x), section = "", x = x, y = y, h = LayoutGate.DefaultHeight,
                        channel = 1, openSeconds = LayoutGate.DefaultOpenSeconds,
                    });
                    created = new LayoutElementRef(LayoutElementKind.Gate, d.gates.Length - 1);
                    break;
                case MapPaletteTool.Boss:
                    if (d.HasBoss)
                    {
                        message = "보스는 스테이지당 하나입니다 (기존 보스를 선택해 옮기거나 삭제하세요).";
                        return false;
                    }
                    d.boss = new LayoutBoss
                    {
                        bossId = DefaultBossId, x = x, y = y,
                        arenaMinX = Round01(x - DefaultArenaHalfWidth), arenaMaxX = Round01(x + DefaultArenaHalfWidth),
                    };
                    created = new LayoutElementRef(LayoutElementKind.Boss, 0);
                    break;
                case MapPaletteTool.Section:
                    d.sections = Append(d.sections, new LayoutSection { id = NextSectionId(d), startX = x, endX = Round01(x + 10f), note = "" });
                    created = new LayoutElementRef(LayoutElementKind.Section, d.sections.Length - 1);
                    break;
                default:
                    Assert.IsTrue(false, "TryAdd: unhandled tool " + tool);
                    return false;
            }
            Assert.IsTrue(created.IsValid(d), "TryAdd must return a valid reference.");
            return true;
        }

        /// <summary>
        /// Adds a decoration of prop <paramref name="propId"/> at <paramref name="at"/> (already snapped) with the given depth,
        /// rotY 0 and scale 1. Its id is {stageId}_dec{n}. Callers pass a propId they have checked against the prop table.
        /// </summary>
        public static bool TryAddDecoration(StageLayoutData d, string propId, float z, Vector2 at, out LayoutElementRef created)
        {
            Assert.IsNotNull(d, "TryAddDecoration needs data.");
            Assert.IsFalse(string.IsNullOrEmpty(propId), "TryAddDecoration needs a propId.");
            created = LayoutElementRef.None;
            if (string.IsNullOrEmpty(propId) || float.IsNaN(z) || float.IsInfinity(z))
                return false;
            float x = Round01(at.x);
            d.decorations = Append(d.decorations, new LayoutDecoration
            {
                id = NextId(d, LayoutElementKind.Decoration, null, x), prop = propId, x = x, y = Round01(at.y), z = z, rotY = 0f, scale = 1f,
            });
            created = new LayoutElementRef(LayoutElementKind.Decoration, d.decorations.Length - 1);
            Assert.IsTrue(created.IsValid(d), "TryAddDecoration must return a valid reference.");
            return true;
        }

        /// <summary>Removes an element (not spawn / goal). Sections are removed without touching references (see RemoveSection).</summary>
        public static void Remove(StageLayoutData d, LayoutElementRef r)
        {
            Assert.IsTrue(r.IsValid(d), "Remove: invalid reference " + r);
            Assert.IsTrue(r.kind != LayoutElementKind.Spawn && r.kind != LayoutElementKind.Goal, "Remove: spawn and goal cannot be removed.");
            switch (r.kind)
            {
                case LayoutElementKind.Section: d.sections = RemoveAt(d.sections, r.index); break;
                case LayoutElementKind.Block: d.blocks = RemoveAt(d.blocks, r.index); break;
                case LayoutElementKind.Checkpoint: d.checkpoints = RemoveAt(d.checkpoints, r.index); break;
                case LayoutElementKind.Coin: d.coins = RemoveAt(d.coins, r.index); break;
                case LayoutElementKind.Special: d.specials = RemoveAt(d.specials, r.index); break;
                case LayoutElementKind.Spike: d.spikes = RemoveAt(d.spikes, r.index); break;
                case LayoutElementKind.Mover: d.movers = RemoveAt(d.movers, r.index); break;
                case LayoutElementKind.Enemy: d.enemies = RemoveAt(d.enemies, r.index); break;
                case LayoutElementKind.Faller: d.fallers = RemoveAt(d.fallers, r.index); break;
                case LayoutElementKind.Decoration: d.decorations = RemoveAt(d.decorations, r.index); break;
                case LayoutElementKind.SpikeTrap: d.spikeTraps = RemoveAt(d.spikeTraps, r.index); break;
                case LayoutElementKind.Switch: d.switches = RemoveAt(d.switches, r.index); break;
                case LayoutElementKind.Gate: d.gates = RemoveAt(d.gates, r.index); break;
                case LayoutElementKind.Boss: d.boss = new LayoutBoss(); break;
            }
        }

        /// <summary>Removes a section; with <paramref name="clearRefs"/> the elements that reference it get an empty section.</summary>
        public static void RemoveSection(StageLayoutData d, int index, bool clearRefs)
        {
            Assert.IsTrue(new LayoutElementRef(LayoutElementKind.Section, index).IsValid(d), "RemoveSection: invalid index.");
            string id = d.sections[index].id;
            d.sections = RemoveAt(d.sections, index);
            if (clearRefs)
                Retarget(d, id, "");
        }

        /// <summary>Number of elements whose section field equals <paramref name="sectionId"/>.</summary>
        public static int CountSectionRefs(StageLayoutData d, string sectionId)
        {
            int n = 0;
            ForEachSectionField(d, (get, set) => { if (get() == sectionId) n++; });
            return n;
        }

        private static void Retarget(StageLayoutData d, string oldId, string newId)
        {
            ForEachSectionField(d, (get, set) => { if (get() == oldId) set(newId); });
        }

        private static void ForEachSectionField(StageLayoutData d, Action<Func<string>, Action<string>> visit)
        {
            foreach (var e in d.blocks ?? Array.Empty<LayoutBlock>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.checkpoints ?? Array.Empty<LayoutCheckpoint>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.coins ?? Array.Empty<LayoutCoin>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.specials ?? Array.Empty<LayoutSpecial>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.spikes ?? Array.Empty<LayoutSpike>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.movers ?? Array.Empty<LayoutMover>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.enemies ?? Array.Empty<LayoutEnemy>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.fallers ?? Array.Empty<LayoutFaller>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.spikeTraps ?? Array.Empty<LayoutSpikeTrap>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.switches ?? Array.Empty<LayoutSwitch>()) { var o = e; visit(() => o.section, v => o.section = v); }
            foreach (var e in d.gates ?? Array.Empty<LayoutGate>()) { var o = e; visit(() => o.section, v => o.section = v); }
        }

        /// <summary>Copies an element one grid step to the right with a fresh id. Refuses spawn, goal and a 4th special.</summary>
        public static bool Duplicate(StageLayoutData d, LayoutElementRef r, float dx, out LayoutElementRef created, out string message)
        {
            Assert.IsTrue(r.IsValid(d), "Duplicate: invalid reference " + r);
            created = LayoutElementRef.None;
            message = null;
            switch (r.kind)
            {
                case LayoutElementKind.Spawn:
                case LayoutElementKind.Goal:
                    message = "spawn과 goal은 복제할 수 없습니다.";
                    return false;
                case LayoutElementKind.Section:
                {
                    var c = Clone(d.sections[r.index]);
                    c.id = NextSectionId(d);
                    c.startX = Round01(c.startX + dx);
                    c.endX = Round01(c.endX + dx);
                    d.sections = Append(d.sections, c);
                    break;
                }
                case LayoutElementKind.Block:
                {
                    var c = Clone(d.blocks[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Block, c.kind, c.x);
                    d.blocks = Append(d.blocks, c);
                    break;
                }
                case LayoutElementKind.Checkpoint:
                {
                    var c = Clone(d.checkpoints[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Checkpoint, null, c.x);
                    d.checkpoints = Append(d.checkpoints, c);
                    break;
                }
                case LayoutElementKind.Coin:
                {
                    var c = Clone(d.coins[r.index]);
                    c.x = Round01(c.x + dx);
                    d.coins = Append(d.coins, c);
                    break;
                }
                case LayoutElementKind.Special:
                {
                    int slot = FreeSlot(d);
                    if (slot < 0)
                    {
                        message = $"특수 수집품은 최대 {StageSession.SpecialSlotCount}개입니다 (빈 slot 없음).";
                        return false;
                    }
                    var c = Clone(d.specials[r.index]);
                    c.x = Round01(c.x + dx);
                    c.slot = slot;
                    c.id = NextId(d, LayoutElementKind.Special, null, c.x);
                    d.specials = Append(d.specials, c);
                    break;
                }
                case LayoutElementKind.Spike:
                {
                    var c = Clone(d.spikes[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Spike, null, c.x);
                    d.spikes = Append(d.spikes, c);
                    break;
                }
                case LayoutElementKind.Mover:
                {
                    var c = Clone(d.movers[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Mover, null, c.x);
                    d.movers = Append(d.movers, c);
                    break;
                }
                case LayoutElementKind.Enemy:
                {
                    var c = Clone(d.enemies[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Enemy, null, c.x, c.monsterId);
                    d.enemies = Append(d.enemies, c);
                    break;
                }
                case LayoutElementKind.Faller:
                {
                    var c = Clone(d.fallers[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Faller, null, c.x);
                    d.fallers = Append(d.fallers, c);
                    break;
                }
                case LayoutElementKind.Boss:
                    message = "보스는 복제할 수 없습니다.";
                    return false;
                case LayoutElementKind.SpikeTrap:
                {
                    var c = Clone(d.spikeTraps[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.SpikeTrap, null, c.x);
                    d.spikeTraps = Append(d.spikeTraps, c);
                    break;
                }
                case LayoutElementKind.Switch:
                {
                    var c = Clone(d.switches[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Switch, null, c.x);
                    d.switches = Append(d.switches, c);
                    break;
                }
                case LayoutElementKind.Gate:
                {
                    var c = Clone(d.gates[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Gate, null, c.x);
                    d.gates = Append(d.gates, c);
                    break;
                }
                case LayoutElementKind.Decoration:
                {
                    var c = Clone(d.decorations[r.index]);
                    c.x = Round01(c.x + dx);
                    c.id = NextId(d, LayoutElementKind.Decoration, null, c.x);
                    d.decorations = Append(d.decorations, c);
                    break;
                }
            }
            created = new LayoutElementRef(r.kind, LengthOf(d, r.kind) - 1);
            Assert.IsTrue(created.IsValid(d), "Duplicate must return a valid reference.");
            return true;
        }

        private static int LengthOf(StageLayoutData d, LayoutElementKind kind)
        {
            switch (kind)
            {
                case LayoutElementKind.Section: return d.sections.Length;
                case LayoutElementKind.Block: return d.blocks.Length;
                case LayoutElementKind.Checkpoint: return d.checkpoints.Length;
                case LayoutElementKind.Coin: return d.coins.Length;
                case LayoutElementKind.Special: return d.specials.Length;
                case LayoutElementKind.Spike: return d.spikes.Length;
                case LayoutElementKind.Mover: return d.movers.Length;
                case LayoutElementKind.Enemy: return d.enemies.Length;
                case LayoutElementKind.Faller: return d.fallers.Length;
                case LayoutElementKind.Decoration: return d.decorations.Length;
                case LayoutElementKind.SpikeTrap: return d.spikeTraps.Length;
                case LayoutElementKind.Switch: return d.switches.Length;
                case LayoutElementKind.Gate: return d.gates.Length;
                default: return 0;
            }
        }

        // ------------------------------------------------------------------ position

        /// <summary>The element's anchor: (x, y); for a section (startX, 0).</summary>
        public static Vector2 GetPosition(StageLayoutData d, LayoutElementRef r)
        {
            Assert.IsTrue(r.IsValid(d), "GetPosition: invalid reference " + r);
            switch (r.kind)
            {
                case LayoutElementKind.Spawn: return new Vector2(d.spawn.x, d.spawn.y);
                case LayoutElementKind.Goal: return new Vector2(d.goal.x, d.goal.y);
                case LayoutElementKind.Section: return new Vector2(d.sections[r.index].startX, 0f);
                case LayoutElementKind.Block: return new Vector2(d.blocks[r.index].x, d.blocks[r.index].y);
                case LayoutElementKind.Checkpoint: return new Vector2(d.checkpoints[r.index].x, d.checkpoints[r.index].y);
                case LayoutElementKind.Coin: return new Vector2(d.coins[r.index].x, d.coins[r.index].y);
                case LayoutElementKind.Special: return new Vector2(d.specials[r.index].x, d.specials[r.index].y);
                case LayoutElementKind.Spike: return new Vector2(d.spikes[r.index].x, d.spikes[r.index].y);
                case LayoutElementKind.Mover: return new Vector2(d.movers[r.index].x, d.movers[r.index].y);
                case LayoutElementKind.Enemy: return new Vector2(d.enemies[r.index].x, d.enemies[r.index].y);
                case LayoutElementKind.Decoration: return new Vector2(d.decorations[r.index].x, d.decorations[r.index].y);
                case LayoutElementKind.SpikeTrap: return new Vector2(d.spikeTraps[r.index].x, d.spikeTraps[r.index].y);
                case LayoutElementKind.Switch: return new Vector2(d.switches[r.index].x, d.switches[r.index].y);
                case LayoutElementKind.Gate: return new Vector2(d.gates[r.index].x, d.gates[r.index].y);
                case LayoutElementKind.Boss: return new Vector2(d.boss.x, d.boss.y);
                default: return new Vector2(d.fallers[r.index].x, d.fallers[r.index].y);
            }
        }

        /// <summary>Moves the element to <paramref name="p"/> (rounded to 0.01 m). A section keeps its width and ignores y.</summary>
        public static void SetPosition(StageLayoutData d, LayoutElementRef r, Vector2 p)
        {
            Assert.IsTrue(r.IsValid(d), "SetPosition: invalid reference " + r);
            float x = Round01(p.x);
            float y = Round01(p.y);
            switch (r.kind)
            {
                case LayoutElementKind.Spawn: d.spawn.x = x; d.spawn.y = y; break;
                case LayoutElementKind.Goal: d.goal.x = x; d.goal.y = y; break;
                case LayoutElementKind.Section:
                {
                    var s = d.sections[r.index];
                    float width = s.endX - s.startX;
                    s.startX = x;
                    s.endX = Round01(x + width);
                    break;
                }
                case LayoutElementKind.Block: d.blocks[r.index].x = x; d.blocks[r.index].y = y; break;
                case LayoutElementKind.Checkpoint: d.checkpoints[r.index].x = x; d.checkpoints[r.index].y = y; break;
                case LayoutElementKind.Coin: d.coins[r.index].x = x; d.coins[r.index].y = y; break;
                case LayoutElementKind.Special: d.specials[r.index].x = x; d.specials[r.index].y = y; break;
                case LayoutElementKind.Spike: d.spikes[r.index].x = x; d.spikes[r.index].y = y; break;
                case LayoutElementKind.Mover: d.movers[r.index].x = x; d.movers[r.index].y = y; break;
                case LayoutElementKind.Enemy: d.enemies[r.index].x = x; d.enemies[r.index].y = y; break;
                case LayoutElementKind.Faller: d.fallers[r.index].x = x; d.fallers[r.index].y = y; break;
                case LayoutElementKind.Decoration: d.decorations[r.index].x = x; d.decorations[r.index].y = y; break;
                case LayoutElementKind.SpikeTrap: d.spikeTraps[r.index].x = x; d.spikeTraps[r.index].y = y; break;
                case LayoutElementKind.Switch: d.switches[r.index].x = x; d.switches[r.index].y = y; break;
                case LayoutElementKind.Gate: d.gates[r.index].x = x; d.gates[r.index].y = y; break;
                case LayoutElementKind.Boss:
                {
                    float dx = x - d.boss.x; // the arena moves with the boss
                    d.boss.x = x;
                    d.boss.y = y;
                    d.boss.arenaMinX = Round01(d.boss.arenaMinX + dx);
                    d.boss.arenaMaxX = Round01(d.boss.arenaMaxX + dx);
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ handles

        /// <summary>
        /// Applies a handle drag. <paramref name="value"/> is the (already snapped) world coordinate of the handle:
        /// x for Left / Right / PatrolEnd / TriggerWidth, y for Top / Bottom, the ghost's left x (axis x) or top y
        /// (axis y) for MoverEnd. Results are clamped to the minimum sizes (the 7.4 lower bounds); returns whether anything changed.
        /// </summary>
        public static bool SetEdge(StageLayoutData d, LayoutElementRef r, LayoutHandle handle, float value, float grid)
        {
            Assert.IsTrue(r.IsValid(d), "SetEdge: invalid reference " + r);
            float minBlock = Mathf.Max(grid, MinSize);
            Vector4 before = EdgeSnapshot(d, r);
            switch (r.kind)
            {
                case LayoutElementKind.Block:
                {
                    var b = d.blocks[r.index];
                    if (handle == LayoutHandle.Left)
                    {
                        float right = b.x + b.w;
                        b.x = Round01(Mathf.Min(value, right - minBlock));
                        b.w = Round01(right - b.x);
                    }
                    else if (handle == LayoutHandle.Right)
                    {
                        b.w = Round01(Mathf.Max(minBlock, value - b.x));
                    }
                    else if (handle == LayoutHandle.Top)
                    {
                        float bottom = b.y - b.h;
                        b.y = Round01(Mathf.Max(value, bottom + minBlock));
                        b.h = Round01(b.y - bottom);
                    }
                    else if (handle == LayoutHandle.Bottom)
                    {
                        float bottom = Mathf.Min(value, b.y - minBlock);
                        b.h = Round01(b.y - bottom);
                    }
                    break;
                }
                case LayoutElementKind.Spike:
                {
                    var s = d.spikes[r.index];
                    if (handle == LayoutHandle.Left)
                    {
                        float right = s.x + s.w;
                        s.x = Round01(Mathf.Min(value, right - MinSpikeWidth));
                        s.w = Round01(right - s.x);
                    }
                    else if (handle == LayoutHandle.Right)
                    {
                        s.w = Round01(Mathf.Max(MinSpikeWidth, value - s.x));
                    }
                    break;
                }
                case LayoutElementKind.SpikeTrap:
                {
                    var t = d.spikeTraps[r.index];
                    if (handle == LayoutHandle.Left)
                    {
                        float right = t.x + t.w;
                        t.x = Round01(Mathf.Min(value, right - MinSpikeTrapWidth));
                        t.w = Round01(right - t.x);
                    }
                    else if (handle == LayoutHandle.Right)
                    {
                        t.w = Round01(Mathf.Max(MinSpikeTrapWidth, value - t.x));
                    }
                    break;
                }
                case LayoutElementKind.Gate:
                    if (handle == LayoutHandle.Top)
                    {
                        var g = d.gates[r.index];
                        g.h = Round01(Mathf.Max(MinGateHeight, value - g.y));
                    }
                    break;
                case LayoutElementKind.Boss:
                    if (handle == LayoutHandle.Left)
                        d.boss.arenaMinX = Round01(Mathf.Min(value, d.boss.x - 1f));
                    else if (handle == LayoutHandle.Right)
                        d.boss.arenaMaxX = Round01(Mathf.Max(value, d.boss.x + 1f));
                    break;
                case LayoutElementKind.Mover:
                {
                    var m = d.movers[r.index];
                    if (handle == LayoutHandle.Right)
                    {
                        m.w = Round01(Mathf.Max(MinMoverWidth, value - m.x));
                    }
                    else if (handle == LayoutHandle.MoverEnd)
                    {
                        float dist = value - (m.axis == "y" ? m.y : m.x);
                        if (Mathf.Abs(dist) < MinDistance)
                            dist = dist < 0f ? -MinDistance : MinDistance;
                        m.distance = Round01(dist);
                    }
                    break;
                }
                case LayoutElementKind.Enemy:
                    if (handle == LayoutHandle.PatrolEnd)
                    {
                        var e = d.enemies[r.index];
                        e.range = Round01(Mathf.Max(MinEnemyRange, value - e.x));
                    }
                    break;
                case LayoutElementKind.Faller:
                    if (handle == LayoutHandle.TriggerWidth)
                    {
                        var f = d.fallers[r.index];
                        f.triggerHalfWidth = Round01(Mathf.Max(MinTriggerHalfWidth, Mathf.Abs(value - f.x)));
                    }
                    break;
                case LayoutElementKind.Section:
                {
                    var s = d.sections[r.index];
                    if (handle == LayoutHandle.Left)
                        s.startX = Round01(Mathf.Min(value, s.endX));
                    else if (handle == LayoutHandle.Right)
                        s.endX = Round01(Mathf.Max(value, s.startX));
                    break;
                }
            }
            return before != EdgeSnapshot(d, r);
        }

        // The values SetEdge can change, for change detection without serializing the whole layout.
        private static Vector4 EdgeSnapshot(StageLayoutData d, LayoutElementRef r)
        {
            switch (r.kind)
            {
                case LayoutElementKind.Block: { var b = d.blocks[r.index]; return new Vector4(b.x, b.y, b.w, b.h); }
                case LayoutElementKind.Spike: { var s = d.spikes[r.index]; return new Vector4(s.x, s.w, 0f, 0f); }
                case LayoutElementKind.Mover: { var m = d.movers[r.index]; return new Vector4(m.w, m.distance, 0f, 0f); }
                case LayoutElementKind.Enemy: return new Vector4(d.enemies[r.index].range, 0f, 0f, 0f);
                case LayoutElementKind.Faller: return new Vector4(d.fallers[r.index].triggerHalfWidth, 0f, 0f, 0f);
                case LayoutElementKind.Section: { var s = d.sections[r.index]; return new Vector4(s.startX, s.endX, 0f, 0f); }
                case LayoutElementKind.SpikeTrap: { var t = d.spikeTraps[r.index]; return new Vector4(t.x, t.w, 0f, 0f); }
                case LayoutElementKind.Gate: return new Vector4(d.gates[r.index].h, 0f, 0f, 0f);
                case LayoutElementKind.Boss: return new Vector4(d.boss.arenaMinX, d.boss.arenaMaxX, 0f, 0f);
                default: return Vector4.zero;
            }
        }

        // ------------------------------------------------------------------ ids / section fields

        /// <summary>Element id, or null for kinds without one (coin, spawn, goal).</summary>
        public static string GetId(StageLayoutData d, LayoutElementRef r)
        {
            Assert.IsTrue(r.IsValid(d), "GetId: invalid reference " + r);
            switch (r.kind)
            {
                case LayoutElementKind.Section: return d.sections[r.index].id;
                case LayoutElementKind.Block: return d.blocks[r.index].id;
                case LayoutElementKind.Checkpoint: return d.checkpoints[r.index].id;
                case LayoutElementKind.Special: return d.specials[r.index].id;
                case LayoutElementKind.Spike: return d.spikes[r.index].id;
                case LayoutElementKind.Mover: return d.movers[r.index].id;
                case LayoutElementKind.Enemy: return d.enemies[r.index].id;
                case LayoutElementKind.Faller: return d.fallers[r.index].id;
                case LayoutElementKind.Decoration: return d.decorations[r.index].id;
                case LayoutElementKind.SpikeTrap: return d.spikeTraps[r.index].id;
                case LayoutElementKind.Switch: return d.switches[r.index].id;
                case LayoutElementKind.Gate: return d.gates[r.index].id;
                default: return null;
            }
        }

        /// <summary>Renames an element; false (no change) if the id is empty, unsupported for the kind, or already used.</summary>
        public static bool RenameId(StageLayoutData d, LayoutElementRef r, string newId)
        {
            Assert.IsTrue(r.IsValid(d), "RenameId: invalid reference " + r);
            if (string.IsNullOrWhiteSpace(newId))
                return false;
            newId = newId.Trim();
            string current = GetId(d, r);
            if (current == null)
                return false;
            if (current == newId)
                return true;
            if (AllIds(d).Contains(newId))
                return false;
            if (r.kind == LayoutElementKind.Section)
                return RenameSection(d, r.index, newId);
            switch (r.kind)
            {
                case LayoutElementKind.Block: d.blocks[r.index].id = newId; break;
                case LayoutElementKind.Checkpoint: d.checkpoints[r.index].id = newId; break;
                case LayoutElementKind.Special: d.specials[r.index].id = newId; break;
                case LayoutElementKind.Spike: d.spikes[r.index].id = newId; break;
                case LayoutElementKind.Mover: d.movers[r.index].id = newId; break;
                case LayoutElementKind.Enemy: d.enemies[r.index].id = newId; break;
                case LayoutElementKind.Faller: d.fallers[r.index].id = newId; break;
                case LayoutElementKind.Decoration: d.decorations[r.index].id = newId; break;
                case LayoutElementKind.SpikeTrap: d.spikeTraps[r.index].id = newId; break;
                case LayoutElementKind.Switch: d.switches[r.index].id = newId; break;
                case LayoutElementKind.Gate: d.gates[r.index].id = newId; break;
            }
            return true;
        }

        /// <summary>Renames a section and every reference to it (one edit). False if the id is empty or taken.</summary>
        public static bool RenameSection(StageLayoutData d, int index, string newId)
        {
            Assert.IsTrue(new LayoutElementRef(LayoutElementKind.Section, index).IsValid(d), "RenameSection: invalid index.");
            if (string.IsNullOrWhiteSpace(newId))
                return false;
            newId = newId.Trim();
            string old = d.sections[index].id;
            if (old == newId)
                return true;
            if (AllIds(d).Contains(newId))
                return false;
            d.sections[index].id = newId;
            Retarget(d, old, newId);
            return true;
        }

        /// <summary>The element's section field (null for sections, spawn, goal).</summary>
        public static string GetSection(StageLayoutData d, LayoutElementRef r)
        {
            Assert.IsTrue(r.IsValid(d), "GetSection: invalid reference " + r);
            switch (r.kind)
            {
                case LayoutElementKind.Block: return d.blocks[r.index].section;
                case LayoutElementKind.Checkpoint: return d.checkpoints[r.index].section;
                case LayoutElementKind.Coin: return d.coins[r.index].section;
                case LayoutElementKind.Special: return d.specials[r.index].section;
                case LayoutElementKind.Spike: return d.spikes[r.index].section;
                case LayoutElementKind.Mover: return d.movers[r.index].section;
                case LayoutElementKind.Enemy: return d.enemies[r.index].section;
                case LayoutElementKind.Faller: return d.fallers[r.index].section;
                case LayoutElementKind.SpikeTrap: return d.spikeTraps[r.index].section;
                case LayoutElementKind.Switch: return d.switches[r.index].section;
                case LayoutElementKind.Gate: return d.gates[r.index].section;
                default: return null;
            }
        }

        public static void SetSection(StageLayoutData d, LayoutElementRef r, string sectionId)
        {
            Assert.IsTrue(r.IsValid(d), "SetSection: invalid reference " + r);
            string v = sectionId ?? "";
            switch (r.kind)
            {
                case LayoutElementKind.Block: d.blocks[r.index].section = v; break;
                case LayoutElementKind.Checkpoint: d.checkpoints[r.index].section = v; break;
                case LayoutElementKind.Coin: d.coins[r.index].section = v; break;
                case LayoutElementKind.Special: d.specials[r.index].section = v; break;
                case LayoutElementKind.Spike: d.spikes[r.index].section = v; break;
                case LayoutElementKind.Mover: d.movers[r.index].section = v; break;
                case LayoutElementKind.Enemy: d.enemies[r.index].section = v; break;
                case LayoutElementKind.Faller: d.fallers[r.index].section = v; break;
                case LayoutElementKind.SpikeTrap: d.spikeTraps[r.index].section = v; break;
                case LayoutElementKind.Switch: d.switches[r.index].section = v; break;
                case LayoutElementKind.Gate: d.gates[r.index].section = v; break;
                default: Assert.IsTrue(false, "SetSection: kind has no section field: " + r.kind); break;
            }
        }

        // ------------------------------------------------------------------ section push (P2, MT-023)

        /// <summary>
        /// Inserts (dx &gt; 0) or removes (dx &lt; 0) space at <paramref name="atX"/>: everything with x &gt;= atX moves by dx;
        /// blocks, spikes... that only cross atX grow / shrink at their far end; sections (start &gt;= atX, end &gt;= atX) and
        /// goal / length follow. Refuses (false, nothing changed) when a removal would delete elements in [atX+dx, atX).
        /// </summary>
        public static bool ShiftFrom(StageLayoutData d, float atX, float dx, out string message)
        {
            Assert.IsNotNull(d, "ShiftFrom needs data.");
            message = null;
            if (Mathf.Approximately(dx, 0f))
            {
                message = "dx가 0입니다.";
                return false;
            }
            float lo = atX + dx; // start of the removed range when dx < 0
            if (dx < 0f)
            {
                if (lo < 0f)
                {
                    message = "삭제 범위가 x = 0 밖으로 나갑니다.";
                    return false;
                }
                string blocker = FirstStartIn(d, lo, atX);
                if (blocker != null)
                {
                    message = $"삭제될 범위 [{lo:0.##}, {atX:0.##})에 요소가 있습니다: {blocker}";
                    return false;
                }
            }

            // Dry run on a copy: widths must stay valid before the real data is touched.
            var work = Clone(d);
            Normalize(work);
            if (!ApplyShift(work, atX, dx, out message))
                return false;
            ApplyShift(d, atX, dx, out _);
            return true;
        }

        private static void Normalize(StageLayoutData d) => StageLayoutJson.Normalize(d);

        private static string FirstStartIn(StageLayoutData d, float lo, float hi)
        {
            bool In(float x) => x >= lo && x < hi;
            foreach (var e in d.blocks ?? Array.Empty<LayoutBlock>()) if (In(e.x)) return e.id;
            foreach (var e in d.checkpoints ?? Array.Empty<LayoutCheckpoint>()) if (In(e.x)) return e.id;
            foreach (var e in d.coins ?? Array.Empty<LayoutCoin>()) if (In(e.x)) return "coin x=" + e.x;
            foreach (var e in d.specials ?? Array.Empty<LayoutSpecial>()) if (In(e.x)) return e.id;
            foreach (var e in d.spikes ?? Array.Empty<LayoutSpike>()) if (In(e.x)) return e.id;
            foreach (var e in d.movers ?? Array.Empty<LayoutMover>()) if (In(e.x)) return e.id;
            foreach (var e in d.enemies ?? Array.Empty<LayoutEnemy>()) if (In(e.x)) return e.id;
            foreach (var e in d.fallers ?? Array.Empty<LayoutFaller>()) if (In(e.x)) return e.id;
            foreach (var e in d.decorations ?? Array.Empty<LayoutDecoration>()) if (In(e.x)) return e.id;
            foreach (var e in d.spikeTraps ?? Array.Empty<LayoutSpikeTrap>()) if (In(e.x)) return e.id;
            foreach (var e in d.switches ?? Array.Empty<LayoutSwitch>()) if (In(e.x)) return e.id;
            foreach (var e in d.gates ?? Array.Empty<LayoutGate>()) if (In(e.x)) return e.id;
            if (d.HasBoss && (In(d.boss.x) || In(d.boss.arenaMinX))) return "boss";
            if (d.spawn != null && In(d.spawn.x)) return "spawn";
            if (d.goal != null && In(d.goal.x)) return "goal";
            return null;
        }

        private static bool ApplyShift(StageLayoutData d, float atX, float dx, out string message)
        {
            message = null;
            // Elements that begin before atX but extend past it grow / shrink by dx (their far end moves with the content).
            bool Crosses(float x, float w) => x < atX && x + w > atX;
            foreach (var b in d.blocks ?? Array.Empty<LayoutBlock>())
            {
                if (b.x >= atX) b.x = Round01(b.x + dx);
                else if (Crosses(b.x, b.w))
                {
                    if (b.w + dx < MinSize) { message = $"블록 '{b.id}'의 폭이 너무 작아집니다."; return false; }
                    b.w = Round01(b.w + dx);
                }
            }
            foreach (var s in d.spikes ?? Array.Empty<LayoutSpike>())
            {
                if (s.x >= atX) s.x = Round01(s.x + dx);
            }
            foreach (var m in d.movers ?? Array.Empty<LayoutMover>())
            {
                if (m.x >= atX) m.x = Round01(m.x + dx);
                else if (Crosses(m.x, m.w))
                {
                    if (m.w + dx < MinMoverWidth) { message = $"이동 발판 '{m.id}'의 폭이 너무 작아집니다."; return false; }
                    m.w = Round01(m.w + dx);
                }
            }
            foreach (var e in d.checkpoints ?? Array.Empty<LayoutCheckpoint>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            foreach (var e in d.coins ?? Array.Empty<LayoutCoin>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            foreach (var e in d.specials ?? Array.Empty<LayoutSpecial>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            foreach (var e in d.enemies ?? Array.Empty<LayoutEnemy>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            foreach (var e in d.fallers ?? Array.Empty<LayoutFaller>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            foreach (var e in d.decorations ?? Array.Empty<LayoutDecoration>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            foreach (var e in d.spikeTraps ?? Array.Empty<LayoutSpikeTrap>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            foreach (var e in d.switches ?? Array.Empty<LayoutSwitch>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            foreach (var e in d.gates ?? Array.Empty<LayoutGate>()) if (e.x >= atX) e.x = Round01(e.x + dx);
            if (d.HasBoss)
            {
                if (d.boss.x >= atX) d.boss.x = Round01(d.boss.x + dx);
                if (d.boss.arenaMinX >= atX) d.boss.arenaMinX = Round01(d.boss.arenaMinX + dx);
                if (d.boss.arenaMaxX >= atX) d.boss.arenaMaxX = Round01(d.boss.arenaMaxX + dx);
            }
            foreach (var s in d.sections ?? Array.Empty<LayoutSection>())
            {
                if (s.startX >= atX) s.startX = Round01(s.startX + dx);
                if (s.endX >= atX) s.endX = Round01(s.endX + dx);
                if (s.endX < s.startX) s.endX = s.startX;
            }
            if (d.spawn != null && d.spawn.x >= atX) d.spawn.x = Round01(d.spawn.x + dx);
            if (d.goal != null && d.goal.x >= atX) d.goal.x = Round01(d.goal.x + dx);
            if (d.length >= atX) d.length = Round01(d.length + dx);
            return true;
        }
    }
}
