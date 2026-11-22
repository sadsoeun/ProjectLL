using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Build + play test (design D-05) without any runtime change: the layout is built into its registered
    /// Stage_&lt;id&gt; scene, 06_InGame and that scene are opened together, InGameController.debugStageId is set in memory
    /// only, and Play Mode is entered. When Play Mode ends the value and the previous scene setup are restored.
    /// The state lives in SessionState so it survives the domain reload of entering Play Mode (R-04).
    /// </summary>
    [InitializeOnLoad]
    internal static class StageMapPlayTest
    {
        private const string KeyState = "MyGame.MapTool.PlayTest.State";       // "" | "Armed" | "Playing"
        private const string KeySetup = "MyGame.MapTool.PlayTest.SceneSetup";  // JSON of the scene setup before the test
        private const string KeyHasSetup = "MyGame.MapTool.PlayTest.HasSetup"; // distinguishes "no scenes with paths" from "no session"
        private const string KeyDebugId = "MyGame.MapTool.PlayTest.OriginalDebugStageId";
        private const string KeyArmedAt = "MyGame.MapTool.PlayTest.ArmedAt";
        private const string DebugStageField = "debugStageId";
        private const double OrphanSeconds = 2.0;

        private static string InGameScenePath => SceneFlowSetup.ScenesDir + "/06_InGame.unity";
        private static string StageScenePath(string stageId) => $"{SceneFlowSetup.StageScenesDir}/Stage_{stageId}.unity";

        [Serializable]
        private sealed class SetupEntry
        {
            public string path;
            public bool isLoaded;
            public bool isActive;
        }

        [Serializable]
        private sealed class SetupList
        {
            public List<SetupEntry> entries = new List<SetupEntry>();
        }

        static StageMapPlayTest()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OrphanWatch;
        }

        /// <summary>True while a test session is armed or playing (the window shows it).</summary>
        public static bool IsActive => !string.IsNullOrEmpty(SessionState.GetString(KeyState, ""));

        // ------------------------------------------------------------------ preconditions

        /// <summary>
        /// Play tests need the stage registered in StageDatabase with SceneName == Stage_&lt;stageId&gt; (C-06) and the
        /// 06_InGame scene. Unregistered layouts can only be built (Q-01).
        /// </summary>
        public static bool CanPlay(string stageId, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(stageId))
            {
                reason = "stageId가 비어 있습니다.";
                return false;
            }
            var db = AssetDatabase.LoadAssetAtPath<StageDatabase>(SceneFlowSetup.DatabasePath);
            if (db == null)
            {
                reason = $"StageDatabase를 찾을 수 없습니다 ({SceneFlowSetup.DatabasePath}).";
                return false;
            }
            if (!db.TryGetStage(stageId, out var stage))
            {
                reason = $"StageDatabase에 '{stageId}'가 등록되어 있지 않습니다. 빌드만 가능합니다.";
                return false;
            }
            if (stage.SceneName != "Stage_" + stageId)
            {
                reason = $"등록된 씬 이름 '{stage.SceneName}'이(가) 'Stage_{stageId}'와 다릅니다.";
                return false;
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(InGameScenePath) == null)
            {
                reason = $"{InGameScenePath}가 없습니다.";
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ start

        /// <summary>
        /// Builds <paramref name="layoutPath"/> and enters Play Mode on it. The layout file must already be saved and valid
        /// (the caller checks). Returns false with a message and no lasting state change on any failure.
        /// </summary>
        public static bool Start(string layoutPath, out string message)
        {
            message = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                message = "Play Mode 또는 컴파일 중입니다.";
                return false;
            }
            if (IsActive)
            {
                message = "이전 플레이 테스트 세션이 아직 정리되지 않았습니다.";
                return false;
            }
            if (!StageLayoutJson.TryParse(StageMapPaths.ReadText(layoutPath), out var data, out string parseError))
            {
                message = "레이아웃을 읽을 수 없습니다: " + parseError;
                return false;
            }
            if (!CanPlay(data.stageId, out string reason))
            {
                message = reason;
                return false;
            }

            RecordSceneSetup();
            try
            {
                EditorUtility.DisplayProgressBar("Map Tool", "스테이지 씬을 빌드하는 중...", 0.3f);
                StageLayoutBuilder.BuildFromFile(layoutPath);

                EditorUtility.DisplayProgressBar("Map Tool", "InGame 씬을 여는 중...", 0.7f);
                var inGame = EditorSceneManager.OpenScene(InGameScenePath, OpenSceneMode.Single);
                EditorSceneManager.OpenScene(StageScenePath(data.stageId), OpenSceneMode.Additive);
                SceneManager.SetActiveScene(inGame);

                var controller = FindInGameController(inGame);
                if (controller == null)
                    throw new InvalidOperationException("06_InGame에서 InGameController를 찾을 수 없습니다.");
                var so = new SerializedObject(controller);
                var prop = so.FindProperty(DebugStageField);
                if (prop == null || prop.propertyType != SerializedPropertyType.String)
                    throw new InvalidOperationException($"InGameController에 문자열 필드 '{DebugStageField}'가 없습니다.");

                SessionState.SetString(KeyDebugId, prop.stringValue);
                prop.stringValue = data.stageId;
                so.ApplyModifiedPropertiesWithoutUndo();

                SessionState.SetString(KeyArmedAt, EditorApplication.timeSinceStartup.ToString("R"));
                SessionState.SetString(KeyState, "Armed");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                message = e.Message;
                Restore();
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorApplication.EnterPlaymode();
            message = "Play Mode 진입을 요청했습니다.";
            return true;
        }

        private static InGameController FindInGameController(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var c = root.GetComponentInChildren<InGameController>(true);
                if (c != null)
                    return c;
            }
            return null;
        }

        // ------------------------------------------------------------------ scene setup record / restore

        private static void RecordSceneSetup()
        {
            var list = new SetupList();
            foreach (var s in EditorSceneManager.GetSceneManagerSetup())
            {
                if (string.IsNullOrEmpty(s.path))
                    continue; // untitled scenes cannot be reopened
                list.entries.Add(new SetupEntry { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive });
            }
            SessionState.SetString(KeySetup, JsonUtility.ToJson(list));
            SessionState.SetBool(KeyHasSetup, true);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            string state = SessionState.GetString(KeyState, "");
            if (string.IsNullOrEmpty(state))
                return;
            if (change == PlayModeStateChange.ExitingEditMode)
                SessionState.SetString(KeyState, "Playing");
            else if (change == PlayModeStateChange.EnteredEditMode)
                Restore();
        }

        // A session armed but never played (compile error dialog, user cancelled): restore after a short wait.
        private static void OrphanWatch()
        {
            if (SessionState.GetString(KeyState, "") != "Armed" || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (!double.TryParse(SessionState.GetString(KeyArmedAt, "0"), out double armedAt))
                armedAt = 0;
            if (EditorApplication.timeSinceStartup - armedAt >= OrphanSeconds)
                Restore();
        }

        /// <summary>Puts debugStageId back, reopens the previous scene setup and clears the session. Safe to call repeatedly.</summary>
        public static void Restore()
        {
            bool hasSetup = SessionState.GetBool(KeyHasSetup, false);
            string setupJson = SessionState.GetString(KeySetup, "");
            string originalId = SessionState.GetString(KeyDebugId, null);
            ClearSession();

            try
            {
                if (originalId != null)
                {
                    var inGame = SceneManager.GetSceneByPath(InGameScenePath);
                    if (inGame.IsValid() && inGame.isLoaded)
                    {
                        var controller = FindInGameController(inGame);
                        if (controller != null)
                        {
                            var so = new SerializedObject(controller);
                            var prop = so.FindProperty(DebugStageField);
                            if (prop != null)
                            {
                                prop.stringValue = originalId;
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                    }
                }

                var setups = new List<SceneSetup>();
                if (hasSetup && !string.IsNullOrEmpty(setupJson))
                {
                    var list = JsonUtility.FromJson<SetupList>(setupJson);
                    foreach (var e in list.entries)
                    {
                        if (File.Exists(StageMapPaths.ToAbsolute(e.path)))
                            setups.Add(new SceneSetup { path = e.path, isLoaded = e.isLoaded, isActive = e.isActive });
                    }
                }
                if (setups.Count > 0)
                {
                    // RestoreSceneManagerSetup needs at least one loaded scene and exactly one active scene.
                    bool anyLoaded = false;
                    int active = 0;
                    foreach (var s in setups)
                    {
                        anyLoaded |= s.isLoaded;
                        if (s.isActive) active++;
                    }
                    if (!anyLoaded)
                        setups[0].isLoaded = true;
                    if (active != 1)
                    {
                        foreach (var s in setups)
                            s.isActive = false;
                        foreach (var s in setups)
                        {
                            if (s.isLoaded) { s.isActive = true; break; }
                        }
                    }
                    EditorSceneManager.RestoreSceneManagerSetup(setups.ToArray());
                }
                else if (hasSetup)
                {
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[MapTool] Restoring the scene setup after the play test failed: " + e.Message);
            }
        }

        private static void ClearSession()
        {
            SessionState.EraseString(KeyState);
            SessionState.EraseString(KeySetup);
            SessionState.EraseBool(KeyHasSetup);
            SessionState.EraseString(KeyDebugId);
            SessionState.EraseString(KeyArmedAt);
        }
    }
}
