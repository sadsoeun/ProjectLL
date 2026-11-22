using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Green Meadow v3 art pass (hand-painted albedo atlases): texture import settings, one toon material per asset
    /// (Materials/v3/M_GM_*.mat), FBX material remaps and prefab material assignment. Idempotent.
    /// Menu: Tools/MyGame/Art/Apply Green Meadow v3.
    /// </summary>
    public static class GreenMeadowV3
    {
        private const string Root = "Assets/_Project/Art/Environment/GreenMeadow/";
        private const string TexDir = Root + "Textures/";
        private const string MatDir = Root + "Materials/v3/";
        private const string ModelDir = Root + "Models/";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/GreenMeadow/";
        private const string ToonShader = "MyGame/Toon/Lit";
        private const string WindShader = "MyGame/Ambient/Wind Lit";

        private struct Wind { public float Strength, Speed, Flutter; public Wind(float s, float sp, float f = 0.25f) { Strength = s; Speed = sp; Flutter = f; } }

        private static readonly Dictionary<string, Wind> WindAssets = new()
        {
            { "V01_BroadleafOak", new Wind(0.05f, 0.8f) },
            { "V02_RoundShrub", new Wind(0.03f, 0.9f) },
            { "V03_GrassTuft", new Wind(0.06f, 1.3f, 0.04f) },
            { "V04_FlowerCluster", new Wind(0.05f, 1.3f, 0.04f) },
            { "B02_ForestMass", new Wind(0.08f, 0.6f) },
            { "G01_CheckpointBanner", new Wind(0.07f, 1.6f, 0.3f) },
            { "G01_CheckpointBanner_Active", new Wind(0.07f, 1.6f, 0.3f) },
        };

        private const float TerrainMacroStrength = 0.6f;
        private const float TerrainMacroScale = 4.5f;
        private const float TerrainTileVariation = 0.06f;

        private static readonly string[] Prefabs =
        {
            "T01_GrassCliffCore", "T02_GrassCliffLeftEnd", "T03_GrassCliffRightEnd", "T04_BareRockCore", "T05_CliffStepModule", "T06_GrassFloatingPlatform",
            "T07_ThinTimberPlatform", "T07_ThinTimberPlatform3m", "T07_TimberSupport",
            "V01_BroadleafOak", "V02_RoundShrub", "V03_GrassTuft", "V04_FlowerCluster", "V05_StoneCluster", "P01_TimberFence", "P02_DirectionSign",
            "A01_Windmill", "A02_Watchtower", "G01_Checkpoint", "G02_GoalArch", "G03a_Coin", "G03b_MagentaGem",
            "B01_RollingHill", "B02_ForestMass", "B03_SeaStackCluster", "B04_WaterfallCliff", "B05_CloudMass",
        };

        [MenuItem("Tools/MyGame/Art/Apply Green Meadow v3")]
        public static void Menu() => Debug.Log(Apply());

        public static string Apply()
        {
            var log = new StringBuilder("[GreenMeadowV3]\n");
            AssetDatabase.Refresh();
            ImportTextures(log);
            var mats = MakeMaterials(log);
            RemapFbx(mats, log);
            AssignPrefabs(mats, log);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString();
        }

        private static void ImportTextures(StringBuilder log)
        {
            int n = 0;
            foreach (var f in Directory.GetFiles(TexDir, "T_GM_*_Albedo.png"))
            {
                string p = TexDir + Path.GetFileName(f);
                var ti = (TextureImporter)AssetImporter.GetAtPath(p);
                if (ti == null) { log.AppendLine("  no importer " + p); continue; }
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = true;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = 4;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.crunchedCompression = false;
                ti.SaveAndReimport();
                n++;
            }
            log.AppendLine($"  textures configured: {n}");
        }

        private static Dictionary<string, Material> MakeMaterials(StringBuilder log)
        {
            if (!AssetDatabase.IsValidFolder(MatDir.TrimEnd('/'))) AssetDatabase.CreateFolder(Root.TrimEnd('/') + "/Materials", "v3");
            var result = new Dictionary<string, Material>();
            var keys = new List<string>(Prefabs) { "G01_CheckpointBanner", "G01_CheckpointBanner_Active" };
            foreach (var key in keys)
            {
                string texName = "T_GM_" + key + "_Albedo.png";
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + texName);
                if (tex == null) { log.AppendLine("  missing texture " + texName); continue; }
                bool wind = WindAssets.TryGetValue(key, out var w);
                string path = MatDir + "M_GM_" + key + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader = Shader.Find(wind ? WindShader : ToonShader);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                else mat.shader = shader;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_ToonMode", 1f);
                mat.SetFloat("_ShadeSteps", 3f);
                mat.SetFloat("_ShadeSmoothness", 0.03f);
                mat.SetColor("_ShadowColor", key.StartsWith("B05") ? new Color(0.72f, 0.78f, 0.95f) : new Color(0.72f, 0.70f, 0.84f)); // matches world map v3 tuning
                // Terrain modules repeat every 2 m: world-space colour patches + per-module brightness hide the tiling.
                bool terrain = key.StartsWith("T0") && !key.StartsWith("T07");
                mat.SetFloat("_MacroStrength", terrain ? TerrainMacroStrength : 0f);
                mat.SetFloat("_MacroScale", TerrainMacroScale);
                mat.SetFloat("_TileVariation", terrain ? TerrainTileVariation : 0f);
                if (wind)
                {
                    mat.SetFloat("_WindStrength", w.Strength);
                    mat.SetFloat("_WindSpeed", w.Speed);
                    mat.SetFloat("_Flutter", w.Flutter);
                    mat.SetFloat("_HeightWeight", 0f); // sway weight comes from vertex colour R
                }
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
                result[key] = mat;
            }
            AssetDatabase.SaveAssets();
            log.AppendLine($"  materials: {result.Count}");
            return result;
        }

        private static void RemapFbx(Dictionary<string, Material> mats, StringBuilder log)
        {
            foreach (var name in Prefabs)
            {
                string fbx = ModelDir + name + ".fbx";
                var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
                if (imp == null) { log.AppendLine("  no model importer " + fbx); continue; }
                var keys = name == "G01_Checkpoint" ? new[] { name, "G01_CheckpointBanner" } : new[] { name };
                foreach (var key in keys)
                {
                    // Blender material names: M_GM_<asset>; checkpoint banner: M_GM_G01_CheckpointBanner
                    if (!mats.TryGetValue(key, out var m)) continue;
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), m);
                }
                imp.SaveAndReimport();
            }
            log.AppendLine("  FBX remaps added");
        }

        private static void AssignPrefabs(Dictionary<string, Material> mats, StringBuilder log)
        {
            foreach (var name in Prefabs)
            {
                string path = PrefabDir + name + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int n = 0;
                    foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        string rn = r.gameObject.name;
                        if (rn.EndsWith("_Water") || rn.EndsWith("_Foam")) continue;
                        Material m = rn.EndsWith("_Banner") ? mats["G01_CheckpointBanner"] : mats[name];
                        r.sharedMaterials = new[] { m };
                        n++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.AppendLine($"  {name}: {n} renderer(s) assigned");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
    }
}
