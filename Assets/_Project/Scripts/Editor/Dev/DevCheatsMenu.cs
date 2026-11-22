using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Operator cheats. Editor-only (lives in an Editor folder, so it is never compiled into a player build).
    /// Menu: Tools/MyGame/Cheats.
    /// - In Play Mode: edits the live save, writes it, and reloads the world map if it is open.
    /// - In Edit Mode: edits save_01.json on disk (the next play session picks it up).
    /// Best times are left empty (0 = no record) so cheated clears never look like real records.
    /// </summary>
    public static class DevCheatsMenu
    {
        private const string Menu = "Tools/MyGame/Cheats/";
        private const string StageDbPath = "Assets/_Project/Data/StageDatabase.asset";

        [MenuItem(Menu + "Clear All Stages")]
        private static void ClearAllStages() => Run(false);

        [MenuItem(Menu + "Clear All Stages + Collectibles")]
        private static void ClearAllStagesAndCollectibles() => Run(true);

        private static void Run(bool withCollectibles)
        {
            var db = AssetDatabase.LoadAssetAtPath<StageDatabase>(StageDbPath);
            if (db == null)
            {
                EditorUtility.DisplayDialog("Cheats", $"StageDatabase not found at {StageDbPath}.", "OK");
                return;
            }
            string what = withCollectibles ? "모든 스테이지를 클리어하고 수집품까지 채웁니다." : "모든 스테이지를 클리어 처리합니다.";
            string where = EditorApplication.isPlaying ? "현재 플레이 중인 세이브" : "디스크의 세이브 파일 (save_01.json)";
            if (!EditorUtility.DisplayDialog("운영자 치트", $"{what}\n대상: {where}\n\n되돌릴 수 없습니다. 계속할까요?", "실행", "취소"))
                return;

            if (EditorApplication.isPlaying)
                RunInPlayMode(db, withCollectibles);
            else
                RunOnDisk(db, withCollectibles);
        }

        private static int Apply(SaveData save, StageDatabase db, bool withCollectibles)
        {
            int count = 0;
            foreach (var stage in db.Stages)
            {
                if (stage == null || string.IsNullOrEmpty(stage.StageId))
                    continue;
                var existing = save.FindStage(stage.StageId);
                float keepTime = existing != null && existing.cleared ? existing.bestTime : 0f;
                save.RecordClear(stage.StageId, keepTime); // 0 = "no time" (keeps a real best time if there is one)
                if (withCollectibles && stage.CollectibleCount > 0)
                {
                    var all = new bool[stage.CollectibleCount];
                    for (int i = 0; i < all.Length; i++) all[i] = true;
                    save.FindStage(stage.StageId).MergeCollectibles(all);
                }
                count++;
            }
            return count;
        }

        private static void RunInPlayMode(StageDatabase db, bool withCollectibles)
        {
            if (!GameSystems.Exists)
            {
                Debug.LogWarning("[Cheats] GameSystems is not running.");
                return;
            }
            var systems = GameSystems.Instance;
            if (!systems.Save.HasSave)
            {
                EditorUtility.DisplayDialog("운영자 치트", "불러온 세이브가 없습니다. 새로 시작하거나 이어하기 후 다시 실행하세요.", "확인");
                return;
            }
            int n = Apply(systems.Save.Current, db, withCollectibles);
            bool saved = systems.Save.Save();
            Debug.Log($"[Cheats] Cleared {n} stages{(withCollectibles ? " (+collectibles)" : "")}; save {(systems.Save.IsTemporary ? "is temporary (memory only)" : saved ? "written" : "WRITE FAILED")}.");

            // Node states are resolved when the world map starts, so reload it to show the unlocks.
            if (SceneManager.GetActiveScene().name == SceneId.WorldMap.ToSceneName() && !systems.Loader.IsBusy)
                systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap));
        }

        private static void RunOnDisk(StageDatabase db, bool withCollectibles)
        {
            string path = Path.Combine(Application.persistentDataPath, SaveService.FileName);
            if (JsonFileStore.TryRead(path, out SaveData save) != ReadResult.Ok || save == null || !save.IsValid())
            {
                EditorUtility.DisplayDialog("운영자 치트", $"읽을 수 있는 세이브 파일이 없습니다.\n{path}\n\n게임에서 새로 시작한 뒤 다시 실행하거나, 플레이 중에 실행하세요.", "확인");
                return;
            }
            int n = Apply(save, db, withCollectibles);
            save.updatedAt = System.DateTime.UtcNow.ToString("o");
            bool ok = JsonFileStore.TryWrite(path, save);
            Debug.Log($"[Cheats] Cleared {n} stages{(withCollectibles ? " (+collectibles)" : "")} in {path}: {(ok ? "written" : "WRITE FAILED")}.");
        }
    }
}
