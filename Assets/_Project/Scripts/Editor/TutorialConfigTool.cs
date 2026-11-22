using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>Creates Resources/Tutorial/TutorialConfig.asset (tutorial placement keyed by stage id; stage rebuilds never touch it).</summary>
    public static class TutorialConfigTool
    {
        public const string AssetPath = "Assets/_Project/Resources/Tutorial/TutorialConfig.asset";

        [MenuItem("Tools/MyGame/Tutorial/Select Tutorial Config")]
        private static void SelectMenu() => Selection.activeObject = EnsureAsset(false);

        [MenuItem("Tools/MyGame/Tutorial/Reset Tutorial Config To Defaults")]
        private static void ResetMenu()
        {
            if (!EditorUtility.DisplayDialog("Tutorial", "Overwrite TutorialConfig with the built-in W1_S1 defaults?", "Reset", "Cancel"))
                return;
            Selection.activeObject = EnsureAsset(true);
        }

        /// <summary>Loads the asset, creating it with defaults if missing; resetToDefaults rewrites the values.</summary>
        public static TutorialConfig EnsureAsset(bool resetToDefaults)
        {
            var config = AssetDatabase.LoadAssetAtPath<TutorialConfig>(AssetPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                config = ScriptableObject.CreateInstance<TutorialConfig>();
                config.ApplyDefaults();
                AssetDatabase.CreateAsset(config, AssetPath);
            }
            else if (resetToDefaults)
            {
                Undo.RecordObject(config, "Reset Tutorial Config");
                config.ApplyDefaults();
                EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}
