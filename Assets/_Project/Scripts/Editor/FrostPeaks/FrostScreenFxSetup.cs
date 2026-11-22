using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Frost Peaks screen post-processing (blizzard snow, frozen screen border, wind streaks, cold grading).
    /// Idempotent: creates / refreshes the volume profile, registers <see cref="FrostScreenFxFeature"/> on the URP
    /// renderers, assigns the profile to the FrostPeaks theme kit (so rebuilt stages keep it) and patches the already
    /// built W3 stage scenes.
    /// </summary>
    public static class FrostScreenFxSetup
    {
        public const string ProfilePath = "Assets/_Project/Data/PostProcess/FrostPeaks_ScreenFx.asset";
        public const string ShaderPath = "Assets/_Project/Shaders/FX_FrostScreen.shader";
        public const string Theme = "FrostPeaks";
        private static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset",
        };

        [MenuItem("Tools/MyGame/Art/Setup Frost Screen FX")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder("[FrostScreenFx]\n");
            var profile = EnsureProfile(log);
            foreach (var path in RendererPaths)
                EnsureFeature(path, log);
            AssignToTheme(profile, log);
            PatchStageScenes(profile, log);
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ------------------------------------------------------------------ profile

        private static VolumeProfile EnsureProfile(StringBuilder log)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ProfilePath));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
                log.AppendLine("profile created: " + ProfilePath);
            }
            else
                log.AppendLine("profile refreshed: " + ProfilePath);

            var frost = Get<FrostScreenFx>(profile);
            frost.intensity.Override(1f);
            frost.snowAmount.Override(0.55f);
            frost.flakeSize.Override(1f);
            frost.snowSpeed.Override(1f);
            frost.snowColor.Override(new Color(0.95f, 0.98f, 1f, 1f));
            frost.windDirection.Override(new Vector2(-1f, -0.32f));
            frost.windStrength.Override(1.1f);
            frost.gustStrength.Override(0.65f);
            frost.streakAmount.Override(0.55f);
            frost.frostWidth.Override(0.12f);
            frost.frostOpacity.Override(0.62f);
            frost.frostDistortion.Override(0.012f);
            frost.frostTint.Override(new Color(0.78f, 0.9f, 1f, 1f));

            // Cold grading. Built-in URP effects, all evaluated in the single uber post-process pass.
            var wb = Get<WhiteBalance>(profile);
            wb.temperature.Override(-16f);
            wb.tint.Override(4f);

            var ca = Get<ColorAdjustments>(profile);
            ca.postExposure.Override(0f);
            ca.contrast.Override(10f);
            ca.saturation.Override(-6f);
            ca.colorFilter.Override(new Color(0.94f, 0.98f, 1.03f, 1f));

            var vig = Get<Vignette>(profile);
            vig.color.Override(new Color(0.1f, 0.18f, 0.3f));
            vig.intensity.Override(0.24f);
            vig.smoothness.Override(0.5f);

            var bloom = Get<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.2f);
            bloom.scatter.Override(0.55f);
            bloom.tint.Override(new Color(0.85f, 0.93f, 1f));
            bloom.highQualityFiltering.Override(false);   // cheaper downsample (performance first)

            // Keep the game's untonemapped toon look and switch off costly effects the default profile might add.
            Get<Tonemapping>(profile).mode.Override(TonemappingMode.None);
            Get<MotionBlur>(profile).intensity.Override(0f);
            Get<FilmGrain>(profile).intensity.Override(0f);
            Get<ChromaticAberration>(profile).intensity.Override(0f);
            Get<LensDistortion>(profile).intensity.Override(0f);
            Get<DepthOfField>(profile).mode.Override(DepthOfFieldMode.Off);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T c))
            {
                c = profile.Add<T>(false);
                c.name = typeof(T).Name;
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }
            c.active = true;
            return c;
        }

        // ------------------------------------------------------------------ renderer feature

        private static void EnsureFeature(string rendererPath, StringBuilder log)
        {
            var data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (data == null)
            {
                log.AppendLine("renderer missing: " + rendererPath);
                return;
            }
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            FrostScreenFxFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is FrostScreenFxFeature ff)
                    feature = ff;

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FrostScreenFxFeature>();
                feature.name = "FrostScreenFx";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                {
                    var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mapField?.GetValue(data) is List<long> map)
                        map.Add(localId);
                }
                log.AppendLine("feature added: " + rendererPath);
            }
            else
                log.AppendLine("feature present: " + rendererPath);

            var so = new SerializedObject(feature);
            so.FindProperty("shader").objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            data.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }

        // ------------------------------------------------------------------ theme + scenes

        private static void AssignToTheme(VolumeProfile profile, StringBuilder log)
        {
            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(StageThemeKit.PathFor(Theme));
            if (kit == null)
            {
                log.AppendLine("theme kit missing: " + Theme);
                return;
            }
            kit.screenFxProfile = profile;
            EditorUtility.SetDirty(kit);
            log.AppendLine("theme kit: " + Theme + ".screenFxProfile set");
        }

        /// <summary>Stage scenes whose layout uses the FrostPeaks theme (W3_*).</summary>
        public static List<string> FrostStageScenes()
        {
            var list = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Project/Data/StageLayouts" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json"))
                    continue;
                var text = System.IO.File.ReadAllText(path);
                if (!System.Text.RegularExpressions.Regex.IsMatch(text, "\"theme\"\\s*:\\s*\"" + Theme + "\""))
                    continue;
                var id = System.IO.Path.GetFileNameWithoutExtension(path);
                var scene = $"Assets/_Project/Scenes/Stages/Stage_{id}.unity";
                if (System.IO.File.Exists(scene))
                    list.Add(scene);
            }
            list.Sort();
            return list;
        }

        private static void PatchStageScenes(VolumeProfile profile, StringBuilder log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                log.AppendLine("scenes skipped (Play Mode)");
                return;
            }
            var setup = EditorSceneManager.GetSceneManagerSetup();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var open = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (open.isDirty && !string.IsNullOrEmpty(open.path) && !open.path.EndsWith("03_Title.unity"))
                {
                    log.AppendLine("scenes skipped: unsaved changes in " + open.path);
                    return;
                }
            }
            try
            {
                foreach (var path in FrostStageScenes())
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    GameObject lighting = null;
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.name == "Lighting")
                            lighting = root;
                    if (lighting == null)
                    {
                        log.AppendLine("no Lighting root: " + path);
                        continue;
                    }
                    var w = lighting.GetComponent<StageScreenWeather>() ?? lighting.AddComponent<StageScreenWeather>();
                    w.profile = profile;
                    EditorUtility.SetDirty(w);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    log.AppendLine("scene patched: " + path);
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }
    }
}
