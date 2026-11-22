using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Automatic checks of the map tool's model rules (design 11.1). Menu: Tools/MyGame/Map Tool/Run Self Check.
    /// Reads the layout files but never writes any file. Each check is a static method that adds PASS / FAIL / INFO
    /// lines to a report; the last line is the summary.
    /// </summary>
    internal static class StageMapSelfCheck
    {
        private sealed class Report
        {
            private readonly StringBuilder sb = new StringBuilder();
            public int Passed;
            public int Failed;

            public void Pass(string id, string what)
            {
                Passed++;
                sb.Append("PASS ").Append(id).Append(' ').Append(what).Append('\n');
            }

            public void Fail(string id, string what)
            {
                Failed++;
                sb.Append("FAIL ").Append(id).Append(' ').Append(what).Append('\n');
            }

            public void Check(bool ok, string id, string what, string detailOnFail = null)
            {
                if (ok) Pass(id, what);
                else Fail(id, what + (detailOnFail != null ? " -- " + detailOnFail : ""));
            }

            public void Info(string id, string what) => sb.Append("INFO ").Append(id).Append(' ').Append(what).Append('\n');

            public override string ToString() => sb.ToString();
        }

        private sealed class ScriptedPrompts : IStageMapPrompts
        {
            public PromptChoice Ask(string title, string message, string primary, string secondary, string cancel) => PromptChoice.Primary;
            public bool Confirm(string title, string message, string ok) => true;
        }

        [MenuItem("Tools/MyGame/Map Tool/Run Self Check")]
        public static void RunMenu()
        {
            string result = Run();
            if (result.Contains("\nFAIL ") || result.StartsWith("FAIL "))
                Debug.LogError("[MapTool] Self check\n" + result);
            else
                Debug.Log("[MapTool] Self check\n" + result);
        }

        /// <summary>Runs every check and returns the report; the last line reads "SELF CHECK: n passed, m failed".</summary>
        internal static string Run()
        {
            var r = new Report();
            var checks = new (string name, Action<Report> run)[]
            {
                ("R1-R3/V1", RoundTripChecks),
                ("W", WriterChecks),
                ("E1", CheckAddAllKinds),
                ("E2", CheckSpecialLimit),
                ("E3", CheckRemoveAddDuplicate),
                ("E4", CheckNextId),
                ("E5", CheckEdgeClamps),
                ("E6", CheckSectionRename),
                ("E7", CheckShiftFrom),
                ("C1", CheckDirtyTracking),
                ("C2", CheckTemplate),
                ("C3", CheckNonFiniteWrite),
                ("D1", CheckDecorationRoundTrip),
                ("D2", CheckDecorationEdits),
                ("D3", CheckThemeField),
                ("D4", CheckLegacyLayoutsUnchanged),
                ("D5", CheckPropTable),
                ("D6", CheckDecorationValidation),
            };
            foreach (var (name, run) in checks)
            {
                try
                {
                    run(r);
                }
                catch (Exception e)
                {
                    r.Fail(name, "exception: " + e);
                }
            }
            string summary = $"SELF CHECK: {r.Passed} passed, {r.Failed} failed";
            return r + summary;
        }

        // ------------------------------------------------------------------ helpers

        private static List<string> LayoutFiles()
        {
            var files = new List<string>();
            string dir = StageMapPaths.ToAbsolute(StageLayoutBuilder.LayoutsDir);
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "*.json"))
                    files.Add(StageLayoutBuilder.LayoutsDir + "/" + Path.GetFileName(f));
            }
            files.Sort(StringComparer.Ordinal);
            string test = StageMapPaths.TestDataDir + "/W1_TEST.json";
            if (File.Exists(StageMapPaths.ToAbsolute(test)))
                files.Add(test);
            return files;
        }

        private static StageLayoutData Load(string assetPath)
        {
            if (!StageLayoutJson.TryParse(StageMapPaths.ReadText(assetPath), out var d, out string error))
                throw new InvalidOperationException($"{assetPath}: {error}");
            StageLayoutJson.Normalize(d);
            return d;
        }

        private static StageLayoutData LoadW1S1() => Load(StageLayoutBuilder.LayoutsDir + "/W1_S1.json");

        private static int ErrorsOf(StageLayoutData d, out List<ValidationIssue> issues)
        {
            issues = StageMapValidation.Run(d, d.stageId + ".json", d.stageId);
            return StageMapValidation.Count(issues, IssueSeverity.Error);
        }

        private static int ErrorsOf(StageLayoutData d) => ErrorsOf(d, out _);

        private static LayoutBlock LargestGround(StageLayoutData d)
        {
            LayoutBlock best = null;
            foreach (var b in d.blocks)
            {
                if (b.kind == "ground" && (best == null || b.w > best.w))
                    best = b;
            }
            return best;
        }

        private static int IdBearingCount(StageLayoutData d) =>
            d.sections.Length + d.blocks.Length + d.checkpoints.Length + d.specials.Length + d.spikes.Length
            + d.movers.Length + d.enemies.Length + d.fallers.Length + d.decorations.Length;

        private static string Canonical(StageLayoutData d) => StageLayoutJson.Write(d);

        // ------------------------------------------------------------------ R1-R3, V1

        private static void RoundTripChecks(Report r)
        {
            foreach (var path in LayoutFiles())
            {
                string name = Path.GetFileName(path);
                string raw = StageMapPaths.ReadText(path);
                if (!StageLayoutJson.TryParse(raw, out var d1, out string error))
                {
                    r.Fail("R1", name + " cannot be parsed: " + error);
                    continue;
                }
                StageLayoutJson.Normalize(d1);
                string w1 = Canonical(d1);

                StageLayoutJson.TryParse(w1, out var d2, out _);
                StageLayoutJson.Normalize(d2);
                string w2 = Canonical(d2);
                r.Check(w1 == w2 && StageLayoutJson.FindMissingTopLevelKeys(w1).Count == 0, "R1", name + " write is deterministic and idempotent (16 top-level keys)");

                r.Check(JsonUtility.ToJson(d1) == JsonUtility.ToJson(d2), "R2", name + " meaning is preserved by the round trip");

                if (raw == w1)
                {
                    r.Info("R3", name + " is byte-identical to the canonical format");
                }
                else
                {
                    var rawLines = new HashSet<string>(raw.Split('\n').Select(l => l.Trim()));
                    int diff = w1.Split('\n').Count(l => !rawLines.Contains(l.Trim()));
                    r.Info("R3", $"{name} differs from the canonical format ({diff} line(s) not present in the original)");
                }

                // V1: validating the original text and the canonical text must give the same messages.
                var e1 = new List<string>(); var w1l = new List<string>();
                var e2 = new List<string>(); var w2l = new List<string>();
                string stem = Path.GetFileNameWithoutExtension(path);
                StageLayoutValidator.Validate(name, raw, d1, stem, e1, w1l);
                StageLayoutValidator.Validate(name, w1, d2, stem, e2, w2l);
                e1.Sort(StringComparer.Ordinal); e2.Sort(StringComparer.Ordinal);
                w1l.Sort(StringComparer.Ordinal); w2l.Sort(StringComparer.Ordinal);
                r.Check(e1.SequenceEqual(e2) && w1l.SequenceEqual(w2l), "V1", $"{name} validation is the same for original and canonical text (errors {e1.Count}, warnings {w1l.Count})");
            }
        }

        // ------------------------------------------------------------------ writer details (W5, W6)

        private static void WriterChecks(Report r)
        {
            r.Check(StageLayoutJson.FormatFloat(2f) == "2.0" && StageLayoutJson.FormatFloat(-8f) == "-8.0" && StageLayoutJson.FormatFloat(1.2f) == "1.2"
                    && StageLayoutJson.FormatFloat(170f) == "170.0" && StageLayoutJson.FormatFloat(0.5f) == "0.5",
                "W5", "numbers use the 0.0### format");
            r.Check(StageLayoutJson.FormatFloat(-0.00001f) == "0.0" && StageLayoutJson.FormatFloat(-0f) == "0.0", "W5", "negative zero is written as 0.0");

            var d = StageLayoutEditOps.CreateTemplate("W9_S9");
            string note = "quote \" backslash \\ newline \n tab \t ctrl \u0001 한글";
            d.sections = new[] { new LayoutSection { id = "W9_S9_A", startX = 0f, endX = 10f, note = note } };
            string json = Canonical(d);
            r.Check(StageLayoutJson.TryParse(json, out var back, out _) && back.sections[0].note == note, "W6", "string escaping round-trips (quote, backslash, control, Korean)");
            r.Check(!json.EndsWith("\n") && !json.Contains("\r") && json.StartsWith("{\n \"version\": 1,\n"), "W1", "LF only, no trailing newline, one-space indent");
        }

        // ------------------------------------------------------------------ E1..E7

        private static void CheckAddAllKinds(Report r)
        {
            var d = LoadW1S1();
            int baseErrors = ErrorsOf(d);
            var ground = LargestGround(d);
            if (ground == null || ground.w < 14f)
            {
                r.Fail("E1", "W1_S1 has no ground block of width >= 14 to place on");
                return;
            }
            float cx = ground.x + 2f;
            float top = ground.y;
            float high = LayoutGeometry.HighestPoint(d) + 6f;

            var tools = new[]
            {
                MapPaletteTool.Checkpoint, MapPaletteTool.Spike, MapPaletteTool.Enemy, MapPaletteTool.Coin, MapPaletteTool.Faller,
                MapPaletteTool.Mover, MapPaletteTool.Platform, MapPaletteTool.Wall, MapPaletteTool.Section,
            };
            bool allOk = true;
            foreach (var tool in tools)
            {
                var kind = ToKind(tool);
                Vector2 raw = new Vector2(cx, top + 0.2f);
                if (tool == MapPaletteTool.Mover) raw = new Vector2(cx, high);
                if (tool == MapPaletteTool.Platform) raw = new Vector2(cx, high + 2f);
                if (tool == MapPaletteTool.Wall) raw = new Vector2(cx + 6f, high + 4f);
                if (tool == MapPaletteTool.Coin) raw = new Vector2(cx, top + 3f);
                if (tool == MapPaletteTool.Faller) raw = new Vector2(cx, top + 4f);
                Vector2 p = StageLayoutEditOps.SnapPlacement(d, kind, raw, 0.5f, true, true, tool == MapPaletteTool.Spike ? 1f : 0f);
                bool ok = StageLayoutEditOps.TryAdd(d, tool, p, out var created, out string message);
                if (!ok || !created.IsValid(d))
                {
                    r.Fail("E1", $"adding {tool} failed: {message}");
                    allOk = false;
                }
            }
            if (!allOk)
                return;
            r.Check(StageLayoutEditOps.AllIds(d).Count == IdBearingCount(d), "E1", "every added element got a unique id");
            int errors = ErrorsOf(d, out var issues);
            string firstError = issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).FirstOrDefault();
            r.Check(errors <= baseErrors, "E1", $"adding one of each kind on surfaces adds no validation errors ({baseErrors} -> {errors})", firstError);
        }

        private static LayoutElementKind ToKind(MapPaletteTool tool)
        {
            switch (tool)
            {
                case MapPaletteTool.Ground: case MapPaletteTool.Platform: case MapPaletteTool.Wall: return LayoutElementKind.Block;
                case MapPaletteTool.Checkpoint: return LayoutElementKind.Checkpoint;
                case MapPaletteTool.Coin: return LayoutElementKind.Coin;
                case MapPaletteTool.Special: return LayoutElementKind.Special;
                case MapPaletteTool.Spike: return LayoutElementKind.Spike;
                case MapPaletteTool.Mover: return LayoutElementKind.Mover;
                case MapPaletteTool.Enemy: return LayoutElementKind.Enemy;
                case MapPaletteTool.Faller: return LayoutElementKind.Faller;
                default: return LayoutElementKind.Section;
            }
        }

        private static void CheckSpecialLimit(Report r)
        {
            var d = LoadW1S1();
            string before = Canonical(d);
            r.Check(d.specials.Length == StageSession.SpecialSlotCount, "E2", "W1_S1 uses all special slots (precondition)");
            bool added = StageLayoutEditOps.TryAdd(d, MapPaletteTool.Special, new Vector2(5f, 3f), out _, out string message);
            r.Check(!added && !string.IsNullOrEmpty(message) && Canonical(d) == before, "E2", "4th special is refused and the data is unchanged");
            bool dup = StageLayoutEditOps.Duplicate(d, new LayoutElementRef(LayoutElementKind.Special, 0), 0.5f, out _, out string dupMessage);
            r.Check(!dup && !string.IsNullOrEmpty(dupMessage) && Canonical(d) == before, "E2", "duplicating a special without a free slot is refused");
        }

        private static void CheckRemoveAddDuplicate(Report r)
        {
            var d = StageLayoutEditOps.CreateTemplate("W9_S9");
            for (int i = 0; i < 3; i++)
            {
                var p = StageLayoutEditOps.SnapPlacement(d, LayoutElementKind.Enemy, new Vector2(10f + i * 10f, 0.2f), 0.5f, true, true);
                StageLayoutEditOps.TryAdd(d, MapPaletteTool.Enemy, p, out _, out _);
            }
            var ids = d.enemies.Select(e => e.id).ToList();
            r.Check(ids.SequenceEqual(new[] { "W9_S9_en1", "W9_S9_en2", "W9_S9_en3" }), "E3", "enemy ids follow {stage}_en{n}", string.Join(",", ids));

            StageLayoutEditOps.Remove(d, new LayoutElementRef(LayoutElementKind.Enemy, 1));
            r.Check(d.enemies.Select(e => e.id).SequenceEqual(new[] { "W9_S9_en1", "W9_S9_en3" }), "E3", "Remove shortens the array and keeps the order of the rest");

            var p2 = StageLayoutEditOps.SnapPlacement(d, LayoutElementKind.Enemy, new Vector2(25f, 0.2f), 0.5f, true, true);
            StageLayoutEditOps.TryAdd(d, MapPaletteTool.Enemy, p2, out var created, out _);
            r.Check(d.enemies.Length == 3 && created.index == 2 && d.enemies[2].id == "W9_S9_en2", "E3", "Add appends at the end and reuses the smallest free number");

            int before = d.enemies.Length;
            float x0 = d.enemies[0].x;
            bool ok = StageLayoutEditOps.Duplicate(d, new LayoutElementRef(LayoutElementKind.Enemy, 0), 0.5f, out var copy, out _);
            r.Check(ok && d.enemies.Length == before + 1 && copy.index == before && d.enemies[copy.index].id != d.enemies[0].id
                    && Mathf.Approximately(d.enemies[copy.index].x, x0 + 0.5f), "E3", "Duplicate appends a copy one grid step to the right with a fresh id");
            r.Check(!StageLayoutEditOps.Duplicate(d, new LayoutElementRef(LayoutElementKind.Spawn, 0), 0.5f, out _, out _), "E3", "spawn cannot be duplicated");
        }

        private static void CheckNextId(Report r)
        {
            var d = StageLayoutEditOps.CreateTemplate("W1_S1");
            d.sections = new[] { new LayoutSection { id = "W1_S1_D", startX = 0f, endX = 60f, note = "" } };
            for (int i = 1; i <= 4; i++)
                StageLayoutEditOps.TryAdd(d, MapPaletteTool.Platform, new Vector2(i * 8f, 10f), out _, out _);
            r.Check(d.blocks.Count(b => b.id.StartsWith("W1_S1_D_platform")) == 4, "E4", "platforms in section D are named W1_S1_D_platform1..4");
            string next = StageLayoutEditOps.NextId(d, LayoutElementKind.Block, "platform", 20f);
            r.Check(next == "W1_S1_D_platform5", "E4", "NextId continues with platform5", next);
            r.Check(StageLayoutEditOps.NextId(d, LayoutElementKind.Checkpoint, null, 20f) == "W1_S1_CP1", "E4", "checkpoints use the stageId prefix");
            r.Check(StageLayoutEditOps.NextSectionId(d) == "W1_S1_E", "E4", "next section id is the letter after the highest one");
        }

        private static void CheckEdgeClamps(Report r)
        {
            var d = StageLayoutEditOps.CreateTemplate("W9_S9");
            StageLayoutEditOps.TryAdd(d, MapPaletteTool.Platform, new Vector2(10f, 4f), out var block, out _);
            StageLayoutEditOps.TryAdd(d, MapPaletteTool.Spike, new Vector2(20f, 0f), out var spike, out _);
            StageLayoutEditOps.TryAdd(d, MapPaletteTool.Mover, new Vector2(30f, 3f), out var mover, out _);
            StageLayoutEditOps.TryAdd(d, MapPaletteTool.Enemy, new Vector2(40f, 0f), out var enemy, out _);
            StageLayoutEditOps.TryAdd(d, MapPaletteTool.Faller, new Vector2(50f, 4f), out var faller, out _);
            StageLayoutEditOps.TryAdd(d, MapPaletteTool.Section, new Vector2(5f, 0f), out var section, out _);
            const float grid = 0.5f;

            StageLayoutEditOps.SetEdge(d, block, LayoutHandle.Right, 0f, grid);
            r.Check(d.blocks[block.index].w >= grid, "E5", "block w is clamped to the grid size");
            StageLayoutEditOps.SetEdge(d, block, LayoutHandle.Left, 99f, grid);
            r.Check(d.blocks[block.index].w >= grid, "E5", "dragging the left edge past the right edge keeps w >= grid");
            StageLayoutEditOps.SetEdge(d, block, LayoutHandle.Top, -50f, grid);
            r.Check(d.blocks[block.index].h >= grid, "E5", "block h is clamped (top edge below the bottom)");
            StageLayoutEditOps.SetEdge(d, block, LayoutHandle.Bottom, 99f, grid);
            r.Check(d.blocks[block.index].h >= grid, "E5", "block h is clamped (bottom edge above the top)");
            StageLayoutEditOps.SetEdge(d, spike, LayoutHandle.Right, 0f, grid);
            r.Check(d.spikes[spike.index].w >= StageLayoutEditOps.MinSpikeWidth, "E5", "spike w >= 0.5");
            StageLayoutEditOps.SetEdge(d, mover, LayoutHandle.Right, 0f, grid);
            r.Check(d.movers[mover.index].w >= StageLayoutEditOps.MinMoverWidth, "E5", "mover w >= 0.5");
            StageLayoutEditOps.SetEdge(d, mover, LayoutHandle.MoverEnd, d.movers[mover.index].x, grid);
            r.Check(Mathf.Abs(d.movers[mover.index].distance) >= StageLayoutEditOps.MinDistance, "E5", "mover |distance| >= 0.1");
            StageLayoutEditOps.SetEdge(d, enemy, LayoutHandle.PatrolEnd, 0f, grid);
            r.Check(d.enemies[enemy.index].range >= StageLayoutEditOps.MinEnemyRange, "E5", "enemy range >= 0.5");
            StageLayoutEditOps.SetEdge(d, faller, LayoutHandle.TriggerWidth, d.fallers[faller.index].x, grid);
            r.Check(d.fallers[faller.index].triggerHalfWidth >= StageLayoutEditOps.MinTriggerHalfWidth, "E5", "faller triggerHalfWidth >= 0.1");
            StageLayoutEditOps.SetEdge(d, section, LayoutHandle.Right, -10f, grid);
            r.Check(d.sections[section.index].endX >= d.sections[section.index].startX, "E5", "section endX >= startX");

            // The handle semantics of design 7.4: enemy end point = x + range.
            StageLayoutEditOps.SetEdge(d, enemy, LayoutHandle.PatrolEnd, 47f, grid);
            r.Check(Mathf.Approximately(d.enemies[enemy.index].range, 47f - d.enemies[enemy.index].x), "E5", "enemy end point maps to range = endX - x");
        }

        private static void CheckSectionRename(Report r)
        {
            var d = LoadW1S1();
            int baseErrors = ErrorsOf(d);
            if (d.sections.Length == 0)
            {
                r.Fail("E6", "W1_S1 has no sections");
                return;
            }
            string old = d.sections[0].id;
            int refs = StageLayoutEditOps.CountSectionRefs(d, old);
            bool renamed = StageLayoutEditOps.RenameSection(d, 0, "W1_S1_RENAMED");
            r.Check(renamed && StageLayoutEditOps.CountSectionRefs(d, old) == 0 && StageLayoutEditOps.CountSectionRefs(d, "W1_S1_RENAMED") == refs,
                "E6", $"renaming a section updates its {refs} reference(s)");
            r.Check(ErrorsOf(d) <= baseErrors, "E6", "no validation errors added by the rename");
            r.Check(!StageLayoutEditOps.RenameSection(d, 0, d.blocks[0].id), "E6", "renaming to an id that is already used is refused");

            StageLayoutEditOps.RemoveSection(d, 0, true);
            ErrorsOf(d, out var issues);
            r.Check(StageLayoutEditOps.CountSectionRefs(d, "W1_S1_RENAMED") == 0 && !issues.Any(i => i.Message.Contains("unknown section")),
                "E6", "RemoveSection(clearRefs: true) leaves no 'unknown section' error");
        }

        private static void CheckShiftFrom(Report r)
        {
            var d = LoadW1S1();
            string original = Canonical(d);
            int baseErrors = ErrorsOf(d);
            bool shifted = StageLayoutEditOps.ShiftFrom(d, 40f, 4f, out string message);
            int mid = ErrorsOf(d, out var issues);
            r.Check(shifted && mid <= baseErrors, "E7", $"ShiftFrom(40, +4) adds no validation errors ({baseErrors} -> {mid})",
                message ?? issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).FirstOrDefault());
            bool back = StageLayoutEditOps.ShiftFrom(d, 44f, -4f, out message);
            r.Check(back && Canonical(d) == original, "E7", "ShiftFrom(44, -4) restores the original layout", message);

            // A removal that would delete elements is refused and leaves the data alone.
            var d2 = LoadW1S1();
            string before = Canonical(d2);
            bool refused = !StageLayoutEditOps.ShiftFrom(d2, 40f, -30f, out string why);
            r.Check(refused && Canonical(d2) == before && !string.IsNullOrEmpty(why), "E7", "a negative shift over existing elements is refused");
        }

        // ------------------------------------------------------------------ C1..C3

        private static void CheckDirtyTracking(Report r)
        {
            if (StageMapController.IsLocked)
            {
                r.Info("C1", "skipped: Play Mode or compilation is active");
                return;
            }
            var doc = StageLayoutDocument.CreateTransient(null);
            var controller = new StageMapController(new StageMapSession(), doc, new ScriptedPrompts());
            try
            {
                controller.AttachData(StageLayoutEditOps.CreateTemplate("W9_S9"), "Assets/__SelfCheck__/W9_S9.json");
                r.Check(!controller.IsDirty, "C1", "an attached document starts clean");
                controller.ApplyEdit("selfcheck", d => { d.length += 1f; return true; });
                r.Check(controller.IsDirty, "C1", "an edit makes the document dirty");
                controller.ApplyEdit("selfcheck", d => { d.length -= 1f; return true; });
                r.Check(!controller.IsDirty, "C1", "restoring the same value clears the dirty state");

                controller.BeginDrag("selfcheck drag");
                controller.ApplyEdit("selfcheck", d => { d.goal.x += 1f; return true; });
                r.Check(controller.IsDirty, "C1", "a drag marks the document dirty immediately");
                controller.ApplyEdit("selfcheck", d => { d.goal.x -= 1f; return true; });
                controller.EndDrag();
                r.Check(!controller.IsDirty, "C1", "ending a drag at the original value recomputes the dirty state exactly");
            }
            finally
            {
                Undo.ClearUndo(doc);
                controller.Dispose();
                UnityEngine.Object.DestroyImmediate(doc);
            }
        }

        private static void CheckTemplate(Report r)
        {
            var d = StageLayoutEditOps.CreateTemplate("W9_S9");
            int errors = ErrorsOf(d, out var issues);
            r.Check(errors == 0, "C2", "the new-layout template validates without errors",
                issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).FirstOrDefault());
            bool shape = d.length == 60f && d.killY == -8f && d.cameraMinY == 2.5f && d.blocks.Length == 1 && d.specials.Length == 3
                         && d.blocks[0].w == 60f && d.blocks[0].h == 6f && d.spawn.x == 2f && d.goal.x == 56f;
            r.Check(shape, "C2", "the template has the MT-002 values");
        }

        private static void CheckNonFiniteWrite(Report r)
        {
            var d = StageLayoutEditOps.CreateTemplate("W9_S9");
            d.length = float.NaN;
            bool threw = false;
            try
            {
                Canonical(d);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }
            r.Check(threw, "C3", "writing NaN throws InvalidOperationException (save failure path)");
        }

        // ------------------------------------------------------------------ D1..D6: decorations, theme, prop table (contract v1.3)

        private const string PropHeader = "propId,name,category,prefabPath,defaultZ,collision,status,description";

        private static void CheckDecorationRoundTrip(Report r)
        {
            var d = StageLayoutEditOps.CreateTemplate("W9_S9");
            d.theme = "GreenMeadow";
            d.decorations = new[]
            {
                new LayoutDecoration { id = "W9_S9_dec1", prop = "V01_BroadleafOak", x = 12.5f, y = 0f, z = 8f, rotY = 15f, scale = 1.25f },
                new LayoutDecoration { id = "W9_S9_dec2", prop = "B01_FarHill", x = 40f, y = -2f, rotY = 0f, scale = 1f }, // z not given
            };
            string w1 = Canonical(d);
            bool parsed = StageLayoutJson.TryParse(w1, out var back, out string error);
            r.Check(parsed, "D1", "a layout with decorations parses back", error);
            if (!parsed)
                return;
            StageLayoutJson.Normalize(back);
            r.Check(Canonical(back) == w1, "D1", "decorations + theme survive write -> parse -> write byte-identically");
            r.Check(back.decorations.Length == 2 && back.decorations[0].prop == "V01_BroadleafOak" && back.decorations[0].x == 12.5f
                    && back.decorations[0].z == 8f && back.decorations[0].rotY == 15f && back.decorations[0].scale == 1.25f && back.theme == "GreenMeadow",
                "D1", "decoration fields and theme round-trip");
            r.Check(!back.decorations[1].HasZ && w1.IndexOf("\"z\"", StringComparison.Ordinal) == w1.LastIndexOf("\"z\"", StringComparison.Ordinal),
                "D1", "an unset z stays unset and is not written (the prop table's defaultZ applies)");
            r.Check(StageLayoutJson.FindMissingTopLevelKeys(w1).Count == 0 && StageLayoutJson.FindUnknownKeys(w1).Count == 0,
                "D1", "theme / decorations / prop / z / rotY / scale are known keys (no 'unknown field' report)");

            // Missing array: normal, no correction reported, nothing written.
            var plain = StageLayoutEditOps.CreateTemplate("W9_S9");
            string plainJson = Canonical(plain);
            StageLayoutJson.TryParse(plainJson, out var plainBack, out _);
            r.Check(!plainJson.Contains("\"decorations\"") && !plainJson.Contains("\"theme\"") && StageLayoutJson.Normalize(plainBack).Count == 0
                    && plainBack.decorations.Length == 0, "D1", "a layout without the optional fields loads as empty without corrections and is written without them");

            // Information only: do JsonUtility field initializers apply to array elements (omitted scale -> 1)?
            string noScale = w1.Replace(",\n   \"scale\": 1.25\n", "\n");
            if (noScale != w1 && StageLayoutJson.TryParse(noScale, out var omitted, out _) && omitted.decorations.Length > 0)
            {
                r.Info("D1", omitted.decorations[0].scale == 1f
                    ? "omitted scale reads as 1 (field initializers apply to array elements)"
                    : $"omitted scale reads as {omitted.decorations[0].scale} (initializers NOT applied): files must write scale explicitly; the validator reports scale <= 0");
            }
        }

        private static void CheckDecorationEdits(Report r)
        {
            var d = StageLayoutEditOps.CreateTemplate("W9_S9");
            d.sections = new[] { new LayoutSection { id = "W9_S9_A", startX = 0f, endX = 60f, note = "" } };
            for (int i = 0; i < 3; i++)
                StageLayoutEditOps.TryAddDecoration(d, "V01_BroadleafOak", 8f, new Vector2(10f + i * 5f, 10f), out _);
            r.Check(d.decorations.Select(e => e.id).SequenceEqual(new[] { "W9_S9_dec1", "W9_S9_dec2", "W9_S9_dec3" }),
                "D2", "decoration ids are {stageId}_dec{n} (not section-prefixed)", string.Join(",", d.decorations.Select(e => e.id)));
            r.Check(d.decorations.All(e => e.scale == 1f && e.rotY == 0f && e.z == 8f), "D2", "a new decoration has scale 1, rotY 0 and the given depth");

            StageLayoutEditOps.Remove(d, new LayoutElementRef(LayoutElementKind.Decoration, 1));
            r.Check(d.decorations.Select(e => e.id).SequenceEqual(new[] { "W9_S9_dec1", "W9_S9_dec3" }), "D2", "Remove keeps the order of the rest");
            StageLayoutEditOps.TryAddDecoration(d, "P01_WoodFence", 6f, new Vector2(30f, 10f), out var added);
            r.Check(d.decorations[added.index].id == "W9_S9_dec2", "D2", "Add reuses the smallest free number");

            bool dup = StageLayoutEditOps.Duplicate(d, new LayoutElementRef(LayoutElementKind.Decoration, 0), 0.5f, out var copy, out _);
            r.Check(dup && copy.IsValid(d) && d.decorations[copy.index].id != d.decorations[0].id && d.decorations[copy.index].prop == d.decorations[0].prop
                    && d.decorations[copy.index].z == d.decorations[0].z && Mathf.Approximately(d.decorations[copy.index].x, d.decorations[0].x + 0.5f),
                "D2", "Duplicate appends a copy one grid step to the right with a fresh id and the same prop and depth");
            r.Check(StageLayoutEditOps.AllIds(d).Count == IdBearingCount(d), "D2", "every decoration id is part of AllIds (unique across kinds)");

            float z = d.decorations[0].z;
            StageLayoutEditOps.SetPosition(d, new LayoutElementRef(LayoutElementKind.Decoration, 0), new Vector2(11.234f, 9.5f));
            Vector2 pos = StageLayoutEditOps.GetPosition(d, new LayoutElementRef(LayoutElementKind.Decoration, 0));
            r.Check(Mathf.Approximately(pos.x, 11.23f) && Mathf.Approximately(pos.y, 9.5f) && d.decorations[0].z == z, "D2", "moving changes x / y only (rounded to 0.01 m), never z");

            var picked = LayoutGeometry.Pick(d, new Vector2(pos.x, pos.y + 0.3f), 0.1f);
            r.Check(picked.kind == LayoutElementKind.Decoration && picked.index == 0, "D2", "the pick marker above the pivot selects the decoration");

            bool renamed = StageLayoutEditOps.RenameId(d, new LayoutElementRef(LayoutElementKind.Decoration, 0), "W9_S9_tree");
            r.Check(renamed && !StageLayoutEditOps.RenameId(d, new LayoutElementRef(LayoutElementKind.Decoration, 1), "W9_S9_tree"), "D2", "rename works and a duplicate id is refused");

            float before = d.decorations[1].x;
            bool shifted = StageLayoutEditOps.ShiftFrom(d, 0f, 2f, out _);
            r.Check(shifted && Mathf.Approximately(d.decorations[1].x, before + 2f), "D2", "ShiftFrom moves decorations with the rest of the course");
        }

        private static void CheckThemeField(Report r)
        {
            var d = StageLayoutEditOps.CreateTemplate("W9_S9");
            d.theme = "GreenMeadow";
            string json = Canonical(d);
            StageLayoutJson.TryParse(json, out var back, out _);
            r.Check(json.Contains("\"theme\": \"GreenMeadow\"") && back.theme == "GreenMeadow" && json.IndexOf("\"theme\"", StringComparison.Ordinal) > json.IndexOf("\"fallers\"", StringComparison.Ordinal),
                "D3", "theme round-trips and is written after the 16 required keys");
            d.theme = "";
            r.Check(!Canonical(d).Contains("\"theme\""), "D3", "an empty theme is not written");

            d.theme = "NoSuchTheme_selfcheck";
            int errors = ErrorsOf(d, out var issues);
            r.Check(errors == 0 && issues.Any(i => i.Severity == IssueSeverity.Warning && i.Message.Contains("not found")), "D3", "an unknown theme kit is a warning (primitives are used), not an error");
            d.theme = "bad/name";
            r.Check(ErrorsOf(d) > 0, "D3", "a theme name with a path separator is an error");
        }

        private static void CheckLegacyLayoutsUnchanged(Report r)
        {
            foreach (var path in LayoutFiles())
            {
                string name = Path.GetFileName(path);
                var d = Load(path);
                string canonical = Canonical(d);
                // The writer emits the optional keys only when the layout uses them (files without them stay unchanged).
                bool keysMatchData = string.IsNullOrEmpty(d.theme) != canonical.Contains("\"theme\"")
                                     && (d.decorations.Length == 0) != canonical.Contains("\"decorations\"");
                r.Check(keysMatchData, "D4", name + " writes theme / decorations only when the layout uses them"
                    + $" (theme '{d.theme}', decorations {d.decorations.Length})");
                r.Check(ErrorsOf(d) == 0, "D4", name + " still validates without errors");
                string raw = StageMapPaths.ReadText(path);
                r.Info("D4", name + (raw == canonical ? " is byte-identical to the canonical output" : " differs from the canonical output only as before (see R3)"));
            }
        }

        private static List<string> ParseProps(string csv, out PropTable table)
        {
            var errors = new List<string>();
            table = PropTable.Parse(csv, errors);
            return errors;
        }

        private static void CheckPropTable(Report r)
        {
            string row = "A_One,\"Tree, big\",Vegetation,Assets/x/A_One.prefab,8,false,Implemented,\"say \"\"hi\"\"\"";
            string row2 = "B_Two,Two,Background,Assets/x/B_Two.prefab,-1.5,true,Planned,";
            var errors = ParseProps("﻿" + PropHeader + "\r\n" + row + "\r\n" + row2 + "\r\n", out var table);
            r.Check(errors.Count == 0 && table.Records.Count == 2, "D5", "a table with BOM, CRLF, quoted commas and doubled quotes parses", string.Join("; ", errors));
            if (table.Records.Count == 2)
            {
                var a = table.Records[0];
                var b = table.Records[1];
                r.Check(a.name == "Tree, big" && a.description == "say \"hi\"" && a.defaultZ == 8f && !a.collision && a.IsImplemented
                        && b.defaultZ == -1.5f && b.collision && !b.IsImplemented && table.TryGet("B_Two", out _) && !table.TryGet("missing", out _),
                    "D5", "fields are read as given (InvariantCulture, lowercase booleans)");
            }

            foreach (var bad in new[] { "NaN", "Infinity", "-Infinity", "1e999" })
            {
                errors = ParseProps(PropHeader + "\n" + row.Replace(",8,false", "," + bad + ",false") + "\n", out table);
                r.Check(errors.Count > 0 && table.Records.Count == 0, "D5", $"defaultZ '{bad}' is rejected (non-finite)");
            }
            errors = ParseProps(PropHeader + "\n" + row + "\n" + row + "\n", out table);
            r.Check(errors.Any(e => e.Contains("duplicate propId")) && table.Records.Count == 1, "D5", "a duplicate propId is rejected");
            errors = ParseProps(PropHeader.Replace("collision", "collide") + "\n" + row + "\n", out _);
            r.Check(errors.Count > 0, "D5", "a wrong header is rejected");
            errors = ParseProps(PropHeader + "\n" + row.Replace("false", "False") + "\n", out _);
            r.Check(errors.Count > 0, "D5", "a boolean must be lowercase true / false");
            errors = ParseProps(PropHeader + "\n" + row.Replace("Vegetation", "Trees") + "\n", out _);
            r.Check(errors.Count > 0, "D5", "an unknown category is rejected");
            errors = ParseProps(PropHeader + "\n" + row.Replace("Implemented", "Done") + "\n", out _);
            r.Check(errors.Count > 0, "D5", "an unknown status is rejected");
            errors = ParseProps(PropHeader + "\n" + row.Replace("A_One", "A One") + "\n", out _);
            r.Check(errors.Count > 0, "D5", "a propId with a space is rejected");
            errors = ParseProps(PropHeader + "\n" + row.Replace("Assets/x/A_One.prefab", "x/A_One.fbx") + "\n", out _);
            r.Check(errors.Count > 0, "D5", "a prefabPath outside Assets/ or not .prefab is rejected");

            var shippedErrors = new List<string>();
            var shipped = PropCatalog.Load(shippedErrors);
            r.Check(shipped != null && shippedErrors.Count == 0 && shipped.Records.Count > 0, "D5",
                $"the shipped {PropCatalog.TablePath} parses without errors", shipped == null ? string.Join("; ", shippedErrors) : $"{shipped.Records.Count} rows; " + string.Join("; ", shippedErrors));
            if (shipped != null)
                r.Info("D5", $"{shipped.Records.Count(p => p.IsImplemented)} of {shipped.Records.Count} props are Implemented");
        }

        private static void CheckDecorationValidation(Report r)
        {
            string row(string id, string category, float z, bool collision, string status) =>
                $"{id},{id},{category},Assets/_SelfCheck/{id}.prefab,{z.ToString(System.Globalization.CultureInfo.InvariantCulture)},{(collision ? "true" : "false")},{status},";
            string csv = PropHeader + "\n" + row("ok_prop", "Vegetation", 8f, false, "Implemented") + "\n" + row("plan_prop", "Vegetation", 8f, false, "Planned") + "\n"
                         + row("col_prop", "Vegetation", 8f, true, "Implemented") + "\n" + row("bg_prop", "Background", 0f, false, "Implemented") + "\n";
            var parseErrors = ParseProps(csv, out var table);
            if (parseErrors.Count > 0)
            {
                r.Fail("D6", "test table did not parse: " + string.Join("; ", parseErrors));
                return;
            }

            StageLayoutData Make(params LayoutDecoration[] decs)
            {
                var d = StageLayoutEditOps.CreateTemplate("W9_S9");
                d.decorations = decs;
                return d;
            }
            LayoutDecoration Dec(string id, string prop) => new LayoutDecoration { id = id, prop = prop, x = 10f, y = 10f, scale = 1f };
            bool Has(List<ValidationIssue> issues, IssueSeverity severity, string text) =>
                issues.Any(i => i.Severity == severity && i.Message.Contains(text));

            StageLayoutValidator.PropTableOverride = table;
            try
            {
                int e = ErrorsOf(Make(Dec("W9_S9_dec1", "ok_prop")), out var issues);
                r.Check(e == 0 && !issues.Any(i => i.Message.Contains("decoration")), "D6", "a decoration of an Implemented prop validates cleanly", issues.Select(i => i.Message).FirstOrDefault());

                ErrorsOf(Make(Dec("W9_S9_dec1", "no_such_prop")), out issues);
                r.Check(Has(issues, IssueSeverity.Error, "is not a row"), "D6", "an unknown prop is an error");
                ErrorsOf(Make(Dec("W9_S9_dec1", "plan_prop")), out issues);
                r.Check(Has(issues, IssueSeverity.Error, "Planned"), "D6", "a Planned prop is an error");
                ErrorsOf(Make(Dec("W9_S9_dec1", "")), out issues);
                r.Check(Has(issues, IssueSeverity.Error, "prop is empty"), "D6", "an empty prop is an error");
                ErrorsOf(Make(Dec("W9_S9_dec1", "ok_prop"), Dec("W9_S9_dec1", "ok_prop")), out issues);
                r.Check(Has(issues, IssueSeverity.Error, "duplicate id"), "D6", "a duplicate decoration id is an error");

                var zero = Dec("W9_S9_dec1", "ok_prop");
                zero.scale = 0f;
                ErrorsOf(Make(zero), out issues);
                r.Check(Has(issues, IssueSeverity.Error, "scale must be > 0"), "D6", "scale 0 is an error");
                zero.scale = -2f;
                ErrorsOf(Make(zero), out issues);
                r.Check(Has(issues, IssueSeverity.Error, "scale must be > 0"), "D6", "a negative scale is an error");

                var nan = Dec("W9_S9_dec1", "ok_prop");
                nan.x = float.NaN;
                nan.scale = float.PositiveInfinity;
                var errors = new List<string>();
                StageLayoutValidator.Validate("W9_S9.json", Canonical(Make()), Make(nan), "W9_S9", errors, new List<string>());
                r.Check(errors.Count(m => m.Contains("not a finite number")) >= 2, "D6", "non-finite x / scale are errors (validator, independent of the writer)");
                r.Check(ErrorsOf(Make(nan)) > 0, "D6", "a non-finite value blocks validation in the map tool (serialization failure is an error)");

                ErrorsOf(Make(Dec("W9_S9_dec1", "col_prop")), out issues);
                r.Check(Has(issues, IssueSeverity.Warning, "outside the play lane") && !issues.Any(i => i.Severity == IssueSeverity.Error), "D6", "a collision prop at z outside -2..2 is a warning");
                var inLane = Dec("W9_S9_dec1", "col_prop");
                inLane.z = 1f;
                ErrorsOf(Make(inLane), out issues);
                r.Check(!Has(issues, IssueSeverity.Warning, "play lane"), "D6", "a collision prop inside the play lane has no warning");
                ErrorsOf(Make(Dec("W9_S9_dec1", "bg_prop")), out issues);
                r.Check(Has(issues, IssueSeverity.Warning, "inside the play lane") && !issues.Any(i => i.Severity == IssueSeverity.Error), "D6", "a Background prop inside the play lane is a warning");
                var far = Dec("W9_S9_dec1", "bg_prop");
                far.z = 70f;
                ErrorsOf(Make(far), out issues);
                r.Check(!Has(issues, IssueSeverity.Warning, "play lane"), "D6", "a Background prop at z 70 has no warning");
            }
            finally
            {
                StageLayoutValidator.PropTableOverride = null;
            }
        }
    }
}
