using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Screen post-processing of the non-frost worlds (GreenMeadow, SunDunes, CoralCoast, EmberFortress), built on the
    /// same skeleton as the frost blizzard: wind-blown particles, a themed screen border, wind and matching grading.
    /// Idempotent: creates / refreshes one volume profile per theme, registers <see cref="WeatherScreenFxFeature"/> on
    /// the URP renderers, assigns the profiles to the theme kits (so rebuilt stages keep them) and patches the already
    /// built stage scenes. Frost Peaks keeps <see cref="FrostScreenFxSetup"/>.
    /// </summary>
    public static class WeatherScreenFxSetup
    {
        public const string ProfileDir = "Assets/_Project/Data/PostProcess";
        public const string ShaderPath = "Assets/_Project/Shaders/FX_WeatherScreen.shader";
        public static readonly string[] Themes = { "GreenMeadow", "SunDunes", "CoralCoast", "EmberFortress" };
        private static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset",
        };

        private const string SeagullTheme = "CoralCoast";   // background gulls (AmbientSeagulls) for the coast stages
        private const int SeagullCount = 5;

        public static string ProfilePath(string theme) => $"{ProfileDir}/{theme}_ScreenFx.asset";

        [MenuItem("Tools/MyGame/Art/Setup World Screen FX")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            var log = new StringBuilder("[WeatherScreenFx]\n");
            foreach (var path in RendererPaths)
                EnsureFeature(path, log);
            foreach (var theme in Themes)
            {
                var profile = EnsureProfile(theme, log);
                AssignToTheme(theme, profile, log);
                PatchStageScenes(theme, profile, log);
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        // ------------------------------------------------------------------ profiles

        private static VolumeProfile EnsureProfile(string theme, StringBuilder log)
        {
            string path = ProfilePath(theme);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory(ProfileDir);
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                log.AppendLine("profile created: " + path);
            }
            else
                log.AppendLine("profile refreshed: " + path);

            var fx = Get<WeatherScreenFx>(profile);
            var wb = Get<WhiteBalance>(profile);
            var ca = Get<ColorAdjustments>(profile);
            var vig = Get<Vignette>(profile);
            var bloom = Get<Bloom>(profile);
            fx.intensity.Override(1f);
            ca.postExposure.Override(0f);
            vig.smoothness.Override(0.5f);
            bloom.scatter.Override(0.55f);
            bloom.highQualityFiltering.Override(false);   // cheaper downsample (performance first)

            // Shared base: every theme starts from "nothing" and switches on only what it needs.
            fx.particleGlow.Override(0f);
            fx.streakAmount.Override(0f);
            fx.hazeAmount.Override(0f);
            fx.hazeColor.Override(Color.white);
            fx.borderOpacity.Override(0f);
            fx.borderDistortion.Override(0f);
            fx.borderDetail.Override(0f);
            fx.heatShimmer.Override(0f);
            fx.heatScale.Override(9f);
            fx.heatSpeed.Override(0.6f);
            fx.splashAmount.Override(0f);
            wb.tint.Override(0f);

            switch (theme)
            {
                case "GreenMeadow":   // leaves drifting slowly, nothing else
                    fx.particleAmount.Override(0.06f);
                    fx.particleSize.Override(1.6f);
                    fx.particleSpeed.Override(0.35f);
                    fx.particleColor.Override(new Color(0.6f, 0.82f, 0.3f));
                    fx.particleColorB.Override(new Color(0.93f, 0.8f, 0.32f));
                    fx.particleDrift.Override(new Vector2(0f, -0.06f));
                    fx.particleFlutter.Override(0.9f);
                    fx.particleStretch.Override(0.5f);
                    fx.windDirection.Override(new Vector2(-1f, -0.1f));
                    fx.windStrength.Override(0.3f);
                    fx.gustStrength.Override(0.25f);
                    fx.borderStyle.Override(WeatherBorderStyle.LightLeak);
                    fx.borderWidth.Override(0.1f);
                    fx.borderTint.Override(Color.white);
                    fx.borderGlow.Override(Color.black);
                    wb.temperature.Override(0f);
                    ca.contrast.Override(0f);
                    ca.saturation.Override(0f);
                    ca.colorFilter.Override(Color.white);
                    vig.color.Override(Color.black);
                    vig.intensity.Override(0f);
                    bloom.threshold.Override(1.1f);
                    bloom.intensity.Override(0f);
                    bloom.tint.Override(Color.white);
                    break;

                case "SunDunes":      // blowing sand and a light sand texture on the screen edges
                    fx.particleAmount.Override(0.35f);
                    fx.particleSize.Override(0.5f);
                    fx.particleSpeed.Override(1.1f);
                    fx.particleColor.Override(new Color(0.96f, 0.82f, 0.52f));
                    fx.particleColorB.Override(new Color(1f, 0.92f, 0.68f));
                    fx.particleDrift.Override(new Vector2(0f, -0.03f));
                    fx.particleFlutter.Override(0f);
                    fx.particleStretch.Override(1.2f);
                    fx.windDirection.Override(new Vector2(-1f, -0.08f));
                    fx.windStrength.Override(1f);
                    fx.gustStrength.Override(0.5f);
                    fx.streakAmount.Override(0.12f);
                    fx.hazeColor.Override(new Color(0.94f, 0.8f, 0.52f));
                    fx.hazeAmount.Override(0.04f);
                    fx.borderStyle.Override(WeatherBorderStyle.Dust);
                    fx.borderWidth.Override(0.08f);
                    fx.borderOpacity.Override(0.2f);
                    fx.borderDetail.Override(0.5f);
                    fx.borderTint.Override(new Color(0.9f, 0.74f, 0.46f));
                    fx.borderGlow.Override(new Color(1f, 0.9f, 0.66f));
                    wb.temperature.Override(4f);
                    ca.contrast.Override(2f);
                    ca.saturation.Override(0f);
                    ca.colorFilter.Override(Color.white);
                    vig.color.Override(new Color(0.35f, 0.2f, 0.08f));
                    vig.intensity.Override(0.08f);
                    bloom.threshold.Override(1.1f);
                    bloom.intensity.Override(0.1f);
                    bloom.tint.Override(new Color(1f, 0.93f, 0.78f));
                    break;

                case "CoralCoast":    // a few water drops on the lens and small splashes; nothing blowing around
                    fx.particleAmount.Override(0f);
                    fx.particleSize.Override(0.5f);
                    fx.particleSpeed.Override(1f);
                    fx.particleColor.Override(Color.white);
                    fx.particleColorB.Override(Color.white);
                    fx.particleDrift.Override(Vector2.zero);
                    fx.particleFlutter.Override(0f);
                    fx.particleStretch.Override(0f);
                    fx.windDirection.Override(new Vector2(-1f, 0f));
                    fx.windStrength.Override(0f);
                    fx.gustStrength.Override(0f);
                    fx.borderStyle.Override(WeatherBorderStyle.Droplets);
                    fx.borderWidth.Override(0.12f);
                    fx.borderOpacity.Override(0.06f);
                    fx.borderDistortion.Override(0.014f);
                    fx.borderDetail.Override(0.22f);
                    fx.borderTint.Override(new Color(0.8f, 0.94f, 1f));
                    fx.borderGlow.Override(Color.white);
                    fx.splashAmount.Override(0.7f);
                    wb.temperature.Override(0f);
                    ca.contrast.Override(0f);
                    ca.saturation.Override(0f);
                    ca.colorFilter.Override(Color.white);
                    vig.color.Override(Color.black);
                    vig.intensity.Override(0f);
                    bloom.threshold.Override(1.1f);
                    bloom.intensity.Override(0f);
                    bloom.tint.Override(Color.white);
                    break;

                case "EmberFortress": // a few rising embers, a faint charred edge, light heat shimmer
                    fx.particleAmount.Override(0.15f);
                    fx.particleSize.Override(0.5f);
                    fx.particleSpeed.Override(0.7f);
                    fx.particleColor.Override(new Color(1f, 0.52f, 0.14f));
                    fx.particleColorB.Override(new Color(1f, 0.8f, 0.3f));
                    fx.particleGlow.Override(1f);
                    fx.particleDrift.Override(new Vector2(0f, 0.1f));
                    fx.particleFlutter.Override(0.3f);
                    fx.particleStretch.Override(0.4f);
                    fx.windDirection.Override(new Vector2(-1f, 0.25f));
                    fx.windStrength.Override(0.4f);
                    fx.gustStrength.Override(0.4f);
                    fx.hazeColor.Override(new Color(0.25f, 0.12f, 0.1f));
                    fx.hazeAmount.Override(0.03f);
                    fx.borderStyle.Override(WeatherBorderStyle.Scorch);
                    fx.borderWidth.Override(0.07f);
                    fx.borderOpacity.Override(0.32f);
                    fx.borderDistortion.Override(0.003f);
                    fx.borderDetail.Override(0.22f);
                    fx.borderTint.Override(new Color(0.1f, 0.04f, 0.03f));
                    fx.borderGlow.Override(new Color(1f, 0.42f, 0.1f));
                    fx.heatShimmer.Override(0.0012f);
                    fx.heatScale.Override(8f);
                    fx.heatSpeed.Override(0.6f);
                    wb.temperature.Override(4f);
                    ca.contrast.Override(4f);
                    ca.saturation.Override(0f);
                    ca.colorFilter.Override(Color.white);
                    vig.color.Override(new Color(0.25f, 0.04f, 0f));
                    vig.intensity.Override(0.14f);
                    bloom.threshold.Override(1f);
                    bloom.intensity.Override(0.2f);
                    bloom.tint.Override(new Color(1f, 0.85f, 0.7f));
                    break;
            }

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
            WeatherScreenFxFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is WeatherScreenFxFeature wf)
                    feature = wf;

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<WeatherScreenFxFeature>();
                feature.name = "WeatherScreenFx";
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

        private static void AssignToTheme(string theme, VolumeProfile profile, StringBuilder log)
        {
            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(StageThemeKit.PathFor(theme));
            if (kit == null)
            {
                log.AppendLine("theme kit missing: " + theme);
                return;
            }
            kit.screenFxProfile = profile;
            kit.ambientSeagulls = theme == SeagullTheme ? SeagullCount : 0;
            EditorUtility.SetDirty(kit);
            log.AppendLine("theme kit: " + theme + ".screenFxProfile set");
        }

        /// <summary>Built stage scenes whose layout uses the given theme.</summary>
        public static List<string> StageScenes(string theme)
        {
            var list = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:TextAsset", new[] { "Assets/_Project/Data/StageLayouts" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".json"))
                    continue;
                var text = System.IO.File.ReadAllText(path);
                if (!System.Text.RegularExpressions.Regex.IsMatch(text, "\"theme\"\\s*:\\s*\"" + theme + "\""))
                    continue;
                var id = System.IO.Path.GetFileNameWithoutExtension(path);
                var scene = $"Assets/_Project/Scenes/Stages/Stage_{id}.unity";
                if (System.IO.File.Exists(scene))
                    list.Add(scene);
            }
            list.Sort();
            return list;
        }

        /// <summary>Adds <see cref="StageScreenWeather"/> to the Lighting root of every built stage of the theme.
        /// Scenes are opened additively and closed again, so the scenes open in the editor are left untouched.</summary>
        private static void PatchStageScenes(string theme, VolumeProfile profile, StringBuilder log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                log.AppendLine("scenes skipped (Play Mode)");
                return;
            }
            foreach (var path in StageScenes(theme))
            {
                var loaded = SceneManager.GetSceneByPath(path);
                bool wasOpen = loaded.IsValid() && loaded.isLoaded;
                if (wasOpen && loaded.isDirty)
                {
                    log.AppendLine("scene skipped (unsaved changes): " + path);
                    continue;
                }
                var scene = wasOpen ? loaded : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    GameObject lighting = null;
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.name == "Lighting")
                            lighting = root;
                    if (lighting == null)
                    {
                        log.AppendLine("no Lighting root: " + path);
                        continue;
                    }
                    var w = lighting.GetComponent<StageScreenWeather>();
                    if (w == null)
                        w = lighting.AddComponent<StageScreenWeather>();
                    bool gullsOk = true;
                    if (theme == SeagullTheme && lighting.GetComponent<AmbientSeagulls>() == null)
                    {
                        lighting.AddComponent<AmbientSeagulls>().count = SeagullCount;
                        gullsOk = false;
                    }
                    if (w.profile == profile && gullsOk)
                    {
                        log.AppendLine("scene up to date: " + path);
                        continue;
                    }
                    w.profile = profile;
                    EditorUtility.SetDirty(w);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    log.AppendLine("scene patched: " + path);
                }
                finally
                {
                    if (!wasOpen)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }
        }
    }
}
