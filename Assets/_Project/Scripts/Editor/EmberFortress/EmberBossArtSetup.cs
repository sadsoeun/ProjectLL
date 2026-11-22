using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.EditorTools
{
    /// <summary>
    /// World 5 boss stage art, after `Import Bosses` has built the Ember Ironback (9005) prefab and its magma-chunk hazard:
    /// - BossWave_Ember (magma shockwave, EF_FX_MagmaWave, ash puffs) -> BossData 9005 wavePrefab
    /// - BossWall_EmberFortress / BossGate_EmberFortress (arena end wall and entrance gate) -> kit arenaWallVisual / arenaGateVisual
    /// Idempotent (prefab GUIDs kept). Menu: Tools/MyGame/Art/Build Ember Fortress Boss Art. Rebuild the W5 stages afterwards.
    /// </summary>
    public static class EmberBossArtSetup
    {
        private const string BossArt = "Assets/_Project/Art/Characters/Bosses/EF_BOSS_EmberIronback";
        private const string ArenaArt = "Assets/_Project/Art/Environment/BossArena";
        private const string ArenaPrefabs = "Assets/_Project/Prefabs/Environment/BossArena";
        private const string BossPrefabs = "Assets/_Project/Prefabs/Bosses";
        private const string HazardMats = "Assets/_Project/Art/Materials/Hazards";
        private const string ToonShader = "MyGame/Toon/Lit";

        [MenuItem("Tools/MyGame/Art/Build Ember Fortress Boss Art")]
        public static void Menu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode first.");
            var log = new StringBuilder("[EmberBossArtSetup]\n");
            var data = BossCatalog.Load(9005) ?? throw new InvalidOperationException("Boss_9005 missing (Tools/MyGame/Bosses/Create Default Boss Data And Prefabs).");

            // --- ash puff (wave trail): FX_SandPuff with an ash material
            string puffPath = $"{BossPrefabs}/FX_AshPuff.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(puffPath) == null)
                AssetDatabase.CopyAsset($"{BossPrefabs}/FX_SandPuff.prefab", puffPath);
            var ashMat = ColorMaterial($"{HazardMats}/M_FX_AshPuff.mat", new Color(0.42f, 0.38f, 0.42f));
            EditPrefab(puffPath, root =>
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(ashMat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
            });
            var puff = AssetDatabase.LoadAssetAtPath<CartoonFx>(puffPath);
            log.AppendLine("  FX_AshPuff");

            // --- magma shockwave
            var waveMat = TexturedMaterial("EF_FX_MagmaWave");
            string wavePath = $"{BossPrefabs}/BossWave_Ember.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(wavePath) == null)
                AssetDatabase.CopyAsset($"{BossPrefabs}/BossWave.prefab", wavePath);
            EditPrefab(wavePath, root =>
            {
                RemoveChildren(root, "Visual", "Model");
                var model = AddModel(root, $"{BossArt}/EF_FX_MagmaWave.fbx", waveMat);
                model.localScale = new Vector3(1f, 0.95f, 1f); // magma crest over the 1 m (jumpable) hit box
                var fx = root.GetComponent<SandGustFx>() ?? root.gameObject.AddComponent<SandGustFx>();
                EditorWire.Set(fx, "model", model);
                EditorWire.Set(fx, "swirls", Array.Empty<Transform>());
                EditorWire.Set(fx, "crest", (Transform)null);
                EditorWire.Set(fx, "streaks", Array.Empty<Transform>());
                EditorWire.Set(fx, "puffPrefab", puff);
                EditorWire.Set(fx, "leadOffset", 0f);
            });
            data.eruptionPrefab = BuildEruption(waveMat, log);
            data.wavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wavePath);
            data.markerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{BossPrefabs}/BossShadowMarker.prefab");
            EditorUtility.SetDirty(data);
            log.AppendLine("  BossWave_Ember -> Boss_9005.wavePrefab; falling = " + (data.fallingPrefab != null ? data.fallingPrefab.name : "NONE (run Import Bosses)"));

            // --- arena wall and gate
            var wall = ArenaPrefab("BossWall_EmberFortress", log);
            var gate = ArenaPrefab("BossGate_EmberFortress", log);

            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(StageThemeKit.PathFor("EmberFortress"))
                      ?? throw new InvalidOperationException("EmberFortress theme kit not found.");
            kit.arenaWallVisual = wall;
            kit.arenaGateVisual = gate;
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            log.AppendLine("  EmberFortress kit: arenaWallVisual, arenaGateVisual set");
            return log.ToString();
        }

        // ------------------------------------------------------------------ eruption

        /// <summary>
        /// BossEruption_Ember: "Warn" = glowing floor crack (disc + crossing crack bars), "Column" = magma pillar (pivot at the
        /// floor, scaled up by BossEruption) with a bulging molten head. Sized for the default 2.4 m cells / 3.6 m height.
        /// </summary>
        private static GameObject BuildEruption(Material magma, StringBuilder log)
        {
            string path = $"{BossPrefabs}/BossEruption_Ember.prefab";
            var glow = EmissiveMaterial($"{HazardMats}/M_FX_EruptionWarn.mat", new Color(0.35f, 0.12f, 0.05f), new Color(1.6f, 0.55f, 0.1f));
            var crack = EmissiveMaterial($"{HazardMats}/M_FX_EruptionCrack.mat", new Color(0.5f, 0.2f, 0.05f), new Color(2.2f, 1.1f, 0.3f));
            // magma pillar: the wave's painted magma, lit from within
            var pillar = AssetDatabase.LoadAssetAtPath<Material>($"{HazardMats}/M_FX_EruptionPillar.mat");
            if (pillar == null)
            {
                pillar = new Material(magma);
                AssetDatabase.CreateAsset(pillar, $"{HazardMats}/M_FX_EruptionPillar.mat");
            }
            pillar.CopyPropertiesFromMaterial(magma);
            if (pillar.HasProperty("_EmissionMap") && magma.HasProperty("_BaseMap"))
                pillar.SetTexture("_EmissionMap", magma.GetTexture("_BaseMap"));
            pillar.SetColor("_EmissionColor", new Color(0.95f, 0.75f, 0.6f));
            pillar.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(pillar);

            var root = new GameObject("BossEruption_Ember");
            try
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                var box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(2f, 3.6f, 3f);
                box.center = new Vector3(0f, 1.8f, 0f);

                var warn = new GameObject("Warn").transform;
                warn.SetParent(root.transform, false);
                Prim(PrimitiveType.Cylinder, warn, new Vector3(0f, 0.03f, 0f), new Vector3(2.1f, 0.012f, 2.1f), Quaternion.identity, glow);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Cube, warn, new Vector3(0f, 0.05f, 0f), new Vector3(2.2f, 0.02f, 0.14f), Quaternion.Euler(0f, 25f + i * 60f, 0f), crack);

                var column = new GameObject("Column").transform;
                column.SetParent(root.transform, false);
                Prim(PrimitiveType.Cylinder, column, new Vector3(0f, 1.6f, 0f), new Vector3(1.55f, 1.6f, 1.55f), Quaternion.identity, pillar);
                Prim(PrimitiveType.Sphere, column, new Vector3(0f, 3.3f, 0f), new Vector3(2.0f, 1.3f, 2.0f), Quaternion.identity, pillar);
                Prim(PrimitiveType.Sphere, column, new Vector3(0.55f, 3.85f, 0.2f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.identity, pillar);
                Prim(PrimitiveType.Sphere, column, new Vector3(-0.5f, 3.95f, -0.25f), new Vector3(0.55f, 0.55f, 0.55f), Quaternion.identity, pillar);
                Prim(PrimitiveType.Cylinder, column, new Vector3(0f, 0.08f, 0f), new Vector3(2.3f, 0.08f, 2.3f), Quaternion.identity, pillar); // splash ring

                var cell = root.AddComponent<BossEruption>();
                EditorWire.Set(cell, "warnVisual", warn);
                EditorWire.Set(cell, "columnVisual", column);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            log.AppendLine("  BossEruption_Ember -> Boss_9005.eruptionPrefab");
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
        }

        private static Material EmissiveMaterial(string path, Color baseColor, Color emission)
        {
            var mat = ColorMaterial(path, baseColor);
            mat.SetColor("_EmissionColor", emission);
            mat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ arena

        /// <summary>FBX materials (M_BW_EmberFortress_*) become toon materials with the colour authored in Blender; the prefab is
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
