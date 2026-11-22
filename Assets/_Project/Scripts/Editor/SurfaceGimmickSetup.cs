using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Spring pad (blocks with surface "bounce") and conveyor kit (surface "conveyor_r" / "conveyor_l"), shared by every
    /// world. Source: ArtSource/Common/_build_scripts/build_surface_gimmicks.py (Blender). This menu makes the toon
    /// materials and one prefab per piece; StageLayoutBuilder picks the prefabs up by path (missing = primitive look).
    /// Idempotent. Rebuild the stages afterwards.
    /// </summary>
    public static class SurfaceGimmickSetup
    {
        private const string ToonShader = "MyGame/Toon/Lit";
        private const string ModelPath = "Assets/_Project/Art/Environment/Common/Models/G10_SurfaceGimmicks.fbx";
        private const string MatDir = "Assets/_Project/Art/Environment/Common/Materials";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/Common";

        public const string SpringPadPrefab = PrefabDir + "/G10_SpringPad.prefab";
        public static string ConveyorPrefab(string piece) => $"{PrefabDir}/G11_Conveyor_{piece}.prefab";

        // same table as the build script
        private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            { "SG_Pink", new Color(1.00f, 0.42f, 0.62f) }, { "SG_PinkLight", new Color(1.00f, 0.68f, 0.80f) },
            { "SG_PinkDark", new Color(0.80f, 0.22f, 0.46f) }, { "SG_Cream", new Color(0.99f, 0.94f, 0.86f) },
            { "SG_Gold", new Color(1.00f, 0.76f, 0.20f) }, { "SG_Star", new Color(1.00f, 0.93f, 0.55f) },
            { "SG_Belt", new Color(0.17f, 0.18f, 0.22f) }, { "SG_Cleat", new Color(0.30f, 0.31f, 0.38f) },
            { "SG_Steel", new Color(0.46f, 0.53f, 0.64f) }, { "SG_Trim", new Color(0.78f, 0.83f, 0.90f) },
            { "SG_Recess", new Color(0.10f, 0.11f, 0.15f) }, { "SG_Arrow", new Color(1.00f, 0.83f, 0.12f) },
            { "SG_HazardDark", new Color(0.14f, 0.14f, 0.17f) },
        };

        [MenuItem("Tools/MyGame/Art/Setup Surface Gimmicks")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            AssetDatabase.Refresh();
            var sb = new StringBuilder("[SurfaceGimmickSetup]\n");
            var shader = Shader.Find(ToonShader);
            if (shader == null)
                return "Shader not found: " + ToonShader;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                return "FBX missing: " + ModelPath;
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();

            var mats = new Dictionary<string, Material>();
            foreach (var kv in Colors)
                mats[kv.Key] = Toon(shader, "M_" + kv.Key, kv.Value);

            // spring pad: static base + the cushion BounceSurface squashes (both pivots on the block top)
            var pad = new GameObject("G10_SpringPad");
            Piece(model, "Pad_Base", pad.transform, "Base", mats, true, sb);
            Piece(model, "Pad_Cushion", pad.transform, "Cushion", mats, true, sb);
            PrefabUtility.SaveAsPrefabAsset(pad, SpringPadPrefab);
            Object.DestroyImmediate(pad);

            foreach (var (piece, shadows) in new[] { ("Mid", true), ("End", true), ("Wheel", false), ("Drum", false), ("Cleat", false), ("Arrow", false) })
            {
                var root = new GameObject("G11_Conveyor_" + piece);
                Piece(model, "Conv_" + piece, root.transform, "Visual", mats, shadows, sb);
                PrefabUtility.SaveAsPrefabAsset(root, ConveyorPrefab(piece));
                Object.DestroyImmediate(root);
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        /// <summary>Copies one object of the model under <paramref name="parent"/> (pivot kept at the parent's origin).</summary>
        private static void Piece(GameObject model, string source, Transform parent, string name, Dictionary<string, Material> mats, bool shadows, StringBuilder sb)
        {
            var src = model.transform.Find(source);
            if (src == null)
            {
                sb.AppendLine($"  MISSING in FBX: {source}");
                return;
            }
            var go = Object.Instantiate(src.gameObject, parent, false);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var slots = r.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string n = slots[i] != null ? slots[i].name : "";
                    if (mats.TryGetValue(n, out var m))
                        slots[i] = m;
                    else
                        sb.AppendLine($"  {source}: unknown material slot '{n}'");
                }
                r.sharedMaterials = slots;
                r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                var b = r.GetComponent<MeshFilter>().sharedMesh.bounds;
                sb.AppendLine($"  {source}: rot {go.transform.localEulerAngles}, scale {go.transform.localScale}, mesh min {b.min} max {b.max}");
            }
        }

        private static Material Toon(Shader shader, string name, Color color)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
