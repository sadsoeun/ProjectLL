using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Region lighting for 05_WorldMap: one WorldMapLightingPreset + grading VolumeProfile per world (W1..W5) and per
    /// boss node (W1_B..W5_B), a MapLighting object with WorldMapLightingDirector and one global Volume per preset,
    /// and camera post-processing + SMAA. Idempotent. Existing presets/profiles keep their (possibly hand-tuned) values;
    /// "Reset Lighting Presets" rewrites them from the defaults below. Called by WorldMapBuilder and SceneFlowSetup.
    /// </summary>
    public static class WorldMapLightingSetup
    {
        private const string ScenePath = "Assets/_Project/Scenes/05_WorldMap.unity";
        private const string PresetDir = "Assets/_Project/Data/WorldMap/Lighting";
        private const string ProfileDir = "Assets/_Project/Data/PostProcess/WorldMap";
        private const string RootName = "MapLighting";

        /// <summary>Default look of one region. Colours are sRGB like the Inspector.</summary>
        private sealed class Spec
        {
            public string id, mood;
            public Color sun; public float sunIntensity, pitch, yaw, shadow;
            public Color amSky, amEq, amGround;
            public Color fog; public float fogStart, fogEnd;
            public Color sky;
            // grading
            public float exposure, contrast, saturation, temperature, tint;
            public Color filter = Color.white;
            public Vector4 shadows = new Vector4(1f, 1f, 1f, 0f), midtones = new Vector4(1f, 1f, 1f, 0f), highlights = new Vector4(1f, 1f, 1f, 0f);
            public float bloomThreshold = 1f, bloomIntensity = 0.25f, bloomScatter = 0.6f;
            public Color bloomTint = Color.white;
            public float vignette = 0.18f, vignetteSmooth = 0.4f;
            public Color vignetteColor = new Color(0.10f, 0.12f, 0.20f);
        }

        private static readonly Spec[] Worlds =
        {
            new Spec // Green Meadow: clear late afternoon, warm sun, light sky-blue haze
            {
                id = "W1", mood = "Green Meadow - clear afternoon",
                sun = new Color(1f, 0.95f, 0.86f), sunIntensity = 1.1f, pitch = 42f, yaw = 325f, shadow = 0.72f,
                amSky = new Color(0.54f, 0.60f, 0.72f), amEq = new Color(0.50f, 0.53f, 0.54f), amGround = new Color(0.38f, 0.36f, 0.30f),
                fog = new Color(0.72f, 0.84f, 0.96f), fogStart = 8f, fogEnd = 110f, sky = new Color(0.55f, 0.78f, 0.97f),
                exposure = -0.05f, contrast = 12f, saturation = 0f, temperature = 3f, tint = 0f, filter = new Color(1f, 1f, 0.98f),
                shadows = new Vector4(0.96f, 0.96f, 1.08f, 0f), highlights = new Vector4(1.03f, 1.01f, 0.97f, 0f),
                bloomIntensity = 0.25f,
            },
            new Spec // Sun Dunes: hard noon sun, high contrast, sandy haze
            {
                id = "W2", mood = "Sun Dunes - harsh noon",
                sun = new Color(1f, 0.96f, 0.88f), sunIntensity = 1.2f, pitch = 62f, yaw = 330f, shadow = 0.82f,
                amSky = new Color(0.58f, 0.62f, 0.72f), amEq = new Color(0.62f, 0.56f, 0.46f), amGround = new Color(0.50f, 0.42f, 0.32f),
                fog = new Color(0.92f, 0.86f, 0.74f), fogStart = 10f, fogEnd = 100f, sky = new Color(0.70f, 0.80f, 0.93f),
                exposure = -0.1f, contrast = 18f, saturation = -4f, temperature = 6f, tint = 0f, filter = new Color(1f, 0.99f, 0.96f),
                shadows = new Vector4(0.98f, 0.95f, 1.06f, 0f), highlights = new Vector4(1.04f, 1.01f, 0.95f, 0f),
                bloomIntensity = 0.3f, bloomThreshold = 0.95f, vignette = 0.2f, vignetteColor = new Color(0.25f, 0.14f, 0.06f),
            },
            new Spec // Frost Peaks: cold blue-white, low saturation, dense fog
            {
                id = "W3", mood = "Frost Peaks - cold overcast",
                sun = new Color(0.92f, 0.96f, 1f), sunIntensity = 1.05f, pitch = 35f, yaw = 320f, shadow = 0.62f,
                amSky = new Color(0.62f, 0.70f, 0.84f), amEq = new Color(0.58f, 0.64f, 0.74f), amGround = new Color(0.50f, 0.55f, 0.64f),
                fog = new Color(0.80f, 0.87f, 0.96f), fogStart = 2f, fogEnd = 70f, sky = new Color(0.64f, 0.76f, 0.91f),
                exposure = -0.05f, contrast = 10f, saturation = -10f, temperature = -12f, tint = 2f, filter = new Color(0.97f, 0.99f, 1f),
                shadows = new Vector4(0.94f, 0.98f, 1.10f, 0f), highlights = new Vector4(0.99f, 1f, 1.03f, 0f),
                bloomIntensity = 0.35f, bloomThreshold = 0.95f, vignette = 0.22f, vignetteColor = new Color(0.15f, 0.20f, 0.32f),
            },
            new Spec // Coral Coast: bright teal bounce light, fresh
            {
                id = "W4", mood = "Coral Coast - bright seaside",
                sun = new Color(1f, 0.97f, 0.92f), sunIntensity = 1.1f, pitch = 52f, yaw = 335f, shadow = 0.68f,
                amSky = new Color(0.50f, 0.66f, 0.78f), amEq = new Color(0.48f, 0.62f, 0.64f), amGround = new Color(0.34f, 0.44f, 0.44f),
                fog = new Color(0.66f, 0.88f, 0.95f), fogStart = 16f, fogEnd = 130f, sky = new Color(0.45f, 0.78f, 0.96f),
                exposure = -0.1f, contrast = 14f, saturation = 4f, temperature = -4f, tint = -3f, filter = new Color(0.98f, 1f, 1f),
                shadows = new Vector4(0.94f, 1.0f, 1.06f, 0f), highlights = new Vector4(1.02f, 1.01f, 0.98f, 0f),
                bloomIntensity = 0.3f, vignette = 0.16f, vignetteColor = new Color(0.04f, 0.16f, 0.22f),
            },
            new Spec // Ember Fortress: dusk, red rim light, dark violet shadows, lava bounce from below
            {
                id = "W5", mood = "Ember Fortress - dusk",
                sun = new Color(1f, 0.62f, 0.42f), sunIntensity = 1.2f, pitch = 24f, yaw = 300f, shadow = 0.8f,
                amSky = new Color(0.40f, 0.32f, 0.50f), amEq = new Color(0.56f, 0.34f, 0.32f), amGround = new Color(0.48f, 0.24f, 0.16f),
                fog = new Color(0.55f, 0.30f, 0.30f), fogStart = 4f, fogEnd = 85f, sky = new Color(0.40f, 0.25f, 0.30f),
                exposure = -0.05f, contrast = 20f, saturation = 6f, temperature = 18f, tint = 4f, filter = new Color(1f, 0.95f, 0.92f),
                shadows = new Vector4(0.92f, 0.88f, 1.12f, 0f), highlights = new Vector4(1.05f, 0.99f, 0.93f, 0f),
                bloomIntensity = 0.6f, bloomThreshold = 0.9f, bloomTint = new Color(1f, 0.85f, 0.7f),
                vignette = 0.3f, vignetteColor = new Color(0.15f, 0.03f, 0.05f),
            },
        };

        /// <summary>Boss nodes: the world's look, darker and more dramatic.</summary>
        private static Spec BossOf(Spec w)
        {
            return new Spec
            {
                id = w.id + "_B", mood = w.mood + " (boss)",
                sun = w.sun, sunIntensity = w.sunIntensity * 0.85f, pitch = Mathf.Max(15f, w.pitch - 8f), yaw = w.yaw, shadow = Mathf.Min(1f, w.shadow + 0.1f),
                amSky = w.amSky * 0.8f, amEq = w.amEq * 0.8f, amGround = w.amGround * 0.8f,
                fog = w.fog * 0.85f, fogStart = w.fogStart - 2f, fogEnd = w.fogEnd * 0.85f, sky = w.sky * 0.8f,
                exposure = w.exposure - 0.15f, contrast = w.contrast + 8f, saturation = w.saturation + 4f,
                temperature = w.temperature, tint = w.tint, filter = w.filter,
                shadows = w.shadows, midtones = w.midtones, highlights = w.highlights,
                bloomThreshold = w.bloomThreshold, bloomIntensity = w.bloomIntensity + 0.1f, bloomScatter = w.bloomScatter, bloomTint = w.bloomTint,
                vignette = w.vignette + 0.12f, vignetteSmooth = w.vignetteSmooth, vignetteColor = w.vignetteColor,
            };
        }

        private static IEnumerable<Spec> AllSpecs()
        {
            foreach (var w in Worlds)
                yield return w;
            foreach (var w in Worlds)
                yield return BossOf(w);
        }

        [MenuItem("Tools/MyGame/World Map/Setup Region Lighting")]
        public static void Menu() => Debug.Log(RunOnScene(false));

        [MenuItem("Tools/MyGame/World Map/Reset Lighting Presets To Defaults")]
        public static void ResetMenu()
        {
            if (EditorUtility.DisplayDialog("Reset world map lighting",
                    "Rewrite all world map lighting presets and grading profiles with the default values?", "Reset", "Cancel"))
                Debug.Log(RunOnScene(true));
        }

        /// <summary>Opens 05_WorldMap, creates/updates assets and scene wiring, saves. Returns a report.</summary>
        public static string RunOnScene(bool resetValues)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before setting up world map lighting.");
            SceneFlowSetup.SaveDirtyScenesQuietly();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string report = Apply(resetValues);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>Applies to the open world map scene (does not save it).</summary>
        public static string Apply(bool resetValues = false)
        {
            var log = new StringBuilder("[WorldMapLightingSetup]\n");
            EnsureFolder(PresetDir);
            EnsureFolder(ProfileDir);

            var regions = new List<WorldMapLightingDirector.Region>();
            foreach (var spec in AllSpecs())
            {
                var preset = LoadOrCreatePreset(spec, resetValues, log);
                regions.Add(new WorldMapLightingDirector.Region { nodeIdPrefix = spec.id, preset = preset });
            }

            // Scene objects.
            var root = GameObject.Find(RootName) ?? new GameObject(RootName);
            if (!root.TryGetComponent(out WorldMapLightingDirector director))
                director = root.AddComponent<WorldMapLightingDirector>();
            var player = UnityEngine.Object.FindFirstObjectByType<MapPlayer>()
                         ?? throw new InvalidOperationException("05_WorldMap has no MapPlayer.");
            var cam = Camera.main ?? throw new InvalidOperationException("05_WorldMap has no MainCamera.");
            Light sun = null;
            foreach (var l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
                throw new InvalidOperationException("05_WorldMap has no directional light.");

            var so = new SerializedObject(director);
            so.FindProperty("player").objectReferenceValue = player;
            so.FindProperty("sun").objectReferenceValue = sun;
            so.FindProperty("targetCamera").objectReferenceValue = cam;
            var arr = so.FindProperty("regions");
            arr.arraySize = regions.Count;
            for (int i = 0; i < regions.Count; i++)
            {
                var el = arr.GetArrayElementAtIndex(i);
                el.FindPropertyRelative("nodeIdPrefix").stringValue = regions[i].nodeIdPrefix;
                el.FindPropertyRelative("preset").objectReferenceValue = regions[i].preset;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // One global volume per preset (the director re-binds by profile at runtime).
            var keep = new HashSet<GameObject>();
            for (int i = 0; i < regions.Count; i++)
            {
                var preset = regions[i].preset;
                string name = "Volume_" + preset.name;
                var t = root.transform.Find(name);
                var go = t != null ? t.gameObject : new GameObject(name);
                go.transform.SetParent(root.transform, false);
                if (!go.TryGetComponent(out Volume v))
                    v = go.AddComponent<Volume>();
                v.isGlobal = true;
                v.sharedProfile = preset.profile;
                v.priority = 10f + i;
                v.weight = 0f;
                keep.Add(go);
            }
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i).gameObject;
                if (!keep.Contains(child) && child.GetComponent<Volume>() != null)
                    UnityEngine.Object.DestroyImmediate(child);
            }
            log.AppendLine($"  scene: {RootName} with director, {regions.Count} region volumes");

            // Camera: post-processing (grading) + SMAA. HDR so the lava / bloom has headroom.
            if (!cam.TryGetComponent(out UniversalAdditionalCameraData data))
                data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            cam.allowHDR = true;
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(cam);
            log.AppendLine("  camera: post-processing on, SMAA high");

            // Preview the start region in the editor (runtime re-applies from the avatar position).
            director.ApplyAt(player.transform.position, true);
            EditorUtility.SetDirty(director);
            return log.ToString();
        }

        private static WorldMapLightingPreset LoadOrCreatePreset(Spec spec, bool reset, StringBuilder log)
        {
            string path = $"{PresetDir}/WML_{spec.id}.asset";
            var preset = AssetDatabase.LoadAssetAtPath<WorldMapLightingPreset>(path);
            bool created = preset == null;
            if (created)
            {
                preset = ScriptableObject.CreateInstance<WorldMapLightingPreset>();
                AssetDatabase.CreateAsset(preset, path);
            }
            if (created || reset)
            {
                preset.sunColor = spec.sun;
                preset.sunIntensity = spec.sunIntensity;
                preset.sunPitch = spec.pitch;
                preset.sunYaw = spec.yaw;
                preset.shadowStrength = spec.shadow;
                preset.ambientSky = spec.amSky;
                preset.ambientEquator = spec.amEq;
                preset.ambientGround = spec.amGround;
                preset.fogColor = spec.fog;
                preset.fogStartBeyondAvatar = spec.fogStart;
                preset.fogEndBeyondAvatar = spec.fogEnd;
                preset.skyColor = spec.sky;
            }
            var profile = LoadOrCreateProfile(spec, reset, out bool profileCreated);
            preset.profile = profile;
            EditorUtility.SetDirty(preset);
            log.AppendLine($"  {spec.id}: {(created ? "created" : reset ? "reset" : "kept")} preset, " +
                           $"{(profileCreated ? "created" : reset ? "reset" : "kept")} profile  ({spec.mood})");
            return preset;
        }

        private static VolumeProfile LoadOrCreateProfile(Spec spec, bool reset, out bool created)
        {
            string path = $"{ProfileDir}/WMPP_{spec.id}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            created = profile == null;
            if (created)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            else if (!reset)
            {
                return profile;
            }
            else
            {
                foreach (var c in profile.components.ToArray())
                    UnityEngine.Object.DestroyImmediate(c, true);
                profile.components.Clear();
            }

            // Every profile overrides the same parameters so the director's weighted layering is an exact blend.
            var tone = Add<Tonemapping>(profile);
            tone.mode.value = TonemappingMode.None; // the toon albedo is authored for display colours

            var ca = Add<ColorAdjustments>(profile);
            ca.postExposure.value = spec.exposure;
            ca.contrast.value = spec.contrast;
            ca.colorFilter.value = spec.filter;
            ca.hueShift.value = 0f;
            ca.saturation.value = spec.saturation;

            var wb = Add<WhiteBalance>(profile);
            wb.temperature.value = spec.temperature;
            wb.tint.value = spec.tint;

            var smh = Add<ShadowsMidtonesHighlights>(profile);
            smh.shadows.value = spec.shadows;
            smh.midtones.value = spec.midtones;
            smh.highlights.value = spec.highlights;

            var bloom = Add<Bloom>(profile);
            bloom.threshold.value = spec.bloomThreshold;
            bloom.intensity.value = spec.bloomIntensity;
            bloom.scatter.value = spec.bloomScatter;
            bloom.tint.value = spec.bloomTint;

            var vig = Add<Vignette>(profile);
            vig.color.value = spec.vignetteColor;
            vig.center.value = new Vector2(0.5f, 0.5f);
            vig.intensity.value = spec.vignette;
            vig.smoothness.value = spec.vignetteSmooth;
            vig.rounded.value = false;

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(true);
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
