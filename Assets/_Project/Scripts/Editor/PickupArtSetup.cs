using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// High-detail pickups from ArtSource/Common/_build_scripts/build_pickups_hq.py (Blender):
    ///   coin        - G03a_Coin_HQ.fbx replaces the content of the shared G03a_Coin prefab (same asset, so every
    ///                 theme kit and built stage picks it up); flat-colour toon materials, three gold shades.
    ///   golden leaf - GoldenLeaf_HQ.fbx is used by GoldenLeafAssets.Apply; this menu re-applies it to the
    ///                 SpecialCollectible prefab.
    /// Idempotent. Note: re-running the Green Meadow v3 importer rebuilds G03a_Coin from the old model; run this again after it.
    /// </summary>
    public static class PickupArtSetup
    {
        private const string ToonShader = "MyGame/Toon/Lit";
        private const string CoinModel = "Assets/_Project/Art/Environment/Common/Models/G03a_Coin_HQ.fbx";
        private const string MatDir = "Assets/_Project/Art/Environment/Common/Materials";
        private const string CoinPrefab = "Assets/_Project/Prefabs/Environment/GreenMeadow/G03a_Coin.prefab";
        private const string SpecialPrefab = "Assets/_Project/Prefabs/Level/SpecialCollectible.prefab";

        [MenuItem("Tools/MyGame/Art/Setup HQ Pickups")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return "Stop Play Mode first.";
            AssetDatabase.Refresh();
            var sb = new StringBuilder("[PickupArtSetup]\n");
            var shader = Shader.Find(ToonShader);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CoinModel);
            if (shader == null || model == null)
                return "missing shader or " + CoinModel;
            System.IO.Directory.CreateDirectory(MatDir);

            var gold = Toon(shader, "M_Coin_Gold", new Color(1.00f, 0.78f, 0.18f), new Color(0.22f, 0.13f, 0.0f));
            var light = Toon(shader, "M_Coin_GoldLight", new Color(1.00f, 0.92f, 0.50f), new Color(0.30f, 0.22f, 0.04f));
            var deep = Toon(shader, "M_Coin_GoldDeep", new Color(0.92f, 0.56f, 0.08f), new Color(0.12f, 0.05f, 0.0f));

            var root = new GameObject("G03a_Coin");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Model";
                int tris = 0;
                foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
                {
                    r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.Contains("GoldLight") ? light : m != null && m.name.Contains("GoldDeep") ? deep : gold).ToArray();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                    tris += r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                    sb.AppendLine($"  coin bounds {r.bounds.min} .. {r.bounds.max}");
                }
                PrefabUtility.SaveAsPrefabAsset(root, CoinPrefab, out bool ok);
                sb.AppendLine($"  {CoinPrefab}: {(ok ? "saved" : "FAILED")}, {tris} tris");
            }
            finally { Object.DestroyImmediate(root); }

            var special = PrefabUtility.LoadPrefabContents(SpecialPrefab);
            try
            {
                var leaf = special.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "GoldenLeaf");
                if (leaf == null) sb.AppendLine("  SpecialCollectible: no GoldenLeaf child");
                else
                {
                    GoldenLeafAssets.Apply(leaf.gameObject);
                    var mesh = leaf.GetComponent<MeshFilter>().sharedMesh;
                    PrefabUtility.SaveAsPrefabAsset(special, SpecialPrefab);
                    sb.AppendLine($"  SpecialCollectible: mesh {AssetDatabase.GetAssetPath(mesh)}, {mesh.triangles.Length / 3} tris, bounds {mesh.bounds.min} .. {mesh.bounds.max}");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(special); }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        private static Material Toon(Shader shader, string name, Color color, Color emission)
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
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission); // a little self-light so coins stay readable in shade
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
