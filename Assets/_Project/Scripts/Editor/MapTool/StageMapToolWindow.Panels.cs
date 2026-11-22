using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    // Window panels (design 7.1): file bar, stage, palette and options, element list, validation, build, status line.
    internal sealed partial class StageMapToolWindow
    {
        private static readonly float[] GridChoices = { 0.25f, 0.5f, 1f };
        private static readonly string[] GridLabels = { "0.25 m", "0.5 m", "1 m" };

        private bool showNewPanel;
        private string newStageId = "W1_S9";
        private string newFolder = StageLayoutBuilder.LayoutsDir;
        private bool showLoadReport = true;
        private bool showBuildReport;
        private string search = "";
        private Vector2 listScroll;
        private Vector2 issueScroll;
        private GUIStyle rowStyle;

        private readonly HashSet<LayoutElementKind> openGroups = new HashSet<LayoutElementKind>
        {
            LayoutElementKind.Spawn, LayoutElementKind.Section, LayoutElementKind.Block, LayoutElementKind.Checkpoint, LayoutElementKind.Enemy,
        };

        // ------------------------------------------------------------------ file bar

        private void DrawFileBar(bool locked)
        {
            bool hasDoc = controller.HasDocument;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(locked))
                {
                    if (GUILayout.Button("새로 만들기", EditorStyles.toolbarButton))
                        showNewPanel = !showNewPanel;
                    if (GUILayout.Button("열기 ▾", EditorStyles.toolbarButton))
                        ShowOpenMenu();
                    using (new EditorGUI.DisabledScope(!hasDoc))
                    {
                        if (GUILayout.Button("저장", EditorStyles.toolbarButton))
                            Defer(() => controller.Save());
                        if (GUILayout.Button("다른 이름", EditorStyles.toolbarButton))
                            Defer(SaveAsWithPicker);
                        if (GUILayout.Button("다시 읽기", EditorStyles.toolbarButton))
                            Defer(() => controller.Reload());
                    }
                }
                GUILayout.FlexibleSpace();
                if (hasDoc)
                    GUILayout.Label(controller.FileName + (controller.IsDirty ? " *" : ""), EditorStyles.boldLabel);
            }

            if (showNewPanel)
                DrawNewPanel();

            if (!hasDoc)
            {
                if (!string.IsNullOrEmpty(controller.LastLoadErrorPath) && GUILayout.Button("텍스트 편집기로 열기: " + controller.LastLoadErrorPath))
                    EditorUtility.OpenWithDefaultApp(StageMapPaths.ToAbsolute(controller.LastLoadErrorPath));
                return;
            }

            EditorGUILayout.LabelField("경로", controller.FilePath, EditorStyles.miniLabel);
            if (controller.ExternalChangePending)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label("디스크 파일이 바뀌었습니다.", EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("다시 읽기", GUILayout.Width(70f)))
                        Defer(() => controller.ReloadFromDisk());
                    if (GUILayout.Button("무시", GUILayout.Width(50f)))
                        Defer(() => controller.DismissExternalChange());
                }
            }
            if (session.loadReport.Count > 0)
            {
                showLoadReport = EditorGUILayout.Foldout(showLoadReport, $"로드 보고 ({session.loadReport.Count})", true);
                if (showLoadReport)
                {
                    foreach (var line in session.loadReport)
                    {
                        var type = line.StartsWith("ERROR", StringComparison.Ordinal) ? MessageType.Error
                            : line.StartsWith("WARN", StringComparison.Ordinal) ? MessageType.Warning : MessageType.Info;
                        EditorGUILayout.HelpBox(line, type);
                    }
                }
            }
        }

        private void DrawNewPanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                newStageId = EditorGUILayout.TextField("stageId", newStageId);
                using (new EditorGUILayout.HorizontalScope())
                {
                    newFolder = EditorGUILayout.TextField("저장 폴더", newFolder);
                    if (GUILayout.Button("선택...", GUILayout.Width(60f)))
                    {
                        string picked = EditorUtility.OpenFolderPanel("저장 폴더 선택", StageMapPaths.ToAbsolute(newFolder), "");
                        string rel = StageMapPaths.ToAssetPath(picked);
                        if (rel != null)
                            newFolder = rel;
                    }
                }
                EditorGUILayout.LabelField("파일", newFolder.TrimEnd('/') + "/" + newStageId + ".json", EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("만들기"))
                    {
                        string id = newStageId;
                        string folder = newFolder;
                        showNewPanel = false;
                        Defer(() => controller.New(id, folder));
                    }
                    if (GUILayout.Button("취소"))
                        showNewPanel = false;
                }
            }
        }

        private void ShowOpenMenu()
        {
            var menu = new GenericMenu();
            string current = controller.FilePath;
            void AddFiles(string dir, string prefix)
            {
                string abs = StageMapPaths.ToAbsolute(dir);
                if (!Directory.Exists(abs))
                    return;
                var files = Directory.GetFiles(abs, "*.json");
                Array.Sort(files, StringComparer.Ordinal);
                foreach (var f in files)
                {
                    string path = dir + "/" + Path.GetFileName(f);
                    menu.AddItem(new GUIContent(prefix + Path.GetFileName(f)), path == current, () => Defer(() => controller.Open(path)));
                }
            }
            AddFiles(StageLayoutBuilder.LayoutsDir, "StageLayouts/");
            AddFiles(StageMapPaths.TestDataDir, "TestData/");
            if (session.recentFiles.Count > 0)
            {
                menu.AddSeparator("");
                foreach (var recent in session.recentFiles)
                {
                    string path = recent;
                    if (File.Exists(StageMapPaths.ToAbsolute(path)))
                        menu.AddItem(new GUIContent("최근/" + path.Replace('/', '∕')), false, () => Defer(() => controller.Open(path)));
                }
            }
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("파일 선택..."), false, () => Defer(OpenWithPicker));
            menu.ShowAsContext();
        }

        private void OpenWithPicker()
        {
            string start = StageMapPaths.ToAbsolute(StageLayoutBuilder.LayoutsDir);
            string picked = EditorUtility.OpenFilePanel("레이아웃 JSON 열기", start, "json");
            if (string.IsNullOrEmpty(picked))
                return;
            string rel = StageMapPaths.ToAssetPath(picked);
            if (rel == null)
            {
                controller.SetStatus("프로젝트 Assets 폴더 안의 파일만 열 수 있습니다.", true);
                return;
            }
            controller.Open(rel);
        }

        private void SaveAsWithPicker()
        {
            string dir = Path.GetDirectoryName(controller.FilePath)?.Replace('\\', '/') ?? StageLayoutBuilder.LayoutsDir;
            string picked = EditorUtility.SaveFilePanelInProject("다른 이름으로 저장 (파일 이름 = stageId)", Path.GetFileNameWithoutExtension(controller.FileName), "json",
                "새 파일 이름이 stageId가 됩니다.", dir);
            if (!string.IsNullOrEmpty(picked))
                controller.SaveAs(picked);
        }

        // ------------------------------------------------------------------ (2) stage

        private void DrawStagePanel()
        {
            var d = controller.Data;
            EditorGUILayout.LabelField("스테이지", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("stageId (변경은 '다른 이름'으로)", d.stageId);
                EditorGUILayout.LabelField("version", d.version.ToString());
                Num("length", dd => dd.length, (dd, v) => dd.length = v);
                Num("killY", dd => dd.killY, (dd, v) => dd.killY = v);
                Num("cameraMinY", dd => dd.cameraMinY, (dd, v) => dd.cameraMinY = v);
                DrawThemeField(d);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("spawn", GUILayout.Width(50f));
                    Num("x", dd => dd.spawn.x, (dd, v) => dd.spawn.x = v, 14f);
                    Num("y", dd => dd.spawn.y, (dd, v) => dd.spawn.y = v, 14f);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("goal", GUILayout.Width(50f));
                    Num("x", dd => dd.goal.x, (dd, v) => dd.goal.x = v, 14f);
                    Num("y", dd => dd.goal.y, (dd, v) => dd.goal.y = v, 14f);
                }
            }
        }

        // Theme kits found under Data/Themes (refreshed when the window gets focus).
        private List<string> themeNames;

        private void RefreshThemeNames() => themeNames = ThemeKitTool.FindThemeNames();

        private void DrawThemeField(StageLayoutData d)
        {
            if (themeNames == null)
                RefreshThemeNames();
            string current = d.theme ?? "";
            var shown = new List<string> { "(없음)" };
            shown.AddRange(themeNames);
            int idx = string.IsNullOrEmpty(current) ? 0 : shown.IndexOf(current);
            if (idx < 0)
            {
                shown.Add("(없는 킷) " + current);
                idx = shown.Count - 1;
            }
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup(new GUIContent("theme", "씬 빌드 때 지형·기믹 외관에 쓰는 테마 킷 (Data/Themes/{theme}.asset). 없음 = 기본 프리미티브."), idx, shown.ToArray());
            if (EditorGUI.EndChangeCheck() && picked != idx && picked <= themeNames.Count)
            {
                string value = picked == 0 ? "" : themeNames[picked - 1];
                controller.ApplyEdit("Map Tool: Edit theme", dd => { dd.theme = value; return true; });
            }
            if (themeNames.Count == 0)
                EditorGUILayout.LabelField("테마 킷이 없습니다: Tools/MyGame/Map Tool/Create Or Refresh Theme Kit", EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------------ (3) palette and options

        private void DrawPalettePanel()
        {
            EditorGUILayout.LabelField("도구 · 팔레트 (Scene 뷰에서 클릭해 배치)", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                ToolRow(MapPaletteTool.Select, MapPaletteTool.Ground, MapPaletteTool.Platform, MapPaletteTool.Wall);
                ToolRow(MapPaletteTool.Checkpoint, MapPaletteTool.Coin, MapPaletteTool.Special, MapPaletteTool.Spike);
                ToolRow(MapPaletteTool.Mover, MapPaletteTool.Enemy, MapPaletteTool.Faller, MapPaletteTool.Section);
                ToolRow(MapPaletteTool.Decoration, MapPaletteTool.SpikeTrap, MapPaletteTool.Switch, MapPaletteTool.Gate);
                ToolRow(MapPaletteTool.Boss);
                if (session.tool == MapPaletteTool.Decoration)
                    DrawPropPicker();

                using (new EditorGUILayout.HorizontalScope())
                {
                    int gridIndex = Array.FindIndex(GridChoices, g => Mathf.Approximately(g, session.grid));
                    EditorGUI.BeginChangeCheck();
                    int picked = EditorGUILayout.Popup("격자", gridIndex < 0 ? 1 : gridIndex, GridLabels);
                    if (EditorGUI.EndChangeCheck())
                        session.grid = GridChoices[picked];
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    session.snap = GUILayout.Toggle(session.snap, "스냅");
                    session.surfaceSnap = GUILayout.Toggle(session.surfaceSnap, "표면 스냅");
                    session.showLabels = GUILayout.Toggle(session.showLabels, "라벨");
                    session.showSections = GUILayout.Toggle(session.showSections, "구간 띠");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    session.editEnabled = GUILayout.Toggle(session.editEnabled, "편집 (끄면 Scene 뷰 클릭을 Unity에 넘김)");
                    if (EditorGUI.EndChangeCheck() && session.editEnabled)
                        Selection.activeObject = null;
                    if (GUILayout.Button("2D 보기", GUILayout.Width(70f)))
                        overlay.FrameStage();
                    if (GUILayout.Button("빈 캔버스", GUILayout.Width(70f)))
                        Defer(OpenEmptyCanvas);
                }
            }
            if (GUI.changed)
                SceneView.RepaintAll();
        }

        // Decoration tool: the prop to place. Only Implemented rows with an existing prefab are offered, grouped by category.
        private void DrawPropPicker()
        {
            var props = controller.Props;
            if (!props.Available)
            {
                EditorGUILayout.HelpBox("prop 테이블을 읽을 수 없습니다: " + props.FailureReason, MessageType.Warning);
                return;
            }
            if (props.Implemented.Count == 0)
            {
                EditorGUILayout.HelpBox("배치할 수 있는 prop이 없습니다. PropTable.csv에서 프리팹이 준비된 행의 status를 Implemented로 바꾸세요 (Docs/prop-table-schema.md).", MessageType.Info);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("prop", GUILayout.Width(40f));
                if (EditorGUILayout.DropdownButton(new GUIContent(props.LabelFor(session.propId)), FocusType.Keyboard))
                {
                    ShowPropMenu(id =>
                    {
                        session.propId = id;
                        Repaint();
                        SceneView.RepaintAll();
                    });
                }
            }
        }

        /// <summary>Category-grouped popup of the Implemented props; <paramref name="onPick"/> runs after the IMGUI event.</summary>
        private void ShowPropMenu(Action<string> onPick)
        {
            var menu = new GenericMenu();
            foreach (var c in controller.Props.Implemented)
            {
                string id = c.PropId;
                menu.AddItem(new GUIContent($"{c.Category}/{c.Label}"), false, () => Defer(() => onPick(id)));
            }
            menu.ShowAsContext();
        }

        private void ToolRow(params MapPaletteTool[] tools)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                foreach (var tool in tools)
                {
                    var prev = GUI.backgroundColor;
                    GUI.backgroundColor = Color.Lerp(Color.white, ToolColor(tool), 0.55f);
                    bool on = GUILayout.Toggle(session.tool == tool, ToolLabel(tool), "Button");
                    GUI.backgroundColor = prev;
                    if (on && session.tool != tool)
                    {
                        var chosen = tool;
                        Defer(() => controller.SetTool(chosen));
                    }
                }
            }
        }

        private static string ToolLabel(MapPaletteTool tool)
        {
            switch (tool)
            {
                case MapPaletteTool.Select: return "선택";
                case MapPaletteTool.Special: return "황금 나뭇잎";
                case MapPaletteTool.Decoration: return "장식";
                case MapPaletteTool.SpikeTrap: return "가시함정";
                case MapPaletteTool.Switch: return "스위치";
                case MapPaletteTool.Gate: return "게이트";
                case MapPaletteTool.Boss: return "보스";
                default: return tool.ToString().ToLowerInvariant();
            }
        }

        private static Color ToolColor(MapPaletteTool tool)
        {
            switch (tool)
            {
                case MapPaletteTool.SpikeTrap: return StageLayoutBuilder.Palette.Spike;
                case MapPaletteTool.Switch: return new Color(1f, 0.85f, 0.25f);
                case MapPaletteTool.Gate: return new Color(0.75f, 0.6f, 0.35f);
                case MapPaletteTool.Boss: return new Color(0.85f, 0.2f, 0.45f);
                case MapPaletteTool.Ground: return StageLayoutBuilder.Palette.Ground;
                case MapPaletteTool.Platform: return StageLayoutBuilder.Palette.Platform;
                case MapPaletteTool.Wall: return StageLayoutBuilder.Palette.Wall;
                case MapPaletteTool.Checkpoint: return StageLayoutBuilder.Palette.CheckpointOn;
                case MapPaletteTool.Coin: return StageLayoutBuilder.Palette.Coin;
                case MapPaletteTool.Special: return StageLayoutBuilder.Palette.Special;
                case MapPaletteTool.Spike: return StageLayoutBuilder.Palette.Spike;
                case MapPaletteTool.Mover: return StageLayoutBuilder.Palette.Mover;
                case MapPaletteTool.Enemy: return new Color(0.55f, 0.25f, 0.85f);
                case MapPaletteTool.Faller: return StageLayoutBuilder.Palette.Faller;
                case MapPaletteTool.Section: return new Color(0.4f, 0.65f, 1f);
                case MapPaletteTool.Decoration: return new Color(0.3f, 0.85f, 0.55f);
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------ (4) element list

        private void DrawElementListPanel()
        {
            var d = controller.Data;
            EditorGUILayout.LabelField("요소 목록", EditorStyles.boldLabel);
            if (rowStyle == null)
                rowStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(14, 2, 1, 1) };

            search = EditorGUILayout.TextField("검색", search);
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Height(240f));
            Group("points (spawn / goal)", LayoutElementKind.Spawn, 2, i => i == 0 ? $"spawn  x{F(d.spawn.x)} y{F(d.spawn.y)}" : $"goal  x{F(d.goal.x)} y{F(d.goal.y)}",
                i => new LayoutElementRef(i == 0 ? LayoutElementKind.Spawn : LayoutElementKind.Goal, 0));
            Group("sections", LayoutElementKind.Section, d.sections.Length, i => $"{d.sections[i].id}  {F(d.sections[i].startX)}..{F(d.sections[i].endX)}  {d.sections[i].note}");
            Group("blocks", LayoutElementKind.Block, d.blocks.Length, i => { var b = d.blocks[i]; return $"{b.id}  {b.kind}  x{F(b.x)} y{F(b.y)} w{F(b.w)} h{F(b.h)}"; });
            Group("checkpoints", LayoutElementKind.Checkpoint, d.checkpoints.Length, i => $"{d.checkpoints[i].id}  x{F(d.checkpoints[i].x)} y{F(d.checkpoints[i].y)}");
            Group("coins", LayoutElementKind.Coin, d.coins.Length, i => $"#{i}  x{F(d.coins[i].x)} y{F(d.coins[i].y)}");
            Group("specials", LayoutElementKind.Special, d.specials.Length, i => $"{d.specials[i].id}  slot{d.specials[i].slot}  x{F(d.specials[i].x)} y{F(d.specials[i].y)}");
            Group("spikes", LayoutElementKind.Spike, d.spikes.Length, i => $"{d.spikes[i].id}  x{F(d.spikes[i].x)} y{F(d.spikes[i].y)} w{F(d.spikes[i].w)}");
            Group("movers", LayoutElementKind.Mover, d.movers.Length, i => { var m = d.movers[i]; return $"{m.id}  x{F(m.x)} y{F(m.y)} w{F(m.w)} {m.axis}{F(m.distance)}"; });
            Group("enemies", LayoutElementKind.Enemy, d.enemies.Length, i => { var e = d.enemies[i]; return $"{e.id}  m{e.ResolvedMonsterId}  x{F(e.x)} y{F(e.y)} range{F(e.range)}"; });
            Group("fallers", LayoutElementKind.Faller, d.fallers.Length, i => $"{d.fallers[i].id}  x{F(d.fallers[i].x)} y{F(d.fallers[i].y)}");
            Group("spikeTraps", LayoutElementKind.SpikeTrap, d.spikeTraps.Length, i => { var t = d.spikeTraps[i]; return $"{t.id}  x{F(t.x)} y{F(t.y)} w{F(t.w)} T{F(t.period)}/{F(t.upTime)}"; });
            Group("switches", LayoutElementKind.Switch, d.switches.Length, i => $"{d.switches[i].id}  ch{d.switches[i].channel}  x{F(d.switches[i].x)} y{F(d.switches[i].y)}");
            Group("gates", LayoutElementKind.Gate, d.gates.Length, i => $"{d.gates[i].id}  ch{d.gates[i].channel}  x{F(d.gates[i].x)} y{F(d.gates[i].y)} h{F(d.gates[i].h)}");
            if (d.HasBoss)
                Group("boss", LayoutElementKind.Boss, 1, i => $"boss {d.boss.bossId}  x{F(d.boss.x)} y{F(d.boss.y)}  arena {F(d.boss.arenaMinX)}..{F(d.boss.arenaMaxX)}");
            Group("decorations", LayoutElementKind.Decoration, d.decorations.Length, i => { var dec = d.decorations[i]; return $"{dec.id}  {dec.prop}  x{F(dec.x)} y{F(dec.y)} z{F(controller.Props.ResolveZ(dec))}"; });
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                bool hasSel = !controller.Selection.IsNone;
                using (new EditorGUI.DisabledScope(!hasSel))
                {
                    if (GUILayout.Button("삭제"))
                        Defer(() => controller.DeleteSelected());
                    if (GUILayout.Button("복제"))
                        Defer(() => controller.DuplicateSelected());
                    if (GUILayout.Button("프레이밍"))
                        overlay.FrameSelection();
                }
                if (GUILayout.Button("구간 추가"))
                    Defer(AddSectionAtEnd);
            }
        }

        private void AddSectionAtEnd()
        {
            var d = controller.Data;
            float x = 0f;
            foreach (var s in d.sections)
                x = Mathf.Max(x, s.endX);
            controller.Place(MapPaletteTool.Section, new Vector2(x, 0f), true);
        }

        private static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        private void Group(string title, LayoutElementKind kind, int count, Func<int, string> text, Func<int, LayoutElementRef> refOf = null)
        {
            bool open = openGroups.Contains(kind);
            bool nowOpen = EditorGUILayout.Foldout(open, $"{title} ({count})", true);
            if (nowOpen != open)
            {
                if (nowOpen) openGroups.Add(kind); else openGroups.Remove(kind);
            }
            if (!nowOpen && string.IsNullOrEmpty(search))
                return;

            const int maxRows = 200;
            int shown = 0;
            for (int i = 0; i < count; i++)
            {
                string line = text(i);
                if (!string.IsNullOrEmpty(search) && line.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (!nowOpen && string.IsNullOrEmpty(search))
                    continue;
                if (shown++ >= maxRows)
                {
                    EditorGUILayout.LabelField($"... 더 있음 (검색으로 좁히세요)", EditorStyles.miniLabel);
                    break;
                }
                var r = refOf != null ? refOf(i) : new LayoutElementRef(kind, i);
                bool selected = controller.Selection == r;
                bool error = controller.HasErrorFor(r);
                var prevBg = GUI.backgroundColor;
                var prevContent = GUI.contentColor;
                if (selected) GUI.backgroundColor = new Color(1f, 0.92f, 0.3f);
                if (error) GUI.contentColor = new Color(1f, 0.4f, 0.4f);
                bool clicked = GUILayout.Button(line, rowStyle);
                int clicks = Event.current.clickCount;
                GUI.backgroundColor = prevBg;
                GUI.contentColor = prevContent;
                if (clicked)
                {
                    bool frame = clicks >= 2;
                    Defer(() =>
                    {
                        controller.Select(r);
                        if (frame)
                            overlay.FrameSelection();
                    });
                }
            }
        }

        // ------------------------------------------------------------------ (6) validation

        private void DrawValidationPanel()
        {
            EditorGUILayout.LabelField("검증", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label($"오류 {controller.ErrorCount} · 경고 {controller.WarningCount}", EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("검증", GUILayout.Width(60f)))
                        controller.RunValidation();
                    session.autoValidate = GUILayout.Toggle(session.autoValidate, "자동", GUILayout.Width(50f));
                }
                if (controller.LastValidationTime != default)
                    EditorGUILayout.LabelField("마지막 실행", controller.LastValidationTime.ToString("HH:mm:ss"), EditorStyles.miniLabel);
                if (!controller.Monsters.Available)
                    EditorGUILayout.HelpBox("몬스터 테이블을 읽을 수 없습니다: " + controller.Monsters.FailureReason, MessageType.Warning);
                if (!controller.Props.Available && controller.Data.decorations.Length > 0)
                    EditorGUILayout.HelpBox("prop 테이블을 읽을 수 없습니다: " + controller.Props.FailureReason, MessageType.Warning);

                issueScroll = EditorGUILayout.BeginScrollView(issueScroll, GUILayout.MaxHeight(170f));
                int shown = 0;
                foreach (var issue in controller.Issues)
                {
                    if (shown++ >= 100)
                    {
                        EditorGUILayout.LabelField("... (100건까지 표시)", EditorStyles.miniLabel);
                        break;
                    }
                    bool isError = issue.Severity == IssueSeverity.Error;
                    var prev = GUI.contentColor;
                    GUI.contentColor = isError ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 0.8f, 0.3f);
                    string text = (isError ? "ERROR " : "WARN ") + issue.Message + (issue.Target.IsNone ? "" : "  →");
                    bool clicked = GUILayout.Button(text, rowStyle ?? EditorStyles.label);
                    GUI.contentColor = prev;
                    if (clicked && !issue.Target.IsNone)
                    {
                        var target = issue.Target;
                        Defer(() =>
                        {
                            controller.Select(target);
                            overlay.FrameSelection();
                        });
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        // ------------------------------------------------------------------ (7) build

        private void DrawBuildPanel(bool locked)
        {
            EditorGUILayout.LabelField("빌드", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                string playReason = null;
                bool canPlay = StageMapPlayTest.CanPlay(controller.Data.stageId, out playReason);
                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(locked || controller.IsBusy))
                    {
                        if (GUILayout.Button(new GUIContent("씬 빌드", "저장 후 Stage_<stageId>.unity를 생성합니다 (검증 오류가 없어야 함).")))
                            Defer(() => controller.Build());
                    }
                    using (new EditorGUI.DisabledScope(locked || controller.IsBusy || !canPlay))
                    {
                        var content = new GUIContent("빌드 + 플레이 ▶", canPlay ? "빌드한 뒤 이 스테이지를 Play Mode로 실행합니다. 종료 시 씬 구성이 복원됩니다." : playReason);
                        if (GUILayout.Button(content))
                            Defer(() => controller.BuildAndPlay());
                    }
                }
                if (!canPlay)
                    EditorGUILayout.LabelField("플레이 불가: " + playReason, EditorStyles.wordWrappedMiniLabel);
                if (!string.IsNullOrEmpty(controller.LastBuildReport))
                {
                    showBuildReport = EditorGUILayout.Foldout(showBuildReport, "마지막 빌드 보고", true);
                    if (showBuildReport)
                        EditorGUILayout.SelectableLabel(controller.LastBuildReport, EditorStyles.textArea, GUILayout.MinHeight(80f));
                }
            }
        }

        // ------------------------------------------------------------------ status line

        private void DrawStatusBar()
        {
            string text = controller.StatusMessage;
            if (string.IsNullOrEmpty(text))
                text = controller.HasDocument ? $"격자 {session.grid:0.##} m" : "준비됨";
            var prev = GUI.contentColor;
            if (controller.StatusIsError)
                GUI.contentColor = new Color(1f, 0.5f, 0.4f);
            EditorGUILayout.LabelField(text, EditorStyles.helpBox);
            GUI.contentColor = prev;
        }
    }
}
