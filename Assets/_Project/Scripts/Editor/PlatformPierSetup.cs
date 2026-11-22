using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Platform piers (T10): tall supports that stand behind the lane under floating platforms, one per world.
    /// Source: ArtSource/Common/_build_scripts/build_platform_piers.py (Blender). This menu turns the exported FBXs
    /// into flat-colour toon materials + prefabs and assigns them to the theme kits (platformSupport). Idempotent.
    /// </summary>
    public static class PlatformPierSetup
    {
        private const string ToonShader = "MyGame/Toon/Lit";
        private const string ArtRoot = "Assets/_Project/Art/Environment";
        private const string PrefabRoot = "Assets/_Project/Prefabs/Environment";

        private readonly struct Pier
        {
            public readonly string Key, Theme, Id, ModelDir, PrefabDir;
            public Pier(string key, string theme, string id, string modelDir, string prefabDir)
            { Key = key; Theme = theme; Id = id; ModelDir = modelDir; PrefabDir = prefabDir; }
        }

        private static readonly Pier[] Piers =
        {
            new Pier("GM", "GreenMeadow", "T10_PlatformPier", "GreenMeadow/Models", "GreenMeadow"),
            new Pier("SD", "SunDunes", "SD_G11_PlatformPier", "SunDunes/Ground/Models", "SunDunes/Ground"),
            new Pier("FP", "FrostPeaks", "FP_T10_PlatformPier", "FrostPeaks/Ground/Models", "FrostPeaks/Ground"),
            new Pier("CC", "CoralCoast", "CC_T10_PlatformPier", "CoralCoast/Ground/Models", "CoralCoast/Ground"),
            new Pier("EF", "EmberFortress", "EF_T10_PlatformPier", "EmberFortress/Ground/Models", "EmberFortress/Ground"),
        };

        // Same palette as the Blender script (sRGB).
        private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            { "GM_Stone", new Color(0.78f, 0.62f, 0.42f) }, { "GM_StoneDark", new Color(0.60f, 0.45f, 0.30f) },
            { "GM_StoneLight", new Color(0.90f, 0.76f, 0.54f) }, { "GM_Moss", new Color(0.60f, 0.80f, 0.24f) },
            { "SD_Stone", new Color(0.93f, 0.62f, 0.36f) }, { "SD_StoneDark", new Color(0.78f, 0.46f, 0.25f) },
            { "SD_StoneLight", new Color(0.98f, 0.75f, 0.48f) }, { "SD_Wood", new Color(0.46f, 0.28f, 0.18f) },
            { "FP_Stone", new Color(0.55f, 0.62f, 0.75f) }, { "FP_StoneDark", new Color(0.41f, 0.47f, 0.62f) },
            { "FP_StoneLight", new Color(0.69f, 0.76f, 0.86f) }, { "FP_Snow", new Color(0.95f, 0.97f, 1f) },
            { "FP_Ice", new Color(0.58f, 0.84f, 0.98f) },
            { "CC_Wood", new Color(0.56f, 0.37f, 0.24f) }, { "CC_WoodDark", new Color(0.40f, 0.25f, 0.17f) },
            { "CC_WoodLight", new Color(0.69f, 0.48f, 0.32f) }, { "CC_Rope", new Color(0.86f, 0.76f, 0.56f) },
            { "EF_Stone", new Color(0.36f, 0.30f, 0.37f) }, { "EF_StoneDark", new Color(0.25f, 0.20f, 0.27f) },
            { "EF_StoneLight", new Color(0.48f, 0.41f, 0.48f) }, { "EF_Iron", new Color(0.17f, 0.16f, 0.20f) },
            { "EF_Ember", new Color(1f, 0.48f, 0.10f) },
        };

        [MenuItem("Tools/MyGame/Art/Setup Platform Piers")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            AssetDatabase.Refresh();
            var sb = new StringBuilder("[PlatformPierSetup]\n");
            var shader = Shader.Find(ToonShader);
            if (shader == null)
                return "Shader not found: " + ToonShader;

            foreach (var p in Piers)
            {
                string fbxPath = $"{ArtRoot}/{p.ModelDir}/{p.Id}.fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (model == null)
                {
                    sb.AppendLine($"  {p.Id}: FBX missing ({fbxPath})");
                    continue;
                }
                string matDir = $"{ArtRoot}/{p.ModelDir}".Replace("/Models", "/Materials");
                Directory.CreateDirectory(matDir);
                string prefabDir = $"{PrefabRoot}/{p.PrefabDir}";
                Directory.CreateDirectory(prefabDir);

                var root = new GameObject(p.Id);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Visual";
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        mats[i] = EnsureMaterial(shader, matDir, mats[i] != null ? mats[i].name : "", p.Key);
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                var bounds = new Bounds(Vector3.zero, Vector3.zero);
                foreach (var r in visual.GetComponentsInChildren<Renderer>())
                    bounds.Encapsulate(r.bounds);
                string prefabPath = $"{prefabDir}/{p.Id}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Object.DestroyImmediate(root);

                var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(StageThemeKit.PathFor(p.Theme));
                if (kit != null)
                {
                    kit.platformSupport = prefab;
                    EditorUtility.SetDirty(kit);
                }
                sb.AppendLine($"  {p.Id}: prefab ok, bounds min {bounds.min} max {bounds.max}, kit {(kit != null ? "assigned" : "MISSING")}");
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>FBX slot "M_GM_Pier_Stone" -> material asset with the palette colour "GM_Stone".</summary>
        private static Material EnsureMaterial(Shader shader, string dir, string slotName, string key)
        {
            string shade = slotName.Contains("_Pier_") ? slotName.Substring(slotName.IndexOf("_Pier_") + 6) : "Stone";
            string name = $"M_{key}_Pier_{shade}";
            string path = $"{dir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            if (Colors.TryGetValue($"{key}_{shade}", out var c))
                mat.SetColor("_BaseColor", c);
            if (shade == "Ember")
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(2.2f, 0.7f, 0.12f));
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
