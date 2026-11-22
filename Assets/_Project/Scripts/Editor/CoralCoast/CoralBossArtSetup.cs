using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.EditorTools
{
    /// <summary>
    /// World 4 boss stage art, after `Import Bosses` has built the Coral Hermit King (9004) prefab and its coral-chunk hazard:
    /// - BossWave_Coral (low foam wave, CC_FX_FoamWave, foam puffs) -> BossData 9004 wavePrefab
    /// - BossWall_CoralCoast / BossGate_CoralCoast (arena end wall and entrance gate) -> kit arenaWallVisual / arenaGateVisual
    /// Idempotent (prefab GUIDs kept). Menu: Tools/MyGame/Art/Build Coral Coast Boss Art. Rebuild the W4 stages afterwards.
    /// </summary>
    public static class CoralBossArtSetup
    {
        private const string BossArt = "Assets/_Project/Art/Characters/Bosses/CC_BOSS_CoralHermitKing";
        private const string ArenaArt = "Assets/_Project/Art/Environment/BossArena";
        private const string ArenaPrefabs = "Assets/_Project/Prefabs/Environment/BossArena";
        private const string BossPrefabs = "Assets/_Project/Prefabs/Bosses";
        private const string HazardMats = "Assets/_Project/Art/Materials/Hazards";
        private const string ToonShader = "MyGame/Toon/Lit";

        [MenuItem("Tools/MyGame/Art/Build Coral Coast Boss Art")]
        public static void Menu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            var log = new StringBuilder("[CoralBossArtSetup]\n");
            var data = BossCatalog.Load(9004) ?? throw new InvalidOperationException("Boss_9004 missing (Tools/MyGame/Bosses/Create Default Boss Data And Prefabs).");

            // --- foam puff (wave trail): FX_SandPuff with a sea-foam material
            string puffPath = $"{BossPrefabs}/FX_FoamPuff.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(puffPath) == null)
                AssetDatabase.CopyAsset($"{BossPrefabs}/FX_SandPuff.prefab", puffPath);
            var foamMat = ColorMaterial($"{HazardMats}/M_FX_FoamPuff.mat", new Color(0.9f, 0.98f, 1f));
            EditPrefab(puffPath, root =>
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(foamMat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            });
            var puff = AssetDatabase.LoadAssetAtPath<CartoonFx>(puffPath);
            log.AppendLine("  FX_FoamPuff");

            // --- low foam shockwave
            var waveMat = TexturedMaterial("CC_FX_FoamWave");
            string wavePath = $"{BossPrefabs}/BossWave_Coral.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(wavePath) == null)
                AssetDatabase.CopyAsset($"{BossPrefabs}/BossWave.prefab", wavePath);
            EditPrefab(wavePath, root =>
            {
                RemoveChildren(root, "Visual", "Model");
                var model = AddModel(root, $"{BossArt}/CC_FX_FoamWave.fbx", waveMat);
                model.localScale = new Vector3(1f, 0.95f, 1f); // foam crest over the 1 m (jumpable) hit box
                var fx = root.GetComponent<SandGustFx>() ?? root.gameObject.AddComponent<SandGustFx>();
                EditorWire.Set(fx, "model", model);
                EditorWire.Set(fx, "swirls", Array.Empty<Transform>());
                EditorWire.Set(fx, "crest", (Transform)null);
                EditorWire.Set(fx, "streaks", Array.Empty<Transform>());
                EditorWire.Set(fx, "puffPrefab", puff);
                EditorWire.Set(fx, "leadOffset", 0f);
            });
            data.wavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wavePath);
            data.markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BossPrefabs}/BossShadowMarker.prefab");
            EditorUtility.SetDirty(data);
            log.AppendLine("  BossWave_Coral -> Boss_9004.wavePrefab; falling = " + (data.fallingPrefab != null ? data.fallingPrefab.name : "NONE (run Import Bosses)"));

            // --- arena wall and gate
            var wall = ArenaPrefab("BossWall_CoralCoast", log);
            var gate = ArenaPrefab("BossGate_CoralCoast", log);

            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(StageThemeKit.PathFor("CoralCoast"))
                      ?? throw new InvalidOperationException("CoralCoast theme kit not found.");
            kit.arenaWallVisual = wall;
            kit.arenaGateVisual = gate;
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            log.AppendLine("  CoralCoast kit: arenaWallVisual, arenaGateVisual set");
            return log.ToString();
        }

        // ------------------------------------------------------------------ arena

        /// <summary>FBX materials (M_BW_CoralCoast_*) become toon materials with the colour authored in Blender; the prefab is
        /// the model rotated 180 deg around Y like the GreenMeadow / SunDunes walls.</summary>
        private static GameObject ArenaPrefab(string name, StringBuilder log)
        {
            string fbx = $"{ArenaArt}/{name}.fbx";
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter ?? throw new InvalidOperationException("missing " + fbx);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            var embedded = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().ToList();
            var keys = imp.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name)
                .Concat(embedded.Select(m => m.name)).Distinct().ToList();
            int n = 0;
            foreach (string slot in keys)
            {
                string matName = slot.StartsWith("M_", StringComparison.Ordinal) ? slot : "M_" + slot;
                var src = embedded.FirstOrDefault(m => m.name == slot);
                Color c = src != null && src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                    : src != null && src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
                var mat = AssetDatabase.LoadAssetAtPath<Material>($"{ArenaArt}/Materials/{matName}.mat");
                if (mat == null)
                    mat = ColorMaterial($"{ArenaArt}/Materials/{matName}.mat", c);
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), mat);
                n++;
            }
            imp.SaveAndReimport();

            string path = $"{ArenaPrefabs}/{name}.prefab";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            try
            {
                inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    r.lightProbeUsage = LightProbeUsage.Off;
                PrefabUtility.SaveAsPrefabAsset(inst, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(inst); }
            log.AppendLine($"  {name}: {n} material slots -> {path}");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        // ------------------------------------------------------------------ helpers

        private static Material TexturedMaterial(string name)
        {
            SunDunesImporter.ConfigureModel($"{BossArt}/{name}.fbx", false);
            var albedo = SunDunesImporter.ConfigureTexture($"{BossArt}/T_{name}_Albedo.png", true);
            return SunDunesImporter.EnsureToonMaterial($"{BossArt}/M_{name}.mat", albedo, null, false, false);
        }

        private static Material ColorMaterial(string path, Color c)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(ToonShader) ?? throw new InvalidOperationException("toon shader missing"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", c);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Transform AddModel(Transform parent, string fbx, Material mat)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx) ?? throw new InvalidOperationException("model not found: " + fbx);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            inst.name = "Model";
            inst.transform.SetParent(parent, false);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(c);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                r.lightProbeUsage = LightProbeUsage.Off;
            }
            return inst.transform;
        }

        private static void RemoveChildren(Transform root, params string[] names)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
                if (names.Contains(root.GetChild(i).name))
                    UnityEngine.Object.DestroyImmediate(root.GetChild(i).gameObject);
        }

        private static void EditPrefab(string path, Action<Transform> edit)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok)
                    throw new InvalidOperationException("Failed to save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
