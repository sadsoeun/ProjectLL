using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Imports the SUN DUNES (stage 2) art described in Docs/art/sundunes-contract.md: reads the per-asset manifests
    /// (Docs/art/sundunes/manifest/*.json), sets up textures and toon materials, and builds the prefabs (identity root,
    /// LODGroup from *_LOD0/1/2, COL_* meshes → colliders). Monsters additionally get an Animator controller
    /// (Speed / Attack / Hit / Defeat), a Visual prefab and a gameplay prefab (Monster + Behavior graph).
    /// Then fills the map-tool data: PropTable.csv rows (SD_*), Data/Themes/SunDunes.asset, MonsterTable.csv rows.
    /// Idempotent: re-running overwrites the generated assets in place (prefab GUIDs are kept).
    /// Menu: Tools/MyGame/Art/Import Sun Dunes.
    /// </summary>
    public static partial class SunDunesImporter
    {
        private const string EnvArtRoot = "Assets/_Project/Art/Environment/SunDunes";
        private const string EnvPrefabRoot = "Assets/_Project/Prefabs/Environment/SunDunes";
        private const string MonsterArtRoot = "Assets/_Project/Art/Characters/Monsters/Stage02";
        private const string MonsterPrefabRoot = "Assets/_Project/Prefabs/Monsters/Stage02";
        private const string ToonShader = "MyGame/Toon/Lit";
        private const string WindShader = "MyGame/Ambient/Wind Lit";
        private const string WaterShader = "MyGame/Ambient/Water";
        private static readonly Color ShadowTint = new Color(0.72f, 0.70f, 0.84f);

        private static string ManifestDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../Docs/art/sundunes/manifest"));

        private static readonly string[] Categories = { "Ground", "Props", "Background", "FX" };

        [MenuItem("Tools/MyGame/Art/Import Sun Dunes")]
        public static void Menu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before importing Sun Dunes.");
            var log = new StringBuilder("[SunDunesImporter]\n");
            AssetDatabase.Refresh();
            var manifests = LoadManifests(log);
            log.AppendLine($"  manifests: {manifests.Count}");

            var envPrefabs = new Dictionary<string, string>(); // asset name -> prefab path
            var monsterPrefabs = new Dictionary<string, (string visual, string gameplay, JObject m)>();
            foreach (var m in manifests)
            {
                string cat = (string)m["category"];
                try
                {
                    if (cat == "Monsters")
                    {
                        var r = ImportMonster(m, log);
                        if (r.visual != null)
                            monsterPrefabs[(string)m["name"]] = (r.visual, r.gameplay, m);
                    }
                    else if (Categories.Contains(cat))
                    {
                        string p = ImportEnvironment(m, cat, log);
                        if (p != null)
                            envPrefabs[(string)m["name"]] = p;
                    }
                    else
                    {
                        log.AppendLine($"  ! {(string)m["name"]}: unknown category '{cat}'");
                    }
                }
                catch (Exception e)
                {
                    log.AppendLine($"  ! {(string)m["name"]}: {e.GetType().Name}: {e.Message}");
                    Debug.LogException(e);
                }
            }
            AssetDatabase.SaveAssets();

            log.AppendLine(UpdatePropTable(envPrefabs));
            log.AppendLine(UpdateThemeKit(envPrefabs));
            log.AppendLine(UpdateMonsterTable(monsterPrefabs));
            log.AppendLine(ApplyMonsterTable());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return log.ToString().TrimEnd();
        }

        /// <summary>Writes the table (incl. the movement column) into the monster prefabs. A failure is reported, not fatal.</summary>
        private static string ApplyMonsterTable()
        {
            try
            {
                return "  " + MonsterTableTool.Apply().Replace("\n", "\n  ");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return "  ! Apply Monster Table failed: " + e.Message;
            }
        }

        // ------------------------------------------------------------------ manifests

        private static List<JObject> LoadManifests(StringBuilder log)
        {
            var list = new List<JObject>();
            if (!Directory.Exists(ManifestDir))
            {
                log.AppendLine("  ! manifest folder missing: " + ManifestDir);
                return list;
            }
            foreach (var file in Directory.GetFiles(ManifestDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                try
                {
                    var token = JToken.Parse(File.ReadAllText(file));
                    // A file may hold one asset or an array of assets (variants).
                    if (token is JArray arr)
                        list.AddRange(arr.OfType<JObject>());
                    else if (token is JObject obj)
                        list.Add(obj);
                }
                catch (Exception e)
                {
                    log.AppendLine($"  ! {Path.GetFileName(file)}: {e.Message}");
                }
            }
            return list.Where(m => !string.IsNullOrEmpty((string)m["name"]) && !string.IsNullOrEmpty((string)m["fbx"])).ToList();
        }

        private static bool HasPart(JObject m, string token) =>
            m["parts"] is JArray parts && parts.Any(p => ((string)p ?? "").IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);

        // ------------------------------------------------------------------ textures / materials

        internal static Texture2D ConfigureTexture(string path, bool sRGB, int maxSize = 2048)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;
            if (AssetImporter.GetAtPath(path) is TextureImporter ti)
            {
                bool dirty = ti.textureType != TextureImporterType.Default || ti.sRGBTexture != sRGB || !ti.mipmapEnabled
                             || ti.maxTextureSize < maxSize || ti.anisoLevel < 4;
                if (dirty)
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = sRGB;
                    ti.mipmapEnabled = true;
                    ti.maxTextureSize = maxSize;
                    ti.anisoLevel = 4;
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Material EnsureToonMaterial(string path, Texture2D albedo, Texture2D emission, bool wind, bool terrain)
        {
            var shader = Shader.Find(wind ? WindShader : ToonShader)
                         ?? throw new InvalidOperationException("Shader not found: " + (wind ? WindShader : ToonShader));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_ToonMode", 1f);
            mat.SetFloat("_ShadeSteps", 3f);
            mat.SetFloat("_ShadeSmoothness", 0.03f);
            mat.SetColor("_ShadowColor", ShadowTint);
            mat.SetTexture("_EmissionMap", emission);
            mat.SetColor("_EmissionColor", emission != null ? Color.white * 1.4f : Color.black);
            // Repeated terrain modules: world-space colour patches + per-module brightness hide the 2 m tiling.
            mat.SetFloat("_MacroStrength", terrain ? 0.5f : 0f);
            mat.SetFloat("_MacroScale", 4.5f);
            mat.SetFloat("_TileVariation", terrain ? 0.05f : 0f);
            if (wind)
            {
                mat.SetFloat("_WindStrength", 0.06f);
                mat.SetFloat("_WindSpeed", 1.1f);
                mat.SetFloat("_HeightWeight", 0f); // sway weight comes from vertex colour R
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureWaterMaterial()
        {
            string path = EnvArtRoot + "/Ground/Materials/M_SD_OasisWater.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(WaterShader);
            if (shader == null)
                return null;
            if (mat == null)
            {
                SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", new Color(0.20f, 0.66f, 0.70f, 0.85f));   // oasis teal #42C6CB, deeper
            mat.SetColor("_ShallowColor", new Color(0.45f, 0.86f, 0.84f, 0.6f));
            mat.SetFloat("_WaveHeight", 0.015f);
            mat.SetFloat("_DepthFade", 0.6f);
            mat.SetFloat("_FoamWidth", 0.25f);
            mat.SetFloat("_RippleScale", 0.8f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ model import

        internal static ModelImporter ConfigureModel(string fbx, bool animated)
        {
            if (!(AssetImporter.GetAtPath(fbx) is ModelImporter mi))
                throw new InvalidOperationException("Not a model: " + fbx);
            bool dirty = false;
            void Set<T>(T current, T wanted, Action<T> apply)
            {
                if (EqualityComparer<T>.Default.Equals(current, wanted))
                    return;
                apply(wanted);
                dirty = true;
            }
            Set(mi.materialImportMode, ModelImporterMaterialImportMode.None, v => mi.materialImportMode = v);
            Set(mi.importCameras, false, v => mi.importCameras = v);
            Set(mi.importLights, false, v => mi.importLights = v);
            Set(mi.importAnimation, animated, v => mi.importAnimation = v);
            Set(mi.animationType, animated ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None, v => mi.animationType = v);
            Set(mi.meshCompression, ModelImporterMeshCompression.Off, v => mi.meshCompression = v);
            Set(mi.isReadable, false, v => mi.isReadable = v);
            Set(mi.addCollider, false, v => mi.addCollider = v);
            if (dirty)
                mi.SaveAndReimport();
            return mi;
        }

        /// <summary>
        /// Gives every visible renderer the asset material (water parts get the animated water material), turns COL_
        /// meshes into colliders (renderer off), and builds a LODGroup when *_LOD1 / *_LOD2 meshes exist.
        /// </summary>
        internal static (int renderers, int colliders, int lods) DressModel(GameObject root, Material main, Material water, bool isBackground)
        {
            int rendererCount = 0, colliderCount = 0;
            var lod = new List<Renderer>[3] { new List<Renderer>(), new List<Renderer>(), new List<Renderer>() };
            var shared = new List<Renderer>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>();
                if (t.name.StartsWith("COL_", StringComparison.Ordinal))
                {
                    if (t.GetComponent<Collider>() == null && t.GetComponent<MeshFilter>() != null)
                    {
                        string n = t.name.ToLowerInvariant();
                        if (n.Contains("ramp") || n.Contains("convex") || n.Contains("stair") || n.Contains("deck"))
                        {
                            var mc = t.gameObject.AddComponent<MeshCollider>();
                            mc.convex = true;
                        }
                        else
                        {
                            t.gameObject.AddComponent<BoxCollider>(); // fits the mesh bounds
                        }
                        colliderCount++;
                    }
                    if (r != null)
                        r.enabled = false;
                    continue;
                }
                if (r == null)
                    continue;
                bool isWater = water != null && t.name.IndexOf("Water", StringComparison.OrdinalIgnoreCase) >= 0;
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = isWater ? water : main;
                r.sharedMaterials = mats;
                if (isBackground)
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // far solids: no shadow cost
                rendererCount++;

                int level = t.name.EndsWith("_LOD1", StringComparison.Ordinal) ? 1 : t.name.EndsWith("_LOD2", StringComparison.Ordinal) ? 2 : t.name.EndsWith("_LOD0", StringComparison.Ordinal) ? 0 : -1;
                if (level < 0)
                    shared.Add(r);
                else
                    lod[level].Add(r);
            }

            int levels = lod[2].Count > 0 ? 3 : lod[1].Count > 0 ? 2 : 0;
            if (levels == 0)
                return (rendererCount, colliderCount, 0);
            var group = root.GetComponent<LODGroup>();
            if (group == null)
                group = root.AddComponent<LODGroup>(); // no ?? on UnityEngine.Object (fake null)
            foreach (var nested in root.GetComponentsInChildren<LODGroup>(true))
                if (nested != group)
                    UnityEngine.Object.DestroyImmediate(nested, true); // the FBX import adds its own for *_LOD* meshes: keep one group
            float[] heights = isBackground
                ? (levels == 3 ? new[] { 0.30f, 0.12f, 0.005f } : new[] { 0.25f, 0.005f })
                : (levels == 3 ? new[] { 0.35f, 0.12f, 0.01f } : new[] { 0.30f, 0.01f });
            var lods = new LOD[levels];
            for (int i = 0; i < levels; i++)
                lods[i] = new LOD(heights[i], lod[i].Concat(shared).ToArray());
            group.SetLODs(lods);
            group.RecalculateBounds();
            return (rendererCount, colliderCount, levels);
        }

        // ------------------------------------------------------------------ environment

        private static string ImportEnvironment(JObject m, string cat, StringBuilder log)
        {
            string name = (string)m["name"];
            string fbx = (string)m["fbx"];
            if (AssetDatabase.LoadAssetAtPath<GameObject>(fbx) == null)
            {
                log.AppendLine($"  - {name}: FBX not found ({fbx}), skipped");
                return null;
            }
            ConfigureModel(fbx, false);
            string texDir = $"{EnvArtRoot}/{cat}/Textures/";
            var albedo = ConfigureTexture(texDir + (string)m["albedo"], true);
            var emission = m["emission"]?.Type == JTokenType.String ? ConfigureTexture(texDir + (string)m["emission"], true) : null;
            if (albedo == null)
                log.AppendLine($"  ! {name}: albedo missing ({texDir}{(string)m["albedo"]})");
            bool wind = (bool?)m["wind"] ?? false;
            bool terrain = cat == "Ground" && !name.Contains("Oasis");
            var mat = EnsureToonMaterial($"{EnvArtRoot}/{cat}/Materials/M_{name}.mat", albedo, emission, wind, terrain);
            var water = name.Contains("Oasis") || HasPart(m, "Water") ? EnsureWaterMaterial() : null;

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var root = new GameObject(name);
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                var (rc, cc, lv) = DressModel(root, mat, water, cat == "Background");
                string path = $"{EnvPrefabRoot}/{cat}/{name}.prefab";
                SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
                PrefabUtility.SaveAsPrefabAsset(root, path);
                log.AppendLine($"  + {name}: renderers {rc}, colliders {cc}, LODs {lv}{(wind ? ", wind" : "")}{(emission != null ? ", emission" : "")}");
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
