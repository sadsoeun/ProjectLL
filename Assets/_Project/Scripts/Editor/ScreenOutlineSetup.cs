using System.Collections.Generic;
using System.Linq;
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
    /// Subtle background outline (screen-space depth + normals edges). Idempotent:
    /// names Rendering Layer 4 "ScreenOutlineExclude", creates / refreshes the stage and world-map volume profiles,
    /// registers <see cref="ScreenOutlineFeature"/> on the URP renderers, puts the exclusion layer on every character
    /// prefab renderer (player, monsters, bosses) and adds a global outline Volume to 06_InGame and 05_WorldMap.
    /// Scenes are opened additively and closed again, so whatever scene is open in the editor is left untouched.
    /// </summary>
    public static class ScreenOutlineSetup
    {
        public const string StageProfilePath = "Assets/_Project/Data/PostProcess/ScreenOutline_Stage.asset";
        public const string WorldMapProfilePath = "Assets/_Project/Data/PostProcess/ScreenOutline_WorldMap.asset";
        public const string ShaderPath = "Assets/_Project/Shaders/FX_ScreenOutline.shader";
        public const string InGameScene = "Assets/_Project/Scenes/06_InGame.unity";
        public const string WorldMapScene = "Assets/_Project/Scenes/05_WorldMap.unity";
        public const string VolumeObjectName = "ScreenOutlineVolume";

        /// <summary>Rendering layer index read by ScreenOutlineMask.hlsl (SCREEN_OUTLINE_EXCLUDE_LAYER).</summary>
        public const int ExcludeLayerIndex = 4;
        public const string ExcludeLayerName = "ScreenOutlineExclude";
        public const uint ExcludeLayerBit = 1u << ExcludeLayerIndex;

        private static readonly string[] RendererPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset",
        };

        /// <summary>Folders whose prefabs are characters (own hull outline -> excluded from the screen outline).</summary>
        private static readonly string[] CharacterFolders =
        {
            "Assets/_Project/Art/Characters",
            "Assets/_Project/Prefabs/Player",
            "Assets/_Project/Prefabs/Monsters/Stage01",
            "Assets/_Project/Prefabs/Monsters/Stage02",
            "Assets/_Project/Prefabs/Monsters/Stage03",
            "Assets/_Project/Prefabs/Monsters/Stage04",
            "Assets/_Project/Prefabs/Monsters/Stage05",
            "Assets/_Project/Prefabs/Bosses",
        };

        [MenuItem("Tools/MyGame/Art/Setup Screen Outline")]
        public static void RunMenu() => Debug.Log(Run(true));

        public static string Run(bool patchScenes)
        {
            var log = new StringBuilder("[ScreenOutline]\n");
            EnsureRenderingLayerName(log);
            var stage = EnsureProfile(StageProfilePath, false, log);
            var map = EnsureProfile(WorldMapProfilePath, true, log);
            foreach (var path in RendererPaths)
                EnsureFeature(path, log);
            TagCharacterPrefabs(log);
            AssetDatabase.SaveAssets();
            if (patchScenes)
            {
                PatchScene(InGameScene, stage, log);
                PatchScene(WorldMapScene, map, log);
            }
            return log.ToString();
        }

        // ------------------------------------------------------------------ rendering layer

        private static void EnsureRenderingLayerName(StringBuilder log)
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            if (tagManager == null)
            {
                log.AppendLine("TagManager missing");
                return;
            }
            var so = new SerializedObject(tagManager);
            var layers = so.FindProperty("m_RenderingLayers");
            if (layers == null || !layers.isArray)
            {
                log.AppendLine("m_RenderingLayers not found (layer stays unnamed, bit " + ExcludeLayerIndex + ")");
                return;
            }
            while (layers.arraySize <= ExcludeLayerIndex)
            {
                layers.InsertArrayElementAtIndex(layers.arraySize);
                layers.GetArrayElementAtIndex(layers.arraySize - 1).stringValue = "";
            }
            var element = layers.GetArrayElementAtIndex(ExcludeLayerIndex);
            if (element.stringValue != ExcludeLayerName)
            {
                if (!string.IsNullOrEmpty(element.stringValue))
                    log.AppendLine("WARNING: rendering layer " + ExcludeLayerIndex + " was '" + element.stringValue + "', renamed");
                element.stringValue = ExcludeLayerName;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine("rendering layer " + ExcludeLayerIndex + " named " + ExcludeLayerName);
            }
            else
                log.AppendLine("rendering layer present: " + ExcludeLayerName);
        }

        // ------------------------------------------------------------------ profiles

        private static VolumeProfile EnsureProfile(string path, bool worldMap, StringBuilder log)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
                log.AppendLine("profile created: " + path);
            }
            else
                log.AppendLine("profile refreshed: " + path);

            var o = Get<ScreenOutline>(profile);
            ApplyDefaults(o, worldMap);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>Tuned defaults. Stage: side camera ~16 m from the lane. World map: high angled camera.</summary>
        public static void ApplyDefaults(ScreenOutline o, bool worldMap)
        {
            o.intensity.Override(worldMap ? 0.55f : 0.6f);
            o.thickness.Override(1f);
            o.tint.Override(new Color(0.32f, 0.3f, 0.36f, 1f));
            o.maxDarkening.Override(1f);
            o.depthThreshold.Override(worldMap ? 0.008f : 0.012f);
            o.depthWeight.Override(1f);
            o.normalThreshold.Override(0.3f);
            o.normalWeight.Override(worldMap ? 0.6f : 0.7f);
            o.foliageCreaseScale.Override(0.1f);
            o.softness.Override(1.5f);
            o.fadeStart.Override(worldMap ? 45f : 24f);
            o.fadeEnd.Override(worldMap ? 110f : 55f);
            o.nearFade.Override(1f);
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
            ScreenOutlineFeature feature = null;
            foreach (var f in data.rendererFeatures)
                if (f is ScreenOutlineFeature sf)
                    feature = sf;

            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<ScreenOutlineFeature>();
                feature.name = "ScreenOutline";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                {
                    var mapField = typeof(ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (mapField?.GetValue(data) is List<long> map)
                        map.Add(localId);
                }
                feature.SetActive(true);
                log.AppendLine("feature added: " + rendererPath);
            }
            else
                log.AppendLine("feature present: " + rendererPath + " (active=" + feature.isActive + ")");

            var so = new SerializedObject(feature);
            so.FindProperty("shader").objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
        }

        // ------------------------------------------------------------------ character exclusion

        /// <summary>Character prefab paths; nested *_Visual prefabs first so outer prefabs inherit instead of overriding.</summary>
        public static List<string> CharacterPrefabs()
        {
            var folders = CharacterFolders.Where(AssetDatabase.IsValidFolder).ToArray();
            return AssetDatabase.FindAssets("t:Prefab", folders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".prefab"))
                .Where(p => !p.StartsWith("Assets/_Project/Prefabs/Bosses/") || IsBossBody(System.IO.Path.GetFileNameWithoutExtension(p)))
                .Distinct()
                .OrderBy(p => p.Contains("_Visual") || p.StartsWith("Assets/_Project/Art/") ? 0 : 1)
                .ThenBy(p => p)
                .ToList();
        }

        private static bool IsBossBody(string name) => name.Contains("_BOSS_") || name.StartsWith("BossPlaceholder");

        private static void TagCharacterPrefabs(StringBuilder log)
        {
            int prefabs = 0, renderers = 0;
            foreach (var path in CharacterPrefabs())
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset == null || !asset.GetComponentsInChildren<Renderer>(true).Any(NeedsTag))
                    continue;
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = 0;
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!NeedsTag(r))
                            continue;
                        r.renderingLayerMask |= ExcludeLayerBit;
                        n++;
                    }
                    if (n > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        prefabs++;
                        renderers += n;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            log.AppendLine($"character prefabs tagged: {prefabs} ({renderers} renderers)");
        }

        private static bool NeedsTag(Renderer r) =>
            (r is MeshRenderer || r is SkinnedMeshRenderer) && (r.renderingLayerMask & ExcludeLayerBit) == 0;

        // ------------------------------------------------------------------ scenes

        private static void PatchScene(string path, VolumeProfile profile, StringBuilder log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                log.AppendLine("scene skipped (Play Mode): " + path);
                return;
            }
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedHere = false;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                openedHere = true;
            }
            else if (scene.isDirty)
            {
                log.AppendLine("scene skipped (unsaved changes): " + path);
                return;
            }
            try
            {
                GameObject go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == VolumeObjectName);
                if (go == null)
                {
                    go = new GameObject(VolumeObjectName);
                    SceneManager.MoveGameObjectToScene(go, scene);
                }
                var volume = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 5f;   // below the theme screen FX (10) and the perfect-dodge time stop (1000)
                volume.weight = 1f;
                volume.sharedProfile = profile;
                EditorUtility.SetDirty(volume);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.AppendLine("scene patched: " + path);
            }
            finally
            {
                if (openedHere)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
