using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Replaces the primitive placeholder looks with the Blender art (sources in ArtSource):
    ///   Bosses/SD_BOSS_SunGuardian_FX.blend - sand gust (Sun Guardian shockwave), sand puff, sandstone chunk, landing marker
    ///   GreenMeadow/G07_G09_Gimmicks.blend  - log raft mover, chestnut burr faller, thorn bramble spikes
    /// Boss hazard prefabs keep their GUID, collider and BossHazard; only the visual child is swapped. The Green Meadow
    /// pieces become theme kit slots (moverVisual / fallerVisual / spikeVisual); rebuild the W1 stages afterwards.
    /// Models are imported with a 180 degree root yaw; the "Model" child turns it back so Blender +X = Unity +X and the
    /// Blender front (-Y) faces the side camera.
    ///   Tools/MyGame/Art/Build Hazard Art
    /// </summary>
    public static class HazardArtSetup
    {
        private const string SdFx = "Assets/_Project/Art/Characters/Bosses/SD_BOSS_SunGuardian/FX";
        private const string BossCommon = "Assets/_Project/Art/Characters/Bosses/Common";
        private const string GmModels = "Assets/_Project/Art/Environment/GreenMeadow/Models";
        private const string MatDir = "Assets/_Project/Art/Materials/Hazards";
        private const string BossPrefabs = "Assets/_Project/Prefabs/Bosses";
        private const string GmPrefabs = "Assets/_Project/Prefabs/Environment/GreenMeadow";
        private const string ToonShader = "MyGame/Toon/Lit";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";

        // Blender base colours (linear). unlit = flat graphic colour (wind, warning marker).
        private static readonly Dictionary<string, (Color c, bool unlit)> Materials = new()
        {
            ["FX_Sand"] = (new Color(0.86f, 0.55f, 0.22f), false),
            ["FX_SandLight"] = (new Color(1f, 0.80f, 0.48f), false),
            ["FX_Wind"] = (new Color(1f, 0.95f, 0.82f), true),
            ["FX_Grit"] = (new Color(0.55f, 0.32f, 0.12f), false),
            ["SD_Sandstone"] = (new Color(0.80f, 0.52f, 0.26f), false),
            ["SD_SandstoneDark"] = (new Color(0.52f, 0.30f, 0.14f), false),
            ["SD_SunGold"] = (new Color(1f, 0.72f, 0.12f), false),
            ["FX_ShadowDark"] = (new Color(0.06f, 0.04f, 0.06f), true),
            ["FX_WarnRed"] = (new Color(1f, 0.16f, 0.10f), true),
            ["FX_WarnYellow"] = (new Color(1f, 0.82f, 0.18f), true),
            ["GM_Wood"] = (new Color(0.50f, 0.29f, 0.13f), false),
            ["GM_WoodDark"] = (new Color(0.30f, 0.16f, 0.07f), false),
            ["GM_WoodCut"] = (new Color(0.72f, 0.50f, 0.24f), false),
            ["GM_NutLight"] = (new Color(0.80f, 0.62f, 0.38f), false),
            ["GM_Rope"] =(new Color(0.86f, 0.72f, 0.45f), false),
            ["GM_Leaf"] = (new Color(0.30f, 0.62f, 0.18f), false),
            ["GM_LeafLight"] = (new Color(0.52f, 0.80f, 0.28f), false),
            ["GM_Thorn"] = (new Color(0.25f, 0.08f, 0.12f), false),
            ["GM_ThornTip"] = (new Color(1f, 0.12f, 0.06f), false),
            ["GM_Burr"] = (new Color(0.45f, 0.66f, 0.20f), false),
            ["GM_BurrSpike"] = (new Color(0.88f, 0.85f, 0.45f), false),
            ["GM_Nut"] = (new Color(0.48f, 0.25f, 0.10f), false),
        };

        [MenuItem("Tools/MyGame/Art/Build Hazard Art")]
        public static void Menu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building hazard art.");
            var log = new StringBuilder("[HazardArtSetup]\n");
            SceneFlowSetup.EnsureFolder(MatDir);
            var mats = Materials.ToDictionary(kv => kv.Key, kv => EnsureMaterial(kv.Key, kv.Value.c, kv.Value.unlit));
            string[] models =
            {
                $"{SdFx}/SD_FX_SandGust.fbx", $"{SdFx}/SD_FX_SandPuff.fbx", $"{SdFx}/SD_FX_SandstoneChunk.fbx",
                $"{BossCommon}/FX_BossShadowMarker.fbx",
                $"{GmModels}/G07_LogRaftMover.fbx", $"{GmModels}/G08_ChestnutBurrFaller.fbx", $"{GmModels}/G09_ThornBrambleSpikes.fbx",
            };
            foreach (var m in models)
                Remap(m, mats, log);

            // --- Sun Guardian: sand puff (trail), sand gust wave, sandstone chunk; both bosses: landing marker
            string puffPath = $"{BossPrefabs}/FX_SandPuff.prefab";
            var puffRoot = new GameObject("FX_SandPuff");
            try
            {
                AddModel(puffRoot.transform, $"{SdFx}/SD_FX_SandPuff.fbx");
                var fx = puffRoot.AddComponent<CartoonFx>();
                EditorWire.Set(fx, "style", (int)CartoonFx.Style.Puff);
                EditorWire.Set(fx, "lifetime", 0.5f);
                Save(puffRoot, puffPath, log);
            }
            finally { UnityEngine.Object.DestroyImmediate(puffRoot); }
            var puff = AssetDatabase.LoadAssetAtPath<CartoonFx>(puffPath);

            EditPrefab($"{BossPrefabs}/BossWave.prefab", log, root =>
            {
                RemoveChild(root, "Visual");
                var model = AddModel(root, $"{SdFx}/SD_FX_SandGust.fbx");
                var fx = root.GetComponent<SandGustFx>() ?? root.gameObject.AddComponent<SandGustFx>();
                EditorWire.Set(fx, "model", model);
                EditorWire.Set(fx, "swirls", Children(model, "Swirl"));
                EditorWire.Set(fx, "crest", Find(model, "Crest"));
                EditorWire.Set(fx, "streaks", Children(model, "Streak"));
                EditorWire.Set(fx, "puffPrefab", puff);
            });

            EditPrefab($"{BossPrefabs}/BossFallingChunk.prefab", log, root =>
            {
                RemoveChild(root, "Visual");
                var model = AddModel(root, $"{SdFx}/SD_FX_SandstoneChunk.fbx");
                model.localPosition = Vector3.zero;
                var spin = model.gameObject.AddComponent<SpinFx>();
                EditorWire.Set(spin, "degreesPerSecond", new Vector3(70f, 140f, 35f));
            });

            EditPrefab($"{BossPrefabs}/BossShadowMarker.prefab", log, root =>
            {
                RemoveChild(root, "Disc");
                var model = AddModel(root, $"{BossCommon}/FX_BossShadowMarker.fbx");
                var spin = model.gameObject.AddComponent<SpinFx>();
                EditorWire.Set(spin, "degreesPerSecond", new Vector3(0f, 60f, 0f));
            });

            // --- Green Meadow gimmick art (theme kit slots)
            var mover = MakeGmPrefab("G07_LogRaftMover", log, (root, model) => { });
            // Faller art is placed by the stage builder at (-0.75, 0.5, 0) with scale (0.5, 2.5, 0.4) (sized for the
            // 3 x 0.4 x 4 breakaway platform). The model child undoes that so the burr sits centred at 0.85 size.
            var faller = MakeGmPrefab("G08_ChestnutBurrFaller", log, (root, model) =>
            {
                const float k = 0.85f;
                model.localPosition = new Vector3(1.5f, -0.2f, 0f);
                model.localScale = new Vector3(k / 0.5f, k / 2.5f, k / 0.4f);
            });
            var spikes = MakeGmPrefab("G09_ThornBrambleSpikes", log, (root, model) => { });

            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(StageThemeKit.PathFor("GreenMeadow"))
                      ?? throw new InvalidOperationException("GreenMeadow theme kit not found.");
            kit.moverVisual = mover;
            kit.fallerVisual = faller;
            kit.spikeVisual = spikes;
            EditorUtility.SetDirty(kit);
            log.AppendLine("  GreenMeadow kit: moverVisual, fallerVisual, spikeVisual set");
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static GameObject MakeGmPrefab(string name, StringBuilder log, Action<Transform, Transform> adjust)
        {
            var root = new GameObject(name);
            try
            {
                var model = AddModel(root.transform, $"{GmModels}/{name}.fbx");
                adjust(root.transform, model);
                string path = $"{GmPrefabs}/{name}.prefab";
                Save(root, path, log);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        // ------------------------------------------------------------------ helpers

        private static Material EnsureMaterial(string name, Color linear, bool unlit)
        {
            string path = $"{MatDir}/M_{name}.mat";
            var shader = Shader.Find(unlit ? UnlitShader : ToonShader)
                         ?? throw new InvalidOperationException("shader not found");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_" + name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", new Color(Mathf.LinearToGammaSpace(linear.r), Mathf.LinearToGammaSpace(linear.g), Mathf.LinearToGammaSpace(linear.b), 1f));
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void Remap(string path, Dictionary<string, Material> mats, StringBuilder log)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter
                      ?? throw new InvalidOperationException("model not found (refresh the AssetDatabase?): " + path);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            var slots = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Renderer>()
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
            foreach (var key in imp.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name))
                if (!slots.Contains(key)) slots.Add(key);
            var missing = new List<string>();
            foreach (string slot in slots)
            {
                string baseName = slot.Contains('.') ? slot.Substring(0, slot.IndexOf('.')) : slot;
                if (baseName.StartsWith("M_", StringComparison.Ordinal) && !mats.ContainsKey(baseName))
                    baseName = baseName.Substring(2); // already remapped to M_<slot> on an earlier run
                if (mats.TryGetValue(baseName, out var mat))
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), mat);
                else
                    missing.Add(slot);
            }
            imp.SaveAndReimport();
            log.AppendLine($"  {System.IO.Path.GetFileName(path)}: {slots.Count - missing.Count} slots" + (missing.Count > 0 ? " MISSING " + string.Join(",", missing) : ""));
        }

        private static Transform AddModel(Transform parent, string fbx)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx) ?? throw new InvalidOperationException("model not found: " + fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            inst.name = "Model";
            inst.transform.SetParent(parent, false);
            inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * inst.transform.localRotation;
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(c);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                r.lightProbeUsage = LightProbeUsage.Off;
                bool flat = r.sharedMaterials.All(m => m != null && m.shader.name == UnlitShader);
                r.shadowCastingMode = flat ? ShadowCastingMode.Off : ShadowCastingMode.On;
                r.receiveShadows = !flat;
            }
            return inst.transform;
        }

        private static Transform Find(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name)
            ?? throw new InvalidOperationException($"{root.name} has no child '{name}'");

        private static Transform[] Children(Transform root, string prefix) =>
            root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal)).ToArray();

        private static void RemoveChild(Transform root, string name)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                var c = root.GetChild(i);
                if (c.name == name || c.name == "Model")
                    UnityEngine.Object.DestroyImmediate(c.gameObject);
            }
        }

        private static void EditPrefab(string path, StringBuilder log, Action<Transform> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok)
                    throw new InvalidOperationException("Failed to save " + path);
                log.AppendLine("  updated " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void Save(GameObject root, string path, StringBuilder log)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            if (!ok)
                throw new InvalidOperationException("Failed to save " + path);
            log.AppendLine("  saved " + path);
        }
    }
}
