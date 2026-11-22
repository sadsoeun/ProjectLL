using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame.EditorTools
{
    internal enum PromptChoice
    {
        Primary,
        Secondary,
        Cancel,
    }

    /// <summary>Dialogs the controller needs. The window implements it; the self check substitutes a scripted one.</summary>
    internal interface IStageMapPrompts
    {
        /// <summary>Three-way question. <paramref name="secondary"/> may be null (then only primary / cancel).</summary>
        PromptChoice Ask(string title, string message, string primary, string secondary, string cancel);

        bool Confirm(string title, string message, string ok);
    }

    /// <summary>
    /// Handles user intents for the map tool (design 6.2 / 6.4): file commands, selection, edits with Undo, dirty
    /// tracking, validation scheduling, build and play test requests. Owns no UI. The layout lives in the
    /// StageLayoutDocument; window-level state lives in StageMapSession. Main thread only.
    /// </summary>
    internal sealed class StageMapController : IDisposable
    {
        private const double ValidationDebounceSeconds = 0.25;
        private const int MaxRecentFiles = 8;

        private readonly StageMapSession session;
        private readonly IStageMapPrompts prompts;
        private StageLayoutDocument document;

        private bool dragging;
        private int dragGroup;
        private double validationDue;
        private bool busy;
        private bool disposed;

        public event Action Changed;
        public event Action SelectionChanged;
        public event Action ValidationUpdated;

        public List<ValidationIssue> Issues { get; private set; } = new List<ValidationIssue>();
        public DateTime LastValidationTime { get; private set; }
        public string StatusMessage { get; private set; } = "";
        public bool StatusIsError { get; private set; }
        public string LastBuildReport { get; private set; } = "";
        public string LastLoadErrorPath { get; private set; }
        public bool ExternalChangePending { get; private set; }
        public MonsterChoiceCache Monsters { get; } = new MonsterChoiceCache();
        public PropChoiceCache Props { get; } = new PropChoiceCache();

        public StageMapController(StageMapSession session, StageLayoutDocument existingDocument, IStageMapPrompts prompts)
        {
            Assert.IsNotNull(session, "session");
            Assert.IsNotNull(prompts, "prompts");
            this.session = session;
            this.prompts = prompts;
            document = existingDocument != null ? existingDocument : StageLayoutDocument.CreateTransient(null);

            // After a script reload the document normally survives (DontSave object referenced by the window). If it
            // did not (R-05), rebuild it from the canonical JSON kept in the session.
            if (session.hasDocument && string.IsNullOrEmpty(document.Data?.stageId))
                RecoverDocument();

            Monsters.Refresh();
            Props.Refresh();
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.update += Tick;
            if (HasDocument)
                RunValidation();
        }

        // ------------------------------------------------------------------ state

        public StageLayoutDocument Document => document;
        public StageMapSession Session => session;
        public bool HasDocument => session.hasDocument && document != null && document.Data != null;
        public StageLayoutData Data => HasDocument ? document.Data : null;
        public bool IsDirty => session.isDirty;
        public string FilePath => session.filePath;
        public string FileName => string.IsNullOrEmpty(session.filePath) ? "untitled" : Path.GetFileName(session.filePath);
        public LayoutElementRef Selection => session.selection;
        public bool IsDragging => dragging;
        public bool IsBusy => busy;

        /// <summary>True while editing must be refused (Play Mode, entering Play Mode, compiling).</summary>
        public static bool IsLocked => EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling;

        public int ErrorCount => StageMapValidation.Count(Issues, IssueSeverity.Error);
        public int WarningCount => StageMapValidation.Count(Issues, IssueSeverity.Warning);

        private string ExpectedStageId => string.IsNullOrEmpty(session.filePath) ? null : Path.GetFileNameWithoutExtension(session.filePath);

        public void SetStatus(string message, bool isError = false)
        {
            StatusMessage = message ?? "";
            StatusIsError = isError;
            if (isError)
                Debug.LogWarning("[MapTool] " + message);
            Changed?.Invoke();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.update -= Tick;
            Changed = null;
            SelectionChanged = null;
            ValidationUpdated = null;
        }

        // ------------------------------------------------------------------ recovery

        private void RecoverDocument()
        {
            if (!string.IsNullOrEmpty(session.currentJson) && StageLayoutJson.TryParse(session.currentJson, out var data, out _))
            {
                StageLayoutJson.Normalize(data);
                document.Replace(data);
                Undo.ClearUndo(document);
                StatusMessage = "스크립트 리로드 후 편집 내용을 복원했습니다 (Undo 기록은 비워졌습니다).";
                return;
            }
            if (!string.IsNullOrEmpty(session.filePath) && File.Exists(StageMapPaths.ToAbsolute(session.filePath)) && LoadFrom(session.filePath))
            {
                StatusMessage = "편집 내용이 복원되지 않아 파일에서 다시 읽었습니다.";
                StatusIsError = true;
                return;
            }
            session.hasDocument = false;
        }

        // ------------------------------------------------------------------ validation

        public void ScheduleValidation()
        {
            if (session.autoValidate)
                validationDue = EditorApplication.timeSinceStartup + ValidationDebounceSeconds;
        }

        /// <summary>Runs the existing validator on the current model now (also used by the Validate button).</summary>
        public void RunValidation()
        {
            validationDue = 0;
            if (!HasDocument)
            {
                Issues = new List<ValidationIssue>();
            }
            else
            {
                Issues = StageMapValidation.Run(document.Data, FileName, ExpectedStageId);
                LastValidationTime = DateTime.Now;
            }
            ValidationUpdated?.Invoke();
        }

        private void Tick()
        {
            if (validationDue > 0 && !dragging && EditorApplication.timeSinceStartup >= validationDue)
                RunValidation();
        }

        public bool HasErrorFor(LayoutElementRef r)
        {
            foreach (var i in Issues)
            {
                if (i.Severity == IssueSeverity.Error && i.Target == r)
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ dirty / commit

        private void RecomputeDirty()
        {
            try
            {
                session.currentJson = StageLayoutJson.Write(document.Data);
                session.isDirty = session.currentJson != session.savedJson;
            }
            catch (InvalidOperationException e)
            {
                session.isDirty = true;
                StatusMessage = "값을 JSON으로 기록할 수 없습니다: " + e.Message;
                StatusIsError = true;
            }
        }

        private void Commit()
        {
            document.AssertInvariant();
            RecomputeDirty();
            ScheduleValidation();
            Changed?.Invoke();
        }

        private void OnUndoRedo()
        {
            if (!HasDocument)
                return;
            ClampSelection();
            Commit();
        }

        private void ClampSelection()
        {
            if (session.selection.IsNone || session.selection.IsValid(document.Data))
                return;
            session.selection = LayoutElementRef.None;
            SelectionChanged?.Invoke();
        }

        // ------------------------------------------------------------------ edits

        /// <summary>
        /// Applies one edit under an Undo record. The edit returns whether it changed anything. Ignored (with a log)
        /// while editing is locked. Drag edits (between BeginDrag and EndDrag) join the drag's Undo group.
        /// </summary>
        public bool ApplyEdit(string undoName, Func<StageLayoutData, bool> edit)
        {
            Assert.IsNotNull(edit, "edit");
            if (!HasDocument)
                return false;
            if (IsLocked)
            {
                Debug.LogWarning("[MapTool] Edit ignored: Play Mode or compilation is active.");
                return false;
            }
            if (!dragging)
                Undo.IncrementCurrentGroup();
            Undo.RecordObject(document, undoName);
            if (!dragging)
                Undo.SetCurrentGroupName(undoName);

            bool changed = edit(document.Data);
            if (!changed)
                return false;
            if (dragging)
            {
                session.isDirty = true;
                Changed?.Invoke();
            }
            else
            {
                Commit();
            }
            return true;
        }

        public void BeginDrag(string undoName)
        {
            if (!HasDocument || IsLocked || dragging)
                return;
            Undo.IncrementCurrentGroup();
            dragGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            dragging = true;
        }

        public void EndDrag()
        {
            if (!dragging)
                return;
            dragging = false;
            Undo.CollapseUndoOperations(dragGroup);
            if (HasDocument)
                Commit();
        }

        /// <summary>Esc during a drag: reverts everything recorded since BeginDrag.</summary>
        public void CancelDrag()
        {
            if (!dragging)
                return;
            dragging = false;
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(dragGroup);
            if (HasDocument)
            {
                ClampSelection();
                Commit();
            }
        }

        public void Select(LayoutElementRef r)
        {
            if (!HasDocument || !r.IsValid(document.Data))
                r = LayoutElementRef.None;
            if (r == session.selection)
                return;
            session.selection = r;
            SelectionChanged?.Invoke();
            Changed?.Invoke();
        }

        public void SetTool(MapPaletteTool tool)
        {
            if (session.tool == tool)
                return;
            session.tool = tool;
            Changed?.Invoke();
        }

        /// <summary>The grid / surface-snapped point for a raw world point; <paramref name="snapOn"/> already includes the Ctrl inversion.</summary>
        public Vector2 ResolvePoint(LayoutElementKind kind, Vector2 raw, bool snapOn, float probeDx = 0f)
        {
            return StageLayoutEditOps.SnapPlacement(document.Data, kind, raw, session.grid, snapOn, session.surfaceSnap, probeDx);
        }

        private static LayoutElementKind KindOf(MapPaletteTool tool)
        {
            switch (tool)
            {
                case MapPaletteTool.Ground:
                case MapPaletteTool.Platform:
                case MapPaletteTool.Wall: return LayoutElementKind.Block;
                case MapPaletteTool.Checkpoint: return LayoutElementKind.Checkpoint;
                case MapPaletteTool.Coin: return LayoutElementKind.Coin;
                case MapPaletteTool.Special: return LayoutElementKind.Special;
                case MapPaletteTool.Spike: return LayoutElementKind.Spike;
                case MapPaletteTool.Mover: return LayoutElementKind.Mover;
                case MapPaletteTool.Enemy: return LayoutElementKind.Enemy;
                case MapPaletteTool.Faller: return LayoutElementKind.Faller;
                case MapPaletteTool.Section: return LayoutElementKind.Section;
                case MapPaletteTool.Decoration: return LayoutElementKind.Decoration;
                case MapPaletteTool.SpikeTrap: return LayoutElementKind.SpikeTrap;
                case MapPaletteTool.Switch: return LayoutElementKind.Switch;
                case MapPaletteTool.Gate: return LayoutElementKind.Gate;
                case MapPaletteTool.Boss: return LayoutElementKind.Boss;
                default: return LayoutElementKind.None;
            }
        }

        /// <summary>Snapped world point for the palette tool (placement preview and Place use the same rule).</summary>
        public Vector2 ResolvePlacement(MapPaletteTool tool, Vector2 raw, bool snapOn)
        {
            return ResolvePoint(KindOf(tool), raw, snapOn, tool == MapPaletteTool.Spike ? 1f : tool == MapPaletteTool.SpikeTrap ? 1.5f : 0f);
        }

        /// <summary>Places the selected palette tool's element at the raw world point (design 7.5 defaults).</summary>
        public bool Place(MapPaletteTool tool, Vector2 raw, bool snapOn)
        {
            if (!HasDocument || tool == MapPaletteTool.Select)
                return false;
            string propId = session.propId;
            if (tool == MapPaletteTool.Decoration)
            {
                if (!Props.Available || !IsPlaceableProp(propId))
                {
                    SetStatus(Props.Available
                        ? "장식 도구: 배치할 prop을 먼저 선택하세요 (Implemented 상태이고 프리팹이 있는 prop만 선택할 수 있습니다)."
                        : "장식 도구: prop 테이블을 읽을 수 없습니다: " + Props.FailureReason, true);
                    return false;
                }
            }
            Vector2 p = ResolvePlacement(tool, raw, snapOn);
            LayoutElementRef created = LayoutElementRef.None;
            string message = null;
            bool ok = false;
            ApplyEdit("Map Tool: Add " + tool, d =>
            {
                if (tool == MapPaletteTool.Decoration)
                    ok = StageLayoutEditOps.TryAddDecoration(d, propId, Props.DefaultZ(propId), p, out created);
                else
                    ok = StageLayoutEditOps.TryAdd(d, tool, p, out created, out message);
                return ok;
            });
            if (!ok)
            {
                if (message != null)
                    SetStatus(message, true);
                return false;
            }
            Select(created);
            SetStatus($"{tool} 배치: ({p.x:0.##}, {p.y:0.##})");
            return true;
        }

        private bool IsPlaceableProp(string propId)
        {
            if (string.IsNullOrEmpty(propId))
                return false;
            foreach (var c in Props.Implemented)
            {
                if (c.PropId == propId)
                    return true;
            }
            return false;
        }

        public bool DeleteSelected()
        {
            if (!HasDocument || IsLocked)
                return false;
            var sel = session.selection;
            if (sel.IsNone || !sel.IsValid(document.Data))
                return false;
            if (sel.kind == LayoutElementKind.Spawn || sel.kind == LayoutElementKind.Goal)
            {
                SetStatus("spawn과 goal은 삭제할 수 없습니다.", true);
                return false;
            }

            bool clearRefs = false;
            if (sel.kind == LayoutElementKind.Section)
            {
                string id = document.Data.sections[sel.index].id;
                int refs = StageLayoutEditOps.CountSectionRefs(document.Data, id);
                if (refs > 0)
                {
                    var choice = prompts.Ask("구간 삭제",
                        $"구간 '{id}'를 {refs}개 요소가 참조합니다.\n참조를 비우지 않으면 검증 오류(unknown section)가 남습니다.",
                        "참조 비우고 삭제", "참조 유지하고 삭제", "취소");
                    if (choice == PromptChoice.Cancel)
                        return false;
                    clearRefs = choice == PromptChoice.Primary;
                }
            }

            string label = Describe(sel);
            ApplyEdit("Map Tool: Delete " + label, d =>
            {
                if (sel.kind == LayoutElementKind.Section)
                    StageLayoutEditOps.RemoveSection(d, sel.index, clearRefs);
                else
                    StageLayoutEditOps.Remove(d, sel);
                return true;
            });
            session.selection = LayoutElementRef.None;
            SelectionChanged?.Invoke();
            SetStatus("삭제: " + label);
            return true;
        }

        public bool DuplicateSelected()
        {
            if (!HasDocument || IsLocked)
                return false;
            var sel = session.selection;
            if (sel.IsNone || !sel.IsValid(document.Data))
                return false;
            LayoutElementRef created = LayoutElementRef.None;
            string message = null;
            bool ok = false;
            ApplyEdit("Map Tool: Duplicate " + Describe(sel), d =>
            {
                ok = StageLayoutEditOps.Duplicate(d, sel, session.grid, out created, out message);
                return ok;
            });
            if (!ok)
            {
                SetStatus(message ?? "복제할 수 없습니다.", true);
                return false;
            }
            Select(created);
            SetStatus("복제: " + Describe(created));
            return true;
        }

        /// <summary>Moves the selection by whole grid steps (one Undo step per call).</summary>
        public bool NudgeSelected(int stepsX, int stepsY)
        {
            var sel = session.selection;
            if (!HasDocument || sel.IsNone || !sel.IsValid(document.Data))
                return false;
            return ApplyEdit("Map Tool: Nudge " + Describe(sel), d =>
            {
                Vector2 pos = StageLayoutEditOps.GetPosition(d, sel);
                float g = session.grid > 0f ? session.grid : StageMapSession.DefaultGrid;
                StageLayoutEditOps.SetPosition(d, sel, new Vector2(StageLayoutEditOps.Snap(pos.x + stepsX * g, g), StageLayoutEditOps.Snap(pos.y + stepsY * g, g)));
                return true;
            });
        }

        public bool MoveElement(LayoutElementRef r, Vector2 position)
        {
            return ApplyEdit("Map Tool: Move " + Describe(r), d =>
            {
                if (!r.IsValid(d))
                    return false;
                Vector2 old = StageLayoutEditOps.GetPosition(d, r);
                if (Mathf.Approximately(old.x, position.x) && Mathf.Approximately(old.y, position.y))
                    return false;
                StageLayoutEditOps.SetPosition(d, r, position);
                return true;
            });
        }

        public bool DragEdge(LayoutElementRef r, LayoutHandle handle, float value)
        {
            return ApplyEdit("Map Tool: Resize " + Describe(r), d => r.IsValid(d) && StageLayoutEditOps.SetEdge(d, r, handle, value, session.grid));
        }

        /// <summary>Short text for lists, Undo names and status ("block W1_S1_A_ground1", "coin #3").</summary>
        public string Describe(LayoutElementRef r)
        {
            var d = Data;
            if (d == null || !r.IsValid(d))
                return "none";
            string id = StageLayoutEditOps.GetId(d, r);
            string kind = r.kind.ToString().ToLowerInvariant();
            return id != null ? $"{kind} {id}" : r.kind == LayoutElementKind.Coin ? $"coin #{r.index}" : kind;
        }

        // ------------------------------------------------------------------ files

        private bool ConfirmSaveOrDiscard()
        {
            if (!HasDocument || !session.isDirty)
                return true;
            var choice = prompts.Ask("저장되지 않은 변경", $"'{FileName}'의 변경 사항을 저장할까요?", "저장", "저장 안 함", "취소");
            if (choice == PromptChoice.Cancel)
                return false;
            return choice == PromptChoice.Primary ? Save() : true;
        }

        private void AddRecent(string path)
        {
            session.recentFiles.Remove(path);
            session.recentFiles.Insert(0, path);
            while (session.recentFiles.Count > MaxRecentFiles)
                session.recentFiles.RemoveAt(session.recentFiles.Count - 1);
        }

        /// <summary>New layout from the MT-002 template at <paramref name="folder"/>/&lt;stageId&gt;.json (written on first save).</summary>
        public bool New(string stageId, string folder)
        {
            if (IsLocked)
            {
                SetStatus("Play Mode 또는 컴파일 중에는 새로 만들 수 없습니다.", true);
                return false;
            }
            if (!StageMapPaths.IsValidStageId(stageId))
            {
                SetStatus("stageId는 영문자, 숫자, '_' 또는 '-'만 사용할 수 있습니다.", true);
                return false;
            }
            if (string.IsNullOrEmpty(folder) || !folder.StartsWith("Assets", StringComparison.Ordinal))
            {
                SetStatus("저장 위치는 Assets 폴더 안이어야 합니다.", true);
                return false;
            }
            string path = folder.TrimEnd('/') + "/" + stageId + ".json";
            if (File.Exists(StageMapPaths.ToAbsolute(path)))
            {
                SetStatus($"이미 존재하는 파일입니다: {path}", true);
                return false;
            }
            if (!ConfirmSaveOrDiscard())
                return false;

            var data = StageLayoutEditOps.CreateTemplate(stageId);
            SetDocument(data, path, savedJson: "", fileHash: "", report: new List<string> { "새 레이아웃 (아직 저장되지 않음)" }, unknown: null);
            SetStatus($"새 레이아웃 '{stageId}' 생성. 저장하면 {path}에 기록됩니다.");
            return true;
        }

        /// <summary>Opens a layout file (asks to save a dirty current document first). A failure leaves the current model untouched.</summary>
        public bool Open(string assetPath)
        {
            if (IsLocked)
            {
                SetStatus("Play Mode 또는 컴파일 중에는 열 수 없습니다.", true);
                return false;
            }
            if (string.IsNullOrEmpty(assetPath))
                return false;
            if (!ConfirmSaveOrDiscard())
                return false;
            return LoadFrom(assetPath);
        }

        public bool Reload()
        {
            if (!HasDocument || string.IsNullOrEmpty(session.filePath))
                return false;
            if (session.isDirty && !prompts.Confirm("다시 읽기", "저장하지 않은 편집 내용이 사라집니다. 파일에서 다시 읽을까요?", "다시 읽기"))
                return false;
            return LoadFrom(session.filePath);
        }

        private bool LoadFrom(string assetPath)
        {
            string raw;
            string hash;
            try
            {
                raw = StageMapPaths.ReadText(assetPath);
                hash = StageMapPaths.HashOf(assetPath);
            }
            catch (Exception e)
            {
                LastLoadErrorPath = null;
                SetStatus($"파일을 읽을 수 없습니다: {assetPath} ({e.Message})", true);
                return false;
            }

            if (!StageLayoutJson.TryParse(raw, out var data, out string parseError))
            {
                LastLoadErrorPath = assetPath;
                SetStatus($"JSON 파싱 실패: {assetPath} ({parseError})", true);
                return false;
            }

            var fixes = StageLayoutJson.Normalize(data);
            var missing = StageLayoutJson.FindMissingTopLevelKeys(raw);
            var unknown = new List<string>(StageLayoutJson.FindUnknownKeys(raw));
            string canonical;
            try
            {
                canonical = StageLayoutJson.Write(data);
            }
            catch (InvalidOperationException e)
            {
                LastLoadErrorPath = assetPath;
                SetStatus($"파일 값을 처리할 수 없습니다: {assetPath} ({e.Message})", true);
                return false;
            }

            var report = new List<string>();
            if (missing.Count > 0)
                report.Add("ERROR 필수 키 누락: " + string.Join(", ", missing) + " (저장하면 채워집니다)");
            foreach (var f in fixes)
                report.Add("보정: " + f);
            if (unknown.Count > 0)
                report.Add("WARN 계약에 없는 필드 (저장 시 제거됨): " + string.Join(", ", unknown));
            if (canonical != raw)
                report.Add("INFO 정규 형식과 다릅니다. 저장하면 정규 형식으로 다시 기록됩니다 (의미는 동일).");
            string stem = Path.GetFileNameWithoutExtension(assetPath);
            if (data.stageId != stem)
                report.Add($"WARN 파일 이름 '{stem}'과 stageId '{data.stageId}'가 다릅니다.");

            // Missing keys / corrections mean the file is not what the model writes: start dirty so a save fixes it.
            string saved = (missing.Count > 0 || fixes.Count > 0) ? "" : canonical;
            LastLoadErrorPath = null;
            SetDocument(data, assetPath, saved, hash, report, unknown);
            SetStatus($"열기: {assetPath}");
            return true;
        }

        private void SetDocument(StageLayoutData data, string path, string savedJson, string fileHash, List<string> report, List<string> unknown)
        {
            dragging = false;
            document.Replace(data);
            Undo.ClearUndo(document);
            session.hasDocument = true;
            session.filePath = path;
            session.savedJson = savedJson;
            session.savedFileHash = fileHash;
            session.selection = LayoutElementRef.None;
            session.tool = MapPaletteTool.Select;
            session.loadReport = report ?? new List<string>();
            session.unknownKeys = unknown ?? new List<string>();
            ExternalChangePending = false;
            AddRecent(path);
            RecomputeDirty();
            RunValidation();
            SelectionChanged?.Invoke();
            Changed?.Invoke();
        }

        /// <summary>Self-check hook: attaches in-memory data as a clean document without touching any file.</summary>
        internal void AttachData(StageLayoutData data, string pathLabel)
        {
            string canonical = StageLayoutJson.Write(data);
            SetDocument(data, pathLabel, canonical, "", new List<string>(), null);
        }

        public bool Save()
        {
            if (!HasDocument)
                return false;
            if (IsLocked)
            {
                SetStatus("Play Mode 또는 컴파일 중에는 저장할 수 없습니다.", true);
                return false;
            }
            string path = session.filePath;
            if (string.IsNullOrEmpty(path))
            {
                SetStatus("저장 경로가 없습니다. '다른 이름으로 저장'을 사용하세요.", true);
                return false;
            }

            // 1. The file changed on disk since it was loaded / saved.
            if (StageMapPaths.HashOf(path) != session.savedFileHash)
            {
                var choice = prompts.Ask("디스크 파일이 바뀌었습니다",
                    $"'{FileName}'이(가) 열린 뒤 다른 곳에서 수정되었습니다.\n덮어쓰면 그 변경이 사라집니다 (직전 사본은 Temp/MapTool/Backup에 남습니다).",
                    "덮어쓰기", "다시 읽기 (내 편집 폐기)", "취소");
                if (choice == PromptChoice.Cancel)
                    return false;
                if (choice == PromptChoice.Secondary)
                {
                    LoadFrom(path);
                    return false;
                }
            }

            // 2. BuildAll validates every file in LayoutsDir: an invalid one blocks the whole project build (C-03).
            RunValidation();
            if (ErrorCount > 0 && path.StartsWith(StageLayoutBuilder.LayoutsDir + "/", StringComparison.Ordinal))
            {
                if (!prompts.Confirm("검증 오류가 있는 레이아웃",
                        $"검증 오류 {ErrorCount}건이 있습니다. 이 파일을 {StageLayoutBuilder.LayoutsDir}에 저장하면 'Build Stages From Layouts'(BuildAll)가 오류를 해결할 때까지 모두 중단됩니다.",
                        "그래도 저장"))
                    return false;
            }

            // 3. Fields outside the contract are dropped by JsonUtility; ask once.
            if (session.unknownKeys.Count > 0)
            {
                if (!prompts.Confirm("계약에 없는 필드 제거",
                        "다음 필드는 저장하면 제거됩니다: " + string.Join(", ", session.unknownKeys), "제거하고 저장"))
                    return false;
            }

            string json;
            try
            {
                json = StageLayoutJson.Write(document.Data);
            }
            catch (InvalidOperationException e)
            {
                SetStatus("저장 실패 (값을 기록할 수 없음): " + e.Message, true);
                return false;
            }
            try
            {
                StageMapPaths.WriteCanonical(path, json);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                SetStatus("저장 실패: " + e.Message + " (파일은 변경되지 않았고 편집 내용은 유지됩니다)", true);
                return false;
            }

            session.savedJson = json;
            session.currentJson = json;
            session.savedFileHash = StageMapPaths.HashOf(path);
            session.isDirty = false;
            session.unknownKeys = new List<string>();
            ExternalChangePending = false;
            Changed?.Invoke();
            SetStatus("저장됨: " + path);
            return true;
        }

        /// <summary>Saves under a new file (stageId becomes the file name stem). Restores the previous path / stageId on failure.</summary>
        public bool SaveAs(string newAssetPath)
        {
            if (!HasDocument || IsLocked)
                return false;
            if (string.IsNullOrEmpty(newAssetPath) || !newAssetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                SetStatus("저장 경로는 Assets 아래의 .json 파일이어야 합니다.", true);
                return false;
            }
            string stem = Path.GetFileNameWithoutExtension(newAssetPath);
            if (!StageMapPaths.IsValidStageId(stem))
            {
                SetStatus("파일 이름(= stageId)은 영문자, 숫자, '_' 또는 '-'만 사용할 수 있습니다.", true);
                return false;
            }
            if (newAssetPath != session.filePath && File.Exists(StageMapPaths.ToAbsolute(newAssetPath))
                && !prompts.Confirm("파일 덮어쓰기", $"'{newAssetPath}'이(가) 이미 있습니다. 덮어쓸까요?", "덮어쓰기"))
                return false;

            string oldPath = session.filePath;
            string oldHash = session.savedFileHash;
            string oldStage = document.Data.stageId;
            bool renamed = oldStage != stem;
            session.filePath = newAssetPath;
            session.savedFileHash = StageMapPaths.HashOf(newAssetPath); // overwrite already confirmed above
            if (renamed)
                ApplyEdit("Map Tool: Set stageId", d => { d.stageId = stem; return true; });

            if (!Save())
            {
                session.filePath = oldPath;
                session.savedFileHash = oldHash;
                if (renamed)
                    ApplyEdit("Map Tool: Restore stageId", d => { d.stageId = oldStage; return true; });
                return false;
            }
            AddRecent(newAssetPath);
            RunValidation();
            if (renamed)
                SetStatus($"다른 이름으로 저장: {newAssetPath} (stageId를 '{stem}'로 변경. 요소 id 접두는 그대로입니다)");
            return true;
        }

        // ------------------------------------------------------------------ external change

        /// <summary>Compares the file on disk with what was loaded; reloads silently when clean, otherwise raises the warning.</summary>
        public void CheckExternalChange()
        {
            if (!HasDocument || string.IsNullOrEmpty(session.filePath) || dragging || busy)
                return;
            if (StageMapPaths.HashOf(session.filePath) == session.savedFileHash)
            {
                ExternalChangePending = false;
                return;
            }
            if (!session.isDirty)
            {
                if (LoadFrom(session.filePath))
                    SetStatus("디스크 파일이 바뀌어 다시 읽었습니다: " + session.filePath);
                return;
            }
            if (!ExternalChangePending)
            {
                ExternalChangePending = true;
                Changed?.Invoke();
            }
        }

        public void ReloadFromDisk()
        {
            if (HasDocument && !string.IsNullOrEmpty(session.filePath))
                LoadFrom(session.filePath);
        }

        /// <summary>"Ignore": accepts the disk state as the new baseline so the next save does not ask again.</summary>
        public void DismissExternalChange()
        {
            session.savedFileHash = StageMapPaths.HashOf(session.filePath);
            ExternalChangePending = false;
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------ build / play

        /// <summary>Saves if needed, requires zero validation errors, then builds Stage_&lt;stageId&gt;.unity with the existing builder.</summary>
        public bool Build()
        {
            return RunBuild(playAfter: false);
        }

        public bool BuildAndPlay()
        {
            return RunBuild(playAfter: true);
        }

        private bool RunBuild(bool playAfter)
        {
            if (!HasDocument || busy)
                return false;
            if (IsLocked)
            {
                SetStatus("Play Mode 또는 컴파일 중에는 빌드할 수 없습니다.", true);
                return false;
            }
            busy = true;
            try
            {
                string path = session.filePath;
                if (playAfter)
                {
                    if (!StageMapPlayTest.CanPlay(document.Data.stageId, out string reason))
                    {
                        SetStatus("플레이 테스트를 시작할 수 없습니다: " + reason, true);
                        return false;
                    }
                }
                if (session.isDirty || !File.Exists(StageMapPaths.ToAbsolute(path)))
                {
                    if (!Save())
                        return false;
                }
                RunValidation();
                if (ErrorCount > 0)
                {
                    SetStatus($"검증 오류 {ErrorCount}건이 있어 빌드하지 않았습니다.", true);
                    return false;
                }
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    SetStatus("빌드를 취소했습니다.");
                    return false;
                }

                if (playAfter)
                {
                    if (!StageMapPlayTest.Start(path, out string message))
                    {
                        LastBuildReport = "FAIL " + message;
                        SetStatus("플레이 테스트 시작 실패: " + message, true);
                        return false;
                    }
                    LastBuildReport = "STARTED " + message;
                    SetStatus("플레이 테스트 시작: " + document.Data.stageId);
                    return true;
                }

                try
                {
                    EditorUtility.DisplayProgressBar("Map Tool", "스테이지 씬을 빌드하는 중...", 0.5f);
                    LastBuildReport = StageLayoutBuilder.BuildFromFile(path);
                }
                catch (Exception e)
                {
                    LastBuildReport = "FAIL " + e.Message;
                    Debug.LogException(e);
                    SetStatus("빌드 실패 (자세한 내용은 빌드 보고 참조)", true);
                    return false;
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
                SetStatus($"빌드 완료: Stage_{document.Data.stageId}.unity");
                return true;
            }
            finally
            {
                busy = false;
                Changed?.Invoke();
            }
        }
    }
}
