using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Idempotent setup of scenes, prefabs, data assets and Build Settings for the scene flow.
    /// Menu: Tools/MyGame/Setup Scene Flow. Code entry point: MyGame.EditorTools.SceneFlowSetup.Run().
    /// </summary>
    public static partial class SceneFlowSetup
    {
        // ---- asset paths
        internal const string Root = "Assets/_Project";
        internal const string ScenesDir = Root + "/Scenes";
        internal const string StageScenesDir = Root + "/Scenes/Stages";
        internal const string UiPrefabDir = Root + "/Prefabs/UI";
        internal const string ResourcesDir = Root + "/Resources";
        internal const string StageDataDir = Root + "/Data/Stages";
        internal const string NodeDataDir = Root + "/Data/WorldMap";
        internal const string DatabasePath = Root + "/Data/StageDatabase.asset";
        internal const string MaterialDir = Root + "/Art/Materials";
        internal const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        internal const string SettingsPopupPath = UiPrefabDir + "/SettingsPopup.prefab";
        internal const string ConfirmPopupPath = UiPrefabDir + "/ConfirmPopup.prefab";
        internal const string GameSystemsPath = ResourcesDir + "/GameSystems.prefab";

        private static readonly StringBuilder Log = new StringBuilder();

        [MenuItem("Tools/MyGame/Setup Scene Flow")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[SceneFlowSetup] Stop Play Mode before running the setup.");
                return;
            }
            if (EditorApplication.isCompiling)
            {
                Debug.LogError("[SceneFlowSetup] Scripts are compiling. Try again after compilation.");
                return;
            }

            Log.Clear();
            try
            {
                SaveDirtyScenesQuietly();

                EnsureFolders();
                var mats = s_materials = CreateMaterials();
                var db = CreateDataAssets();
                // Persist new assets before NewScene() calls can unload unsaved in-memory data.
                AssetDatabase.SaveAssets();
                CreatePopupPrefabs();
                CreateGameSystemsPrefab(db);

                CreateLogoScene();
                CreateLanguageSelectScene();
                CreateTitleScene(mats);
                CreateLoadingScene();
                CreateWorldMapScene(db, mats);
                CreateInGameScene(mats);
                BuildStageScenesFromLayouts();

                RegisterBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorSceneManager.OpenScene(ScenesDir + "/01_Logo.unity", OpenSceneMode.Single);
                Debug.Log("[SceneFlowSetup] DONE\n" + Log);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneFlowSetup] FAILED: {e.Message}\n{e}\nProgress so far:\n{Log}");
                throw;
            }
        }

        // ------------------------------------------------------------------ helpers

        internal static void Note(string line) => Log.AppendLine("  - " + line);

        internal static void SaveDirtyScenesQuietly()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isDirty)
                    continue;
                if (!string.IsNullOrEmpty(s.path))
                {
                    EditorSceneManager.SaveScene(s);
                    Note($"saved modified scene before setup: {s.path}");
                }
                else
                {
                    Note("discarded an unsaved untitled scene");
                }
            }
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static void EnsureFolders()
        {
            foreach (var dir in new[]
            {
                ScenesDir, StageScenesDir, UiPrefabDir, ResourcesDir, StageDataDir, NodeDataDir,
                MaterialDir, Root + "/Prefabs/Systems", Root + "/Prefabs/Player", Root + "/Prefabs/Level",
                Root + "/Audio", Root + "/Localization",
            })
            {
                EnsureFolder(dir);
            }
            Note("folders ready under " + Root);
        }

        internal static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        internal static void SaveScene(Scene scene, string path)
        {
            // Bake the runtime gold theme so the Editor shows the same UI as Play Mode (Apply is idempotent).
            GoldUiTheme.ApplyToScene(scene);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException($"Failed to save scene '{path}'.");
            Note("scene: " + path);
        }

        internal static Scene NewScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Single mode unloads unused assets, destroying the materials this run created and
            // still references. Re-resolve them from disk (CreateMaterials is create-or-load).
            if (s_materials != null)
                s_materials.CopyFrom(CreateMaterials());
            return scene;
        }

        /// <summary>Reloads the StageDatabase after a NewScene() call has unloaded it.</summary>
        internal static StageDatabase ReloadDatabase()
        {
            var db = AssetDatabase.LoadAssetAtPath<StageDatabase>(DatabasePath);
            if (db == null)
                throw new InvalidOperationException("StageDatabase missing at " + DatabasePath);
            return db;
        }

        // ------------------------------------------------------------------ materials

        private static Materials s_materials;

        internal sealed class Materials
        {
            public Material Ground, Platform, Goal, MapGround, NodeLocked, NodeOpen, NodeCleared, Path, Player, Face, Flag;
            public PhysicsMaterial PlayerPhysics;

            public void CopyFrom(Materials o)
            {
                Ground = o.Ground; Platform = o.Platform; Goal = o.Goal; MapGround = o.MapGround;
                NodeLocked = o.NodeLocked; NodeOpen = o.NodeOpen; NodeCleared = o.NodeCleared;
                Path = o.Path; Player = o.Player; Face = o.Face; Flag = o.Flag; PlayerPhysics = o.PlayerPhysics;
            }
        }

        private static Materials CreateMaterials()
        {
            var m = new Materials
            {
                Ground = LitMaterial("M_Ground", new Color(0.35f, 0.62f, 0.30f)),
                Platform = LitMaterial("M_Platform", new Color(0.62f, 0.45f, 0.28f)),
                Goal = LitMaterial("M_Goal", new Color(1f, 0.85f, 0.2f)),
                MapGround = LitMaterial("M_MapGround", new Color(0.42f, 0.68f, 0.38f)),
                NodeLocked = LitMaterial("M_NodeLocked", new Color(0.25f, 0.25f, 0.28f)),
                NodeOpen = LitMaterial("M_NodeOpen", new Color(1f, 0.85f, 0.25f)),
                NodeCleared = LitMaterial("M_NodeCleared", new Color(0.25f, 0.8f, 0.4f)),
                Path = LitMaterial("M_Path", new Color(0.92f, 0.86f, 0.68f)),
                Player = LitMaterial("M_Player", new Color(0.3f, 0.6f, 1f)),
                Face = LitMaterial("M_PlayerFace", new Color(1f, 1f, 1f)),
                Flag = LitMaterial("M_Flag", new Color(0.9f, 0.2f, 0.2f)),
            };

            string pmPath = MaterialDir + "/PM_Player.asset";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);
            if (pm == null)
            {
                pm = new PhysicsMaterial("PM_Player");
                AssetDatabase.CreateAsset(pm, pmPath);
            }
            pm.dynamicFriction = 0f;
            pm.staticFriction = 0f;
            pm.frictionCombine = PhysicsMaterialCombine.Minimum;
            pm.bounciness = 0f;
            EditorUtility.SetDirty(pm);
            m.PlayerPhysics = pm;
            Note("materials ready in " + MaterialDir);
            return m;
        }

        internal static Material LitMaterial(string name, Color color)
        {
            string path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    throw new InvalidOperationException("URP Lit shader not found. Is the URP package installed?");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ data assets

        private static StageDatabase CreateDataAssets()
        {
            // World 1: four stages in a linear chain S1 -> S2 -> S3 -> S4, left to right on the 30 m wide map.
            string[] ids = { "W1_S1", "W1_S2", "W1_S3", "W1_S4" };
            Vector3[] positions =
            {
                new Vector3(-9f, 0f, -1f), new Vector3(-3f, 0f, 1f), new Vector3(3f, 0f, -1f), new Vector3(9f, 0f, 1f),
            };
            var stageList = new List<StageData>();
            var nodeList = new List<WorldMapNodeData>();
            for (int i = 0; i < ids.Length; i++)
            {
                var stage = CreateStageData(ids[i], $"stage.{ids[i]}.name", $"Stage_{ids[i]}");
                stageList.Add(stage);
                nodeList.Add(CreateNodeData(ids[i], stage,
                    i == 0 ? new string[0] : new[] { ids[i - 1] },
                    null, null,
                    i > 0 ? ids[i - 1] : null,
                    i < ids.Length - 1 ? ids[i + 1] : null,
                    positions[i]));
            }

            var db = LoadOrCreateAsset<StageDatabase>(DatabasePath);
            EditorWire.Set(db, "stages", stageList);
            EditorWire.Set(db, "nodes", nodeList);
            EditorWire.Set(db, "startNodeId", "W1_S1");
            EditorUtility.SetDirty(db);

            if (!db.Validate(out string report))
                throw new InvalidOperationException("StageDatabase validation failed:\n" + report);
            Note("data: StageData x4, WorldMapNodeData x4, StageDatabase (valid)");
            return db;
        }

        private static StageData CreateStageData(string id, string nameKey, string sceneName)
        {
            var asset = LoadOrCreateAsset<StageData>($"{StageDataDir}/StageData_{id}.asset");
            EditorWire.Set(asset, "stageId", id);
            EditorWire.Set(asset, "displayNameKey", nameKey);
            EditorWire.Set(asset, "sceneName", sceneName);
            EditorWire.Set(asset, "timeLimit", 0f);
            EditorWire.Set(asset, "collectibleCount", StageSession.SpecialSlotCount);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static WorldMapNodeData CreateNodeData(string id, StageData stage, string[] prerequisites,
            string up, string down, string left, string right, Vector3 position)
        {
            var asset = LoadOrCreateAsset<WorldMapNodeData>($"{NodeDataDir}/Node_{id}.asset");
            EditorWire.Set(asset, "nodeId", id);
            EditorWire.Set(asset, "stage", stage);
            EditorWire.Set(asset, "prerequisiteStageIds", prerequisites);
            EditorWire.Set(asset, "upNodeId", up ?? string.Empty);
            EditorWire.Set(asset, "downNodeId", down ?? string.Empty);
            EditorWire.Set(asset, "leftNodeId", left ?? string.Empty);
            EditorWire.Set(asset, "rightNodeId", right ?? string.Empty);
            EditorWire.Set(asset, "mapPosition", position);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        // ------------------------------------------------------------------ stage scenes from layouts

        private static void BuildStageScenesFromLayouts()
        {
            if (!StageLayoutBuilder.HasLayouts())
            {
                Debug.LogWarning($"[SceneFlowSetup] No stage layout files in {StageLayoutBuilder.LayoutsDir}; stage scenes were not built. Add layouts and run Tools/MyGame/Build Stages From Layouts.");
                Note("stage scenes: SKIPPED (no layouts)");
                return;
            }
            Note("stage scenes:" + System.Environment.NewLine + StageLayoutBuilder.BuildAll());
        }

        // ------------------------------------------------------------------ build settings

        private static void RegisterBuildSettings()
        {
            var paths = new[]
            {
                ScenesDir + "/01_Logo.unity",
                ScenesDir + "/02_LanguageSelect.unity",
                ScenesDir + "/03_Title.unity",
                ScenesDir + "/04_Loading.unity",
                ScenesDir + "/05_WorldMap.unity",
                ScenesDir + "/06_InGame.unity",
            };
            var list = new List<EditorBuildSettingsScene>();
            foreach (var p in paths)
            {
                if (AssetDatabase.AssetPathToGUID(p).Length == 0)
                    throw new InvalidOperationException($"Scene missing for Build Settings: {p}");
                list.Add(new EditorBuildSettingsScene(p, true));
            }

            // Stage scenes (built from layouts) follow 06_InGame, in database order.
            var db = ReloadDatabase();
            foreach (var stage in db.Stages)
            {
                string p = $"{StageScenesDir}/{stage.SceneName}.unity";
                if (AssetDatabase.AssetPathToGUID(p).Length == 0)
                    Debug.LogWarning($"[SceneFlowSetup] Stage scene missing (no layout built yet?): {p}");
                else
                    list.Add(new EditorBuildSettingsScene(p, true));
            }
            EditorBuildSettings.scenes = list.ToArray();

            var sb = new StringBuilder("Build Settings:");
            for (int i = 0; i < list.Count; i++)
                sb.Append($" [{i}]{System.IO.Path.GetFileNameWithoutExtension(list[i].path)}");
            Note(sb.ToString() + " (SampleScene excluded)");
        }

        // ------------------------------------------------------------------ shared scene helpers

        internal static Camera CreateCamera(string name, Color background, Vector3 position, Vector3 euler, float fov = 40f)
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            go.AddComponent<AudioListener>();
            go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            return cam;
        }

        internal static Light CreateDirectionalLight(Transform parent = null)
        {
            var go = new GameObject("Directional Light");
            if (parent != null)
                go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            return light;
        }

        /// <summary>Flat ambient light so scenes are readable without a skybox or baked lighting.</summary>
        internal static void ConfigureAmbient()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.65f);
        }

        internal static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 position,
            Vector3 scale, Material material, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (material != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider)
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        internal static InputActionAsset LoadInputActions()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (asset == null)
                throw new InvalidOperationException($"InputActionAsset not found at {InputActionsPath}.");
            return asset;
        }
    }
}
