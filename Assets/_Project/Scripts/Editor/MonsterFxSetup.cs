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
    /// Builds the comic attack effects of monsters from the Blender FBXs (ArtSource/Monsters/Common/FX/MonsterFX_All.blend):
    /// materials (flat unlit colours for the comic shapes, toon lit for dust / seed), CartoonFx prefabs and the seed
    /// projectile. Run again after re-exporting an FBX; existing prefabs are overwritten in place (GUIDs kept).
    ///   Tools/MyGame/Monsters/Build Monster FX
    /// </summary>
    public static class MonsterFxSetup
    {
        private const string ModelFolder = "Assets/_Project/Art/Characters/Monsters/Common/FX/Models";
        private const string MaterialFolder = "Assets/_Project/Art/Characters/Monsters/Common/FX/Materials";
        private const string UnlitShader = "Universal Render Pipeline/Unlit";
        private const string ToonShader = "MyGame/Toon/Lit";

        // Blender base colours (linear) of the material slots; unlit = flat comic colour.
        private static readonly Dictionary<string, (Color linear, bool unlit)> Materials = new()
        {
            ["FX_Red"] = (new Color(1f, 0.18f, 0.12f), true),
            ["FX_Yellow"] = (new Color(1f, 0.82f, 0.18f), true),
            ["FX_White"] = (new Color(1f, 1f, 1f), true),
            ["FX_Dark"] = (new Color(0.1f, 0.06f, 0.07f), true),
            ["FX_Pale"] = (new Color(0.93f, 0.95f, 1f), true),
            ["FX_Dust"] = (new Color(0.92f, 0.89f, 0.84f), false),
            ["FX_Seed"] = (new Color(0.42f, 0.72f, 0.22f), false),
            ["FX_SeedTip"] = (new Color(1f, 0.88f, 0.5f), false),
            ["FX_SeedStripe"] = (new Color(0.22f, 0.48f, 0.14f), false),
            ["FX_SeedLeaf"] = (new Color(0.3f, 0.62f, 0.2f), false),
        };

        private static readonly (string model, CartoonFx.Style style, float lifetime, bool billboard)[] Effects =
        {
            ("FX_MonsterAlert", CartoonFx.Style.Pop, 1f, true),
            ("FX_MonsterShockLines", CartoonFx.Style.Burst, 0.4f, true),
            ("FX_MonsterSwoosh", CartoonFx.Style.Swipe, 0.35f, false),
            ("FX_MonsterShockwave", CartoonFx.Style.Ring, 0.7f, false),
            ("FX_MonsterDust", CartoonFx.Style.Puff, 0.5f, false),
        };

        [MenuItem("Tools/MyGame/Monsters/Build Monster FX")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            var log = new StringBuilder("[MonsterFxSetup]\n");
            SceneFlowSetup.EnsureFolder(MaterialFolder);
            SceneFlowSetup.EnsureFolder(MonsterTableTool.FxFolder);
            var mats = Materials.ToDictionary(kv => kv.Key, kv => EnsureMaterial(kv.Key, kv.Value.linear, kv.Value.unlit));

            foreach (var e in Effects.Select(e => e.model).Append("FX_MonsterSeed"))
                RemapModel($"{ModelFolder}/{e}.fbx", mats, log);

            foreach (var e in Effects)
            {
                var root = new GameObject(e.model);
                try
                {
                    var model = AddModel(root, e.model);
                    if (e.billboard || e.style == CartoonFx.Style.Swipe)
                        model.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f); // the drawn side faces the camera (-Z)
                    var fx = root.AddComponent<CartoonFx>();
                    EditorWire.Set(fx, "style", (int)e.style);
                    EditorWire.Set(fx, "lifetime", e.lifetime);
                    EditorWire.Set(fx, "billboard", e.billboard);
                    Save(root, $"{MonsterTableTool.FxFolder}/{e.model}.prefab", log);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }

            var seed = new GameObject("FX_SeedProjectile");
            try
            {
                var model = AddModel(seed, "FX_MonsterSeed");
                var col = seed.AddComponent<SphereCollider>();
                col.isTrigger = true;
                col.radius = 0.2f;
                var rb = seed.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
                var shot = seed.AddComponent<MonsterProjectile>();
                EditorWire.Set(shot, "visual", model.transform);
                Save(seed, MonsterTableTool.SeedProjectilePath, log);
            }
            finally { UnityEngine.Object.DestroyImmediate(seed); }

            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        private static Material EnsureMaterial(string name, Color linear, bool unlit)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var shader = Shader.Find(unlit ? UnlitShader : ToonShader)
                         ?? throw new InvalidOperationException("shader not found: " + (unlit ? UnlitShader : ToonShader));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            // Material colours are authored in sRGB; Blender stores linear.
            var c = new Color(Mathf.LinearToGammaSpace(linear.r), Mathf.LinearToGammaSpace(linear.g), Mathf.LinearToGammaSpace(linear.b), 1f);
            mat.SetColor("_BaseColor", c);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Points every embedded material slot ("FX_Dark.001" included) at the shared material asset.</summary>
        private static void RemapModel(string path, Dictionary<string, Material> mats, StringBuilder log)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter
                      ?? throw new InvalidOperationException("model not found: " + path);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            var slots = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Renderer>()
                .SelectMany(r => r.sharedMaterials).Where(m => m != null).Select(m => m.name).Distinct().ToList();
            // Slot names are also known from the source materials before the first remap.
            foreach (var key in imp.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name))
                if (!slots.Contains(key)) slots.Add(key);
            int n = 0;
            foreach (string slot in slots)
            {
                string baseName = slot.Contains('.') ? slot.Substring(0, slot.IndexOf('.')) : slot;
                if (!mats.TryGetValue(baseName, out var mat))
                {
                    log.AppendLine($"  WARN {path}: slot '{slot}' has no FX material");
                    continue;
                }
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), mat);
                n++;
            }
            imp.SaveAndReimport();
            log.AppendLine($"  {System.IO.Path.GetFileName(path)}: {n} slots remapped ({string.Join(", ", slots)})");
        }

        private static GameObject AddModel(GameObject root, string model)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{ModelFolder}/{model}.fbx")
                        ?? throw new InvalidOperationException("model not found: " + model);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            foreach (var c in inst.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(c);
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = LightProbeUsage.Off;
            }
            return inst;
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
