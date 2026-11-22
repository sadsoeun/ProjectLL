using System;
using System.IO;
using System.Reflection;
using MyGame.Dev;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Tools/MyGame/Demo/Record Showcase Reel: backs up the save + settings files, sets the Game view to 1920x1080, opens the
    /// Title scene and enters Play mode with <see cref="ShowcaseDirector"/>, which records Docs/deliverables/LeafAndLeap_Showcase.mp4.
    /// The backed-up files are restored when Play mode ends, so the reel never changes the player's progress.
    /// </summary>
    [InitializeOnLoad]
    public static class ShowcaseReelMenu
    {
        private const string KeyArmed = "MyGame.Showcase.Armed";
        private const string KeyBackup = "MyGame.Showcase.Backup";
        public const string KeyQuick = "MyGame.Showcase.Quick"; // test run: set true before Record()
        public const string KeyTrailer = "MyGame.Showcase.Trailer"; // clean trailer takes instead of the milestone reel
        public const string KeyStoryPart = "MyGame.Showcase.StoryPart"; // "" = whole shoot, "tutorial" / "boss" = pick-up shoot of that part
        public const string KeyStory = "MyGame.Showcase.Story"; // trailer takes in story order (tutorial, voice unlock, final clear)
        private const string TitleScene = "Assets/_Project/Scenes/03_Title.unity";
        private const string MainBgm = "Assets/_Project/Audio/BGM/BGM_Main.mp3";
        private const string BossBgm = "Assets/_Project/Audio/BGM/BGM_Boss.mp3";
        private static readonly string[] Files = { SaveService.FileName, "settings.json" };

        static ShowcaseReelMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
        }

        [MenuItem("Tools/MyGame/Demo/Record Story Trailer Footage")]
        public static void RecordStory()
        {
            EditorPrefs.SetBool(KeyStory, true);
            RecordTrailer();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorPrefs.SetBool(KeyStory, false);
        }

        [MenuItem("Tools/MyGame/Demo/Record Trailer Footage")]
        public static void RecordTrailer()
        {
            EditorPrefs.SetBool(KeyTrailer, true);
            Record();
            if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorPrefs.SetBool(KeyTrailer, false);
        }

        [MenuItem("Tools/MyGame/Demo/Record Showcase Reel")]
        public static void Record()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Showcase] Stop Play mode first.");
                return;
            }
            bool onTitle = UnityEngine.SceneManagement.SceneManager.sceneCount == 1 &&
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == TitleScene;
            // Play mode never loses edit-mode changes; only switching scenes would, so that needs clean scenes.
            // Unsaved changes in the open scene: leave it alone and start Play mode from the Title scene instead
            // (playModeStartScene keeps the edit-mode scene as it is; it is cleared again when Play mode ends).
            bool viaStartScene = false;
            for (int i = 0; !onTitle && i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    viaStartScene = true;
            string backup = Path.Combine(Path.GetTempPath(), "LeafAndLeap_ShowcaseBackup");
            Directory.CreateDirectory(backup);
            foreach (string f in Files)
            {
                string src = Path.Combine(Application.persistentDataPath, f);
                string dst = Path.Combine(backup, f);
                if (File.Exists(dst)) File.Delete(dst);
                if (File.Exists(src)) File.Copy(src, dst);
            }
            EditorPrefs.SetString(KeyBackup, backup);
            EditorPrefs.SetBool(KeyArmed, true);
            SetGameViewSize(ShowcaseDirector.Width, ShowcaseDirector.Height);
            if (viaStartScene) EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScene);
            else if (!onTitle) EditorSceneManager.OpenScene(TitleScene);
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (!EditorPrefs.GetBool(KeyArmed, false))
                return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                ShowcaseDirector.Quick = EditorPrefs.GetBool(KeyQuick, false);
                ShowcaseDirector.Trailer = EditorPrefs.GetBool(KeyTrailer, false);
                ShowcaseDirector.Story = ShowcaseDirector.Trailer && EditorPrefs.GetBool(KeyStory, false);
                ShowcaseDirector.StoryPart = EditorPrefs.GetString(KeyStoryPart, "");
                if (ShowcaseDirector.Trailer) ShowcaseDirector.Launch(null, null); // the trailer gets its own music in the edit
                else ShowcaseDirector.Launch(AssetDatabase.LoadAssetAtPath<AudioClip>(MainBgm), AssetDatabase.LoadAssetAtPath<AudioClip>(BossBgm));
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorPrefs.SetBool(KeyArmed, false);
                EditorPrefs.SetBool(KeyQuick, false);
                EditorSceneManager.playModeStartScene = null; // never leave the start-scene override behind
                EditorPrefs.SetBool(KeyTrailer, false);
                EditorPrefs.SetBool(KeyStory, false);
                EditorPrefs.SetString(KeyStoryPart, "");
                RestoreBackup();
            }
        }

        private static void RestoreBackup()
        {
            string backup = EditorPrefs.GetString(KeyBackup, "");
            if (string.IsNullOrEmpty(backup) || !Directory.Exists(backup))
                return;
            foreach (string f in Files)
            {
                string src = Path.Combine(backup, f);
                string dst = Path.Combine(Application.persistentDataPath, f);
                if (File.Exists(src)) File.Copy(src, dst, true);
                else if (File.Exists(dst)) File.Delete(dst); // the file did not exist before the reel
            }
            Debug.Log("[Showcase] Save and settings restored from " + backup);
        }

        /// <summary>Selects (adding if needed) a fixed-resolution size in the Game view. Uses internal editor API.</summary>
        private static void SetGameViewSize(int width, int height)
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                object sizes = single.GetProperty("instance").GetValue(null);
                object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
                object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
                var groupT = group.GetType();
                int count = (int)groupT.GetMethod("GetTotalCount").Invoke(group, null);
                int index = -1;
                for (int i = 0; i < count; i++)
                {
                    object s = groupT.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                    var st = s.GetType();
                    if ((int)st.GetProperty("width").GetValue(s) == width && (int)st.GetProperty("height").GetValue(s) == height &&
                        st.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution")
                    { index = i; break; }
                }
                if (index < 0)
                {
                    var sizeType = asm.GetType("UnityEditor.GameViewSize");
                    var kind = asm.GetType("UnityEditor.GameViewSizeType");
                    object size = Activator.CreateInstance(sizeType, Enum.Parse(kind, "FixedResolution"), width, height, "Showcase 1080p");
                    groupT.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                    index = (int)groupT.GetMethod("GetTotalCount").Invoke(group, null) - 1;
                }
                var gameViewType = asm.GetType("UnityEditor.GameView");
                var window = EditorWindow.GetWindow(gameViewType);
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var cb = gameViewType.GetMethod("SizeSelectionCallback", flags);
                if (cb != null) cb.Invoke(window, new object[] { index, null });
                else gameViewType.GetProperty("selectedSizeIndex", flags)?.SetValue(window, index);
                Debug.Log($"[Showcase] Game view size index {index} ({width}x{height}).");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Showcase] Could not set the Game view size; frames will be scaled. " + e.Message);
            }
        }
    }
}
