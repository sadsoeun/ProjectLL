using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Operator cheat window: set each stage to locked-progress states (not cleared / cleared / master clear),
    /// replay the world map unlock presentation, and back up / restore the save. Editor-only.
    /// In Play Mode it edits the live save (and reloads the world map); in Edit Mode it edits save_01.json on disk.
    /// </summary>
    public sealed class StageProgressWindow : EditorWindow
    {
        private const string StageDbPath = "Assets/_Project/Data/StageDatabase.asset";
        private const string BackupName = "save_01.devbackup.json";

        private Vector2 scroll;
        private StageDatabase db;

        private static string SavePath => Path.Combine(Application.persistentDataPath, SaveService.FileName);
        private static string BackupPath => Path.Combine(Application.persistentDataPath, BackupName);

        [MenuItem("Tools/MyGame/Cheats/Stage Progress Window")]
        private static void Open() => GetWindow<StageProgressWindow>("스테이지 진행 치트");

        private void OnEnable() => db = AssetDatabase.LoadAssetAtPath<StageDatabase>(StageDbPath);

        private void OnGUI()
        {
            if (db == null)
            {
                EditorGUILayout.HelpBox($"StageDatabase not found at {StageDbPath}.", MessageType.Error);
                return;
            }
            bool playing = EditorApplication.isPlaying;
            var save = LoadSave(out string problem);
            EditorGUILayout.HelpBox(playing
                ? "Play 중: 현재 플레이 세이브를 수정하고, 월드맵이 열려 있으면 다시 불러옵니다."
                : "Edit 모드: 디스크의 save_01.json을 수정합니다 (에디터와 빌드가 같은 파일을 씁니다).", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("진행 상황 백업")) Backup();
                GUI.enabled = File.Exists(BackupPath);
                if (GUILayout.Button("백업 복원")) Restore();
                GUI.enabled = true;
            }
            EditorGUILayout.LabelField(File.Exists(BackupPath) ? $"백업: {File.GetLastWriteTime(BackupPath):yyyy-MM-dd HH:mm}" : "백업 없음", EditorStyles.miniLabel);

            if (save == null)
            {
                EditorGUILayout.HelpBox(problem, MessageType.Warning);
                return;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전부 미클리어")) Modify(s => { foreach (var st in db.Stages) if (st != null) SetState(s, st, 0); }, "전부 미클리어");
                if (GUILayout.Button("전부 클리어")) Modify(s => { foreach (var st in db.Stages) if (st != null) SetState(s, st, 1); }, "전부 클리어");
                if (GUILayout.Button("전부 마스터")) Modify(s => { foreach (var st in db.Stages) if (st != null) SetState(s, st, 2); }, "전부 마스터");
            }
            if (GUILayout.Button(new GUIContent("엔딩 직전 (마지막 스테이지만 미클리어)",
                    "아웃트로 영상이 붙은 마지막 스테이지만 미클리어로 두고 나머지는 모두 클리어 처리합니다. " +
                    "이미 클리어한 스테이지의 수집품·기록은 그대로 둡니다. 마지막 스테이지를 직접 깨면 첫 클리어로 처리되어 아웃트로가 나옵니다.")))
                Modify(s =>
                {
                    foreach (var st in db.Stages)
                    {
                        if (st == null) continue;
                        if (IsEndingStage(st)) SetState(s, st, 0);
                        else if (StateOf(s, st) == 0) SetState(s, st, 1);
                    }
                }, "엔딩 직전");
            using (new EditorGUILayout.HorizontalScope())
            {
                var tutorial = save.Tutorial;
                EditorGUILayout.LabelField(tutorial.skipped ? "튜토리얼: 건너뜀 (모두 본 것으로 처리)" : $"튜토리얼: 본 안내 {tutorial.SeenCount}개",
                    GUILayout.MinWidth(160));
                if (GUILayout.Button(new GUIContent("튜토리얼 초기화",
                        "모든 튜토리얼 안내를 '보지 않음'으로 되돌립니다 (클리어 기록과 상관없이 다시 표시). 클리어·수집품·해금은 바뀌지 않습니다. " +
                        "Play 중에는 현재 세이브에 바로 적용되고, 스테이지 안이라면 아직 지나가지 않은 안내가 다시 나옵니다.")))
                    Modify(s => s.Tutorial.ResetAll(), "튜토리얼 초기화");
            }
            GUI.enabled = playing;
            if (GUILayout.Button(new GUIContent("결과 화면 마스터 폭죽 미리보기", "Play 중 스테이지 안에서만: 결과 화면을 첫 마스터 클리어처럼 띄웁니다 (배지 + 폭죽). 진행 상황은 바뀌지 않습니다.")))
                PreviewMasterResult();
            GUI.enabled = true;
            EditorGUILayout.Space();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            var stages = db.Stages;
            for (int i = 0; i < stages.Count; i++)
            {
                var st = stages[i];
                if (st == null) continue;
                int state = StateOf(save, st);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(StageUnlocks.DisplayNumber(st.StageId), GUILayout.Width(45));
                    EditorGUILayout.LabelField(state == 2 ? "마스터" : state == 1 ? "클리어" : "-", GUILayout.Width(45));
                    if (GUILayout.Button("미클리어")) Modify(s => SetState(s, st, 0), $"{st.StageId} 미클리어");
                    if (GUILayout.Button("클리어")) Modify(s => SetState(s, st, 1), $"{st.StageId} 클리어");
                    if (GUILayout.Button("마스터")) Modify(s => SetState(s, st, 2), $"{st.StageId} 마스터");
                    int from = i;
                    if (GUILayout.Button(new GUIContent("여기부터 미클리어", "이 스테이지와 이후 스테이지를 모두 미클리어로 되돌립니다.")))
                        Modify(s => { for (int j = from; j < stages.Count; j++) if (stages[j] != null) SetState(s, stages[j], 0); }, $"{st.StageId}부터 미클리어");
                    GUI.enabled = playing;
                    if (GUILayout.Button(new GUIContent("클리어 연출", "Play 중에만: 이 스테이지를 클리어한 것처럼 월드맵으로 돌아가 개방 연출을 보여 줍니다.")))
                        PlayClear(st, false);
                    if (GUILayout.Button(new GUIContent("마스터 연출", "Play 중에만: 수집품까지 모두 모아 클리어한 것처럼 월드맵으로 돌아갑니다. " +
                            "이전에 마스터가 아니었으면 마스터 클리어 연출(황금 폭발·패드 금색 전환·배너)이 먼저 나옵니다. 이미 마스터면 개방 연출만 나옵니다.")))
                        PlayClear(st, true);
                    GUI.enabled = true;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        // 0 = not cleared, 1 = cleared (no collectibles), 2 = master (cleared + every collectible)
        private static int StateOf(SaveData save, StageData st)
        {
            var r = save.FindStage(st.StageId);
            if (r == null || !r.cleared) return 0;
            return st.CollectibleCount > 0 && r.CollectedCount() >= st.CollectibleCount ? 2 : 1;
        }

        /// <summary>The stage whose first clear plays the outro; the last stage of the database when none is flagged.</summary>
        private bool IsEndingStage(StageData st)
        {
            bool anyFlagged = false;
            StageData last = null;
            foreach (var s in db.Stages)
            {
                if (s == null) continue;
                anyFlagged |= s.PlayOutroOnFirstClear;
                last = s;
            }
            return anyFlagged ? st.PlayOutroOnFirstClear : st == last;
        }

        private static void SetState(SaveData save, StageData st, int state)
        {
            var r = save.FindStage(st.StageId);
            if (state == 0)
            {
                if (r != null) save.stages.Remove(r);
                return;
            }
            float keepTime = r != null && r.cleared ? r.bestTime : 0f;
            save.RecordClear(st.StageId, keepTime);
            r = save.FindStage(st.StageId);
            r.collectibles = new bool[Mathf.Max(0, st.CollectibleCount)];
            if (state == 2)
                for (int i = 0; i < r.collectibles.Length; i++) r.collectibles[i] = true;
        }

        private SaveData LoadSave(out string problem)
        {
            problem = null;
            if (EditorApplication.isPlaying)
            {
                if (!GameSystems.Exists) { problem = "GameSystems가 실행 중이 아닙니다 (01_Logo에서 Play)."; return null; }
                var s = GameSystems.Instance.Save;
                if (!s.HasSave) { problem = "불러온 세이브가 없습니다. 새로 시작하거나 이어하기 후 사용하세요."; return null; }
                return s.Current;
            }
            if (JsonFileStore.TryRead(SavePath, out SaveData data) != ReadResult.Ok || data == null || !data.IsValid())
            {
                problem = $"읽을 수 있는 세이브 파일이 없습니다.\n{SavePath}";
                return null;
            }
            return data;
        }

        private void Modify(System.Action<SaveData> change, string label)
        {
            var save = LoadSave(out string problem);
            if (save == null) { Debug.LogWarning("[Cheats] " + problem); return; }
            change(save);
            KeepCurrentNodeOpen(save);
            save.updatedAt = System.DateTime.UtcNow.ToString("o");
            if (EditorApplication.isPlaying)
            {
                var systems = GameSystems.Instance;
                bool ok = systems.Save.Save();
                Debug.Log($"[Cheats] {label}: save {(systems.Save.IsTemporary ? "is temporary (memory only)" : ok ? "written" : "WRITE FAILED")}.");
                ReloadWorldMap(new LoadRequest(SceneId.WorldMap));
            }
            else
            {
                bool ok = JsonFileStore.TryWrite(SavePath, save);
                Debug.Log($"[Cheats] {label} in {SavePath}: {(ok ? "written" : "WRITE FAILED")}.");
            }
            Repaint();
        }

        /// <summary>Same bookkeeping as InGameController.ClearRoutine, then returns to the map with the unlock presentation.</summary>
        private void PlayClear(StageData st, bool master)
        {
            var systems = GameSystems.Exists ? GameSystems.Instance : null;
            if (systems == null || !systems.Save.HasSave || systems.Loader.IsBusy) return;
            var save = systems.Save.Current;
            var lockedBefore = StageUnlocks.CaptureLocked(systems.StageDb, save);
            bool masteredBefore = StageMastery.IsMastered(st, save);
            var existing = save.FindStage(st.StageId);
            bool firstClear = existing == null || !existing.cleared;
            bool[] keep = existing != null && existing.cleared ? existing.collectibles : null;
            SetState(save, st, master ? 2 : 1);
            if (!master && keep != null) save.FindStage(st.StageId).MergeCollectibles(keep);
            var opened = StageUnlocks.NewlyOpened(systems.StageDb, save, lockedBefore);
            bool newlyMastered = !masteredBefore && StageMastery.IsMastered(st, save); // same rule as InGameController.ClearRoutine
            if (systems.StageDb.TryGetNodeByStage(st.StageId, out var node))
                save.currentNodeId = node.NodeId;
            systems.Save.Save();
            Debug.Log($"[Cheats] Clear presentation for {st.StageId}{(master ? " (master)" : "")}{(newlyMastered ? ", newly mastered" : master ? ", already mastered: no master presentation" : "")}; newly opened: {(opened.Length == 0 ? "none" : string.Join(", ", opened))}.");
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, st.StageId, LoadReason.StageClear, opened, newlyMastered, firstClear));
        }

        /// <summary>Play Mode in a stage: shows the result screen as a first-time master clear (badge + fireworks).</summary>
        private static void PreviewMasterResult()
        {
            var view = Object.FindAnyObjectByType<InGameView>();
            if (view == null) { Debug.LogWarning("[Cheats] No InGameView: enter a stage first."); return; }
            Time.timeScale = 0f; // like the real result screen; Continue returns to the world map
            if (GameSystems.Exists) GameSystems.Instance.InputMode.SetMode(InputMode.UI);
            view.ShowResult(83.4f, 57, 3, 3, true, true);
            Debug.Log("[Cheats] Master result preview shown (Continue loads the world map without the master presentation).");
        }

        private void KeepCurrentNodeOpen(SaveData save)
        {
            if (!db.TryGetNode(save.currentNodeId, out var node) || NodeStateResolver.Resolve(node, save) == NodeState.Locked)
                save.currentNodeId = db.StartNodeId;
        }

        private static void ReloadWorldMap(LoadRequest request)
        {
            var systems = GameSystems.Instance;
            if (SceneManager.GetActiveScene().name == SceneId.WorldMap.ToSceneName() && !systems.Loader.IsBusy)
                systems.Loader.LoadWithLoading(request);
        }

        private void Backup()
        {
            if (EditorApplication.isPlaying && GameSystems.Exists && GameSystems.Instance.Save.HasSave)
                GameSystems.Instance.Save.Save();
            if (!File.Exists(SavePath)) { EditorUtility.DisplayDialog("스테이지 진행 치트", "백업할 세이브 파일이 없습니다.", "확인"); return; }
            if (File.Exists(BackupPath) && !EditorUtility.DisplayDialog("스테이지 진행 치트", "기존 백업을 덮어씁니다. 계속할까요?", "덮어쓰기", "취소")) return;
            File.Copy(SavePath, BackupPath, true);
            Debug.Log($"[Cheats] Save backed up to {BackupPath}.");
        }

        private void Restore()
        {
            if (!EditorUtility.DisplayDialog("스테이지 진행 치트", "현재 진행 상황을 백업 시점으로 되돌립니다. 계속할까요?", "복원", "취소")) return;
            File.Copy(BackupPath, SavePath, true);
            if (EditorApplication.isPlaying && GameSystems.Exists)
            {
                GameSystems.Instance.Save.ReloadFromDisk();
                ReloadWorldMap(new LoadRequest(SceneId.WorldMap));
            }
            Debug.Log("[Cheats] Save restored from backup.");
            Repaint();
        }
    }
}
