using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace MyGame.EditorTools
{
    /// <summary>Validation of a stage layout per Docs/stage-layout-schema.md. Errors block the build; warnings are logged.</summary>
    internal sealed class StageLayoutValidator
    {
        private const float SurfaceTolerance = 0.05f;
        private const float Epsilon = 0.001f;
        private const float MaxGap = 4.0f;
        private const float MaxStepUp = 2.6f;

        private static readonly string[] RequiredKeys =
        {
            "version", "stageId", "length", "killY", "cameraMinY", "spawn", "goal", "sections", "blocks",
            "checkpoints", "coins", "specials", "spikes", "movers", "enemies", "fallers",
        };

        private static readonly string[] BlockKinds = { "ground", "platform", "wall" };

        private readonly string label;
        private readonly StageLayoutData d;
        private readonly List<string> errors;
        private readonly List<string> warnings;

        private StageLayoutValidator(string label, StageLayoutData data, List<string> errors, List<string> warnings)
        {
            this.label = label;
            d = data;
            this.errors = errors;
            this.warnings = warnings;
        }

        /// <param name="expectedStageId">File name stem the stageId must match, or null to skip that check.</param>
        public static void Validate(string label, string rawJson, StageLayoutData data, string expectedStageId,
            List<string> errors, List<string> warnings)
        {
            foreach (var key in RequiredKeys)
            {
                if (!rawJson.Contains("\"" + key + "\""))
                    errors.Add($"{label}: required field '{key}' is missing.");
            }
            if (errors.Count > 0 && data == null)
                return;
            new StageLayoutValidator(label, data, errors, warnings).Run(expectedStageId);
        }

        private void Err(string msg) => errors.Add($"{label}: {msg}");
        private void Warn(string msg) => warnings.Add($"{label}: {msg}");

        /// <summary>The validated monster table, or null (with an error) if it cannot be loaded.</summary>
        private MonsterTable LoadMonsterTable()
        {
            try
            {
                return MonsterTableTool.Load(new List<string>());
            }
            catch (Exception ex)
            {
                Err("monster table: " + ex.Message);
                return null;
            }
        }

        private static T[] A<T>(T[] a) => a ?? Array.Empty<T>();
        private static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        private void Run(string expectedStageId)
        {
            if (d.version != 1)
                Err($"unsupported version {d.version} (expected 1).");
            if (string.IsNullOrEmpty(d.stageId))
                Err("stageId is empty.");
            else if (expectedStageId != null && d.stageId != expectedStageId)
                Err($"stageId '{d.stageId}' does not match the file name '{expectedStageId}'.");
            if (d.length <= 0f)
                Err("length must be > 0.");
            if (d.spawn == null) Err("spawn is missing.");
            if (d.goal == null) Err("goal is missing.");
            if (d.spawn == null || d.goal == null)
                return;

            var blocks = A(d.blocks);
            CheckIds();
            CheckBlocks(blocks);

            Inside("spawn", d.spawn.x);
            Inside("goal", d.goal.x);
            if (d.spawn.y <= d.killY)
                Err("spawn is at or below killY.");
            Standing("spawn", d.spawn.x, d.spawn.y, blocks, 0f);
            Standing("goal", d.goal.x, d.goal.y, blocks, 0f);

            foreach (var c in A(d.checkpoints))
            {
                Inside("checkpoint " + c.id, c.x);
                Standing("checkpoint " + c.id, c.x, c.y, blocks, 0f);
            }
            foreach (var s in A(d.spikes))
            {
                if (s.w <= 0f) Err($"spikes '{s.id}': w must be > 0.");
                Inside("spikes " + s.id, s.x);
                Inside("spikes " + s.id, s.x + s.w);
                Standing("spikes " + s.id, s.x + s.w * 0.5f, s.y, blocks, 0f);
                if (s.w > 0f && (!Supported(s.x, s.y, blocks) || !Supported(s.x + s.w, s.y, blocks)))
                    Warn($"spikes '{s.id}' overhang the edge of its surface.");
            }
            MonsterTable monsters = d.enemies != null && d.enemies.Length > 0 ? LoadMonsterTable() : null;
            foreach (var e in A(d.enemies))
            {
                if (e.range <= 0f) Err($"enemy '{e.id}': range must be > 0.");
                if (e.speed < 0f) Err($"enemy '{e.id}': speed must be >= 0 (0 = the monster table's moveSpeed).");
                if (monsters != null && (!monsters.TryGet(e.ResolvedMonsterId, out var monster) || !monster.IsImplemented))
                    Err($"enemy '{e.id}': monsterId {e.ResolvedMonsterId} is not an Implemented row of {MonsterTableTool.TablePath}.");
                Inside("enemy " + e.id, e.x);
                Inside("enemy " + e.id, e.x + e.range);
                Standing("enemy " + e.id, e.x, e.y, blocks, 0f);
                if (!Supported(e.x + e.range, e.y, blocks))
                    Warn($"enemy '{e.id}': the far end of its patrol (x + range) has no surface at y {F(e.y)}.");
            }
            foreach (var m in A(d.movers))
            {
                if (m.w <= 0f) Err($"mover '{m.id}': w must be > 0.");
                if (m.axis != "x" && m.axis != "y") Err($"mover '{m.id}': axis must be \"x\" or \"y\" (got '{m.axis}').");
                if (m.speed <= 0f) Err($"mover '{m.id}': speed must be > 0.");
                if (Math.Abs(m.distance) < Epsilon) Err($"mover '{m.id}': distance must not be 0.");
                if (m.wait < 0f) Err($"mover '{m.id}': wait must be >= 0.");
                float right = m.x + m.w + (m.axis == "x" ? Math.Max(0f, m.distance) : 0f);
                float left = m.x + (m.axis == "x" ? Math.Min(0f, m.distance) : 0f);
                if (left < -Epsilon || right > d.length + Epsilon)
                    Err($"mover '{m.id}' leaves 0..length.");
            }
            foreach (var f in A(d.fallers))
            {
                if (f.triggerHalfWidth <= 0f) Err($"faller '{f.id}': triggerHalfWidth must be > 0.");
                if (f.delay < 0f) Err($"faller '{f.id}': delay must be >= 0.");
                if (f.respawn <= 0f) Err($"faller '{f.id}': respawn must be > 0.");
                Inside("faller " + f.id, f.x);
            }
            foreach (var c in A(d.coins))
            {
                Inside("coin", c.x);
                WarnIfSolid("coin", c.x, c.y, blocks);
            }
            foreach (var s in A(d.specials))
            {
                Inside("special " + s.id, s.x);
                WarnIfSolid("special " + s.id, s.x, s.y, blocks);
            }

            CheckGimmicks(blocks);
            CheckTheme();
            CheckDecorations();
            CheckSpecialSlots();
            CheckSections();
            WarnReachability(blocks);
        }

        // ------------------------------------------------------------------ gimmicks and boss (contract v1.4)

        private void CheckGimmicks(LayoutBlock[] blocks)
        {
            foreach (var t in A(d.spikeTraps))
            {
                string who = $"spikeTrap '{t.id}'";
                if (t.w < 1f) Err($"{who}: w must be >= 1.");
                if (t.period < 0.5f) Err($"{who}: period must be >= 0.5.");
                if (t.upTime <= 0f) Err($"{who}: upTime must be > 0.");
                if (t.warn <= 0f) Err($"{who}: warn must be > 0 (the warning is the fairness telegraph).");
                if (t.upTime + t.warn >= t.period) Err($"{who}: upTime + warn must be < period (needs a safe down phase).");
                Inside(who, t.x);
                Inside(who, t.x + t.w);
                Standing(who, t.x + t.w * 0.5f, t.y, blocks, 0f);
                if (t.w >= 1f && (!Supported(t.x, t.y, blocks) || !Supported(t.x + t.w, t.y, blocks)))
                    Warn($"{who} overhangs the edge of its surface.");
            }

            var switchChannels = new HashSet<int>();
            foreach (var s in A(d.switches))
            {
                string who = $"switch '{s.id}'";
                Inside(who, s.x);
                Standing(who, s.x, s.y, blocks, 0f);
                if (s.channel == BossController.ArenaChannel) Err($"{who}: channel {s.channel} is reserved for boss arenas.");
                switchChannels.Add(s.channel);
            }
            var gateChannels = new HashSet<int>();
            foreach (var g in A(d.gates))
            {
                string who = $"gate '{g.id}'";
                if (g.h < 0.5f) Err($"{who}: h must be >= 0.5.");
                if (g.openSeconds <= 0f) Err($"{who}: openSeconds must be > 0.");
                if (g.channel == BossController.ArenaChannel) Err($"{who}: channel {g.channel} is reserved for boss arenas.");
                Inside(who, g.x);
                Standing(who, g.x, g.y, blocks, 0f);
                gateChannels.Add(g.channel);
                if (!switchChannels.Contains(g.channel))
                    Warn($"{who}: no switch raises channel {g.channel}; the gate never opens.");
            }
            foreach (var s in A(d.switches))
            {
                if (!gateChannels.Contains(s.channel))
                    Warn($"switch '{s.id}': no gate listens to channel {s.channel}.");
            }

            if (!d.HasBoss)
                return;
            var b = d.boss;
            var data = BossCatalog.Load(b.bossId);
            if (data == null && (b.bossId == 9001 || b.bossId == 9002))
                Warn($"boss {b.bossId}: BossData {BossCatalog.PathFor(b.bossId)} does not exist yet; the builder (or Tools/MyGame/Bosses/Create Default Boss Data And Prefabs) creates it.");
            else if (data == null)
                Err($"boss {b.bossId}: no BossData asset at {BossCatalog.PathFor(b.bossId)}.");
            else
            {
                string problem = data.Validate();
                if (problem != null) Err($"boss {b.bossId}: BossData invalid: {problem}");
            }
            float half = data != null ? data.bodyWidth * 0.5f : 1.5f;
            if (b.arenaMinX < 0f || b.arenaMaxX > d.length + Epsilon)
                Err($"boss {b.bossId}: arena {F(b.arenaMinX)}..{F(b.arenaMaxX)} must lie inside 0..{F(d.length)}.");
            if (b.arenaMaxX - b.arenaMinX < 16f)
                Err($"boss {b.bossId}: arena is {F(b.arenaMaxX - b.arenaMinX)} m wide; at least 16 m is needed.");
            if (b.x - half < b.arenaMinX || b.x + half > b.arenaMaxX)
                Err($"boss {b.bossId}: the boss at x={F(b.x)} does not fit inside its arena.");
            Standing($"boss {b.bossId}", b.x, b.y, blocks, 0f);
            if (!Supported(b.arenaMinX, b.y, blocks) || !Supported(b.arenaMaxX, b.y, blocks))
                Warn($"boss {b.bossId}: the arena floor is not continuous at y {F(b.y)} at both arena ends.");
            if (b.arenaMaxX < d.length - 0.5f)
                Warn($"boss {b.bossId}: arenaMaxX is {F(d.length - b.arenaMaxX)} m before the end of the course; the player could leave on the right.");
            bool checkpointBefore = false;
            foreach (var c in A(d.checkpoints))
                checkpointBefore |= c.x < b.arenaMinX && c.x >= b.arenaMinX - 60f;
            if (!checkpointBefore)
                Warn($"boss {b.bossId}: place a checkpoint within 60 m before the arena (a death respawns there).");
            foreach (var g in A(d.gates))
            {
                if (g.x > b.arenaMinX - 1.5f && g.x < b.arenaMinX + 1.5f)
                    Warn($"gate '{g.id}' is close to the arena entrance gate at x={F(b.arenaMinX)}.");
            }
        }

        // ------------------------------------------------------------------ theme / decorations (contract v1.3)

        /// <summary>Self-check hook: when set, decorations are checked against this table instead of PropTable.csv.</summary>
        internal static PropTable PropTableOverride;

        private const float PlayLaneHalfDepth = 2f; // blocks are 4 m deep, centred on z = 0

        private void CheckTheme()
        {
            if (string.IsNullOrEmpty(d.theme))
                return;
            if (!StageThemeKit.IsValidThemeName(d.theme))
            {
                Err($"theme '{d.theme}' may only contain letters, digits, '_' and '-'.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<StageThemeKit>(StageThemeKit.PathFor(d.theme)) == null)
                Warn($"theme '{d.theme}': kit asset {StageThemeKit.PathFor(d.theme)} not found (run Tools/MyGame/Map Tool/Create Or Refresh Theme Kit); primitive visuals are used.");
        }

        private PropTable LoadPropTable()
        {
            if (PropTableOverride != null)
                return PropTableOverride;
            var tableErrors = new List<string>();
            var table = PropCatalog.Load(tableErrors);
            if (table == null || tableErrors.Count > 0)
            {
                Err("prop table: " + string.Join("; ", tableErrors));
                return null;
            }
            return table;
        }

        private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private void CheckDecorations()
        {
            var decs = A(d.decorations);
            if (decs.Length == 0)
                return;
            var props = LoadPropTable();
            foreach (var dec in decs)
            {
                string who = $"decoration '{dec.id}'";
                bool numbersOk = true;
                void Number(string name, float v)
                {
                    if (Finite(v)) return;
                    Err($"{who}: {name} is not a finite number.");
                    numbersOk = false;
                }
                Number("x", dec.x);
                Number("y", dec.y);
                Number("z", dec.z);
                Number("rotY", dec.rotY);
                Number("scale", dec.scale);
                if (Finite(dec.scale) && dec.scale <= 0f)
                    Err($"{who}: scale must be > 0 (got {F(dec.scale)}).");

                if (string.IsNullOrEmpty(dec.prop))
                {
                    Err($"{who}: prop is empty.");
                    continue;
                }
                if (props == null)
                    continue;
                if (!props.TryGet(dec.prop, out var rec))
                {
                    Err($"{who}: prop '{dec.prop}' is not a row of {PropCatalog.TablePath}.");
                    continue;
                }
                if (!rec.IsImplemented)
                {
                    Err($"{who}: prop '{dec.prop}' is {rec.status}; only Implemented props can be placed.");
                    continue;
                }
                if (PropTableOverride == null && !PropCatalog.PrefabExists(rec))
                {
                    Err($"{who}: prefab of prop '{dec.prop}' not found: {rec.prefabPath}.");
                    continue;
                }
                if (!numbersOk)
                    continue;
                float z = dec.ResolveZ(rec.defaultZ);
                bool inLane = Math.Abs(z) <= PlayLaneHalfDepth;
                if (rec.collision && !inLane)
                    Warn($"{who}: prop '{dec.prop}' has collision but z={F(z)} is outside the play lane (-{F(PlayLaneHalfDepth)}..{F(PlayLaneHalfDepth)}); the player cannot reach it.");
                if (rec.category == PropTable.CategoryBackground && inLane)
                    Warn($"{who}: background prop '{dec.prop}' is inside the play lane (z={F(z)}); it may hide or block the course.");
            }
        }

        // ------------------------------------------------------------------ ids / slots / sections

        private void CheckIds()
        {
            var seen = new HashSet<string>();
            void Add(string kind, string id)
            {
                if (string.IsNullOrEmpty(id)) { Err($"{kind} has an empty id."); return; }
                if (!seen.Add(id)) Err($"duplicate id '{id}'.");
            }
            foreach (var s in A(d.sections)) Add("section", s.id);
            foreach (var b in A(d.blocks)) Add("block", b.id);
            foreach (var c in A(d.checkpoints)) Add("checkpoint", c.id);
            foreach (var s in A(d.specials)) Add("special", s.id);
            foreach (var s in A(d.spikes)) Add("spikes", s.id);
            foreach (var m in A(d.movers)) Add("mover", m.id);
            foreach (var e in A(d.enemies)) Add("enemy", e.id);
            foreach (var f in A(d.fallers)) Add("faller", f.id);
            foreach (var t in A(d.spikeTraps)) Add("spikeTrap", t.id);
            foreach (var s in A(d.switches)) Add("switch", s.id);
            foreach (var g in A(d.gates)) Add("gate", g.id);
            foreach (var dec in A(d.decorations)) Add("decoration", dec.id);
        }

        private void CheckSpecialSlots()
        {
            var specials = A(d.specials);
            var count = new int[StageSession.SpecialSlotCount];
            foreach (var s in specials)
            {
                if (s.slot < 0 || s.slot >= count.Length)
                    Err($"special '{s.id}': slot {s.slot} is outside 0..{count.Length - 1}.");
                else
                    count[s.slot]++;
            }
            for (int i = 0; i < count.Length; i++)
            {
                if (count[i] != 1)
                    Err($"specials must use each slot 0..{count.Length - 1} exactly once (slot {i} used {count[i]} times).");
            }
        }

        private void CheckSections()
        {
            var ids = new HashSet<string>();
            foreach (var s in A(d.sections))
            {
                if (s.id != null) ids.Add(s.id);
                if (s.endX < s.startX)
                    Err($"section '{s.id}': endX < startX.");
            }
            void Ref(string kind, string id, string section)
            {
                if (!string.IsNullOrEmpty(section) && !ids.Contains(section))
                    Err($"{kind} '{id}' references unknown section '{section}'.");
            }
            foreach (var b in A(d.blocks)) Ref("block", b.id, b.section);
            foreach (var c in A(d.checkpoints)) Ref("checkpoint", c.id, c.section);
            foreach (var s in A(d.specials)) Ref("special", s.id, s.section);
            foreach (var s in A(d.spikes)) Ref("spikes", s.id, s.section);
            foreach (var m in A(d.movers)) Ref("mover", m.id, m.section);
            foreach (var e in A(d.enemies)) Ref("enemy", e.id, e.section);
            foreach (var f in A(d.fallers)) Ref("faller", f.id, f.section);
            foreach (var t in A(d.spikeTraps)) Ref("spikeTrap", t.id, t.section);
            foreach (var s in A(d.switches)) Ref("switch", s.id, s.section);
            foreach (var g in A(d.gates)) Ref("gate", g.id, g.section);
        }

        // ------------------------------------------------------------------ geometry

        private void CheckBlocks(LayoutBlock[] blocks)
        {
            foreach (var b in blocks)
            {
                if (Array.IndexOf(BlockKinds, b.kind) < 0)
                    Err($"block '{b.id}': kind must be ground, platform or wall (got '{b.kind}').");
                if (!string.IsNullOrEmpty(b.surface) && Array.IndexOf(LayoutBlock.Surfaces, b.surface) < 0)
                    Err($"block '{b.id}': surface must be empty or one of {string.Join(", ", LayoutBlock.Surfaces)} (got '{b.surface}').");
                if (b.w <= 0f || b.h <= 0f)
                    Err($"block '{b.id}': w and h must be > 0.");
                if (b.x < -Epsilon || b.x + b.w > d.length + Epsilon)
                    Err($"block '{b.id}' lies outside 0..length ({F(b.x)}..{F(b.x + b.w)}).");
            }
            for (int i = 0; i < blocks.Length; i++)
            {
                for (int j = i + 1; j < blocks.Length; j++)
                {
                    var a = blocks[i];
                    var b = blocks[j];
                    bool overlapX = a.x < b.x + b.w - Epsilon && b.x < a.x + a.w - Epsilon;
                    bool overlapY = a.y - a.h < b.y - Epsilon && b.y - b.h < a.y - Epsilon;
                    if (overlapX && overlapY)
                        Err($"blocks '{a.id}' and '{b.id}' overlap.");
                }
            }
        }

        private void Inside(string what, float x)
        {
            if (x < -Epsilon || x > d.length + Epsilon)
                Err($"{what} at x={F(x)} is outside 0..{F(d.length)}.");
        }

        private static bool Supported(float x, float y, LayoutBlock[] blocks)
        {
            foreach (var b in blocks)
            {
                if (x >= b.x - SurfaceTolerance && x <= b.x + b.w + SurfaceTolerance && Math.Abs(b.y - y) <= SurfaceTolerance)
                    return true;
            }
            return false;
        }

        private void Standing(string what, float x, float y, LayoutBlock[] blocks, float unused)
        {
            if (!Supported(x, y, blocks))
                Err($"{what} (x={F(x)}, y={F(y)}) does not stand on top of a block surface (+-{SurfaceTolerance} m).");
        }

        private void WarnIfSolid(string what, float x, float y, LayoutBlock[] blocks)
        {
            foreach (var b in blocks)
            {
                if (x > b.x && x < b.x + b.w && y < b.y && y > b.y - b.h)
                {
                    Warn($"{what} at ({F(x)}, {F(y)}) is inside block '{b.id}'.");
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ reachability warnings (level design doc 2.3)

        private struct Surface
        {
            public string Id;
            public float Left, Right, Top;
            public bool Ride; // mover surface: reached by riding, so the step-up limit does not apply
        }

        private void WarnReachability(LayoutBlock[] blocks)
        {
            var list = new List<Surface>();
            foreach (var b in blocks)
                list.Add(new Surface { Id = b.id, Left = b.x, Right = b.x + b.w, Top = b.y });
            // Movers extend the walkable area over their travel range.
            foreach (var m in A(d.movers))
            {
                if (m.axis == "x")
                {
                    float lo = Math.Min(0f, m.distance), hi = Math.Max(0f, m.distance);
                    list.Add(new Surface { Id = m.id, Left = m.x + lo, Right = m.x + m.w + hi, Top = m.y, Ride = true });
                }
                else
                {
                    float lo = Math.Min(0f, m.distance), hi = Math.Max(0f, m.distance);
                    list.Add(new Surface { Id = m.id + "(low)", Left = m.x, Right = m.x + m.w, Top = m.y + lo, Ride = true });
                    list.Add(new Surface { Id = m.id + "(high)", Left = m.x, Right = m.x + m.w, Top = m.y + hi, Ride = true });
                }
            }
            list.Sort((p, q) => p.Left.CompareTo(q.Left));

            for (int i = 1; i < list.Count; i++)
            {
                var cur = list[i];
                bool anyNear = false;
                bool stepOk = false;
                float bestGap = float.MaxValue;
                for (int j = 0; j < i; j++)
                {
                    float gap = cur.Left - list[j].Right;
                    bestGap = Math.Min(bestGap, Math.Max(0f, gap));
                    if (gap <= MaxGap)
                    {
                        anyNear = true;
                        if (cur.Top - list[j].Top <= MaxStepUp)
                            stepOk = true;
                    }
                }
                if (!anyNear)
                    Warn($"'{cur.Id}' is {F(bestGap)} m from the nearest earlier surface (limit {F(MaxGap)} m).");
                else if (!stepOk && !cur.Ride)
                    Warn($"'{cur.Id}' is more than {F(MaxStepUp)} m above every nearby earlier surface.");
            }
        }
    }
}
