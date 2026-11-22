using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools
{
    /// <summary>
    /// World map v3 art importer (contract: Docs/art/worldmap-v3-contract.md).
    /// For every Models/Assets/WM_&lt;Asset&gt;.fbx: import settings, material M_WM_&lt;Asset&gt; (Toon Lit, or toon Wind Lit when "wind" is set in
    /// prop-placements.json) with T_WM_&lt;Asset&gt;_Albedo / _Emission, and Prefabs/WorldMap/Assets/WM_&lt;Asset&gt;.prefab with a LODGroup
    /// (LOD0/LOD1 by name suffix). Then WM_World.prefab is rebuilt = WM_World.fbx (+ "_Walk" MeshColliders, textured pieces) + nested asset
    /// prefab instances from the placement list. Safe to re-run: assets are updated in place, user tweaks of existing materials are kept.
    /// </summary>
    public static class WorldMapAssetImporter
    {
        public sealed class Config
        {
            public string ModelsDir = "Assets/_Project/Art/WorldMap/Models/Assets";
            public string ModelExtension = ".fbx";
            public string TextureDir = "Assets/_Project/Art/WorldMap/Textures";
            public string MaterialDir = "Assets/_Project/Art/WorldMap/Materials/v3";
            public string AssetPrefabDir = "Assets/_Project/Prefabs/WorldMap/Assets";
            public string PlacementsPath = "Assets/_Project/Data/WorldMap/prop-placements.json";
            public string WorldModelPath = "Assets/_Project/Art/WorldMap/Models/WM_World.fbx";
            public string WorldPrefabPath = WorldMapBuilder.WorldPrefabPath;
            public bool RebuildWorld = true;
        }

        public const string ToonShader = "MyGame/Toon/Lit";
        public const string UrpLitShader = "Universal Render Pipeline/Lit";
        private const float CullScreenHeight = 0.01f;
        private const float DefaultLod1Screen = 0.1f;
        private const int MaxTextureSize = 8192; // v4 terrain albedos are 8192 px
        private const int MobileTerrainMaxSize = 4096; // Android / iOS cap for the 8K terrain albedos
        private const int MobileMaxTextureSize = 2048; // Android / iOS / WebGL cap for all other world map textures
        private static readonly Regex LodToken = new Regex(@"_LOD(\d+)", RegexOptions.IgnoreCase);

        [MenuItem("Tools/MyGame/World Map/Import v3 Assets")]
        public static void ImportMenu() => Debug.Log(Run(new Config()));

        [MenuItem("Tools/MyGame/World Map/Import v3 Assets (assets only, keep WM_World.prefab)")]
        public static void ImportAssetsOnlyMenu() => Debug.Log(Run(new Config { RebuildWorld = false }));

        [MenuItem("Tools/MyGame/World Map/Import v3 Assets and Build World Map")]
        public static void ImportAndBuildMenu()
        {
            Debug.Log(Run(new Config()));
            Debug.Log(WorldMapBuilder.Build());
            string report = WorldMapVerifier.Verify(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        // ------------------------------------------------------------------ data

        private sealed class AssetInfo
        {
            public string Name;
            public bool Wind;
            public bool Emission;
            public float Lod1Screen = DefaultLod1Screen;
            public string ModelPath;
        }

        private sealed class Instance
        {
            public string Asset, Parent;
            public Vector3 Position, Scale = Vector3.one;
            public Quaternion Rotation = Quaternion.identity;
        }

        private sealed class Report
        {
            public int Assets, MissingModels, Prefabs, MaterialsCreated, MaterialsUpdated, Textures, Placed, Skipped, PiecesTextured, Colliders;
            public readonly List<string> MissingTextures = new List<string>();
            public readonly List<string> Warnings = new List<string>();

            public override string ToString()
            {
                var sb = new StringBuilder("[WorldMapAssetImporter]\n");
                sb.AppendLine($"  assets {Assets} (models missing {MissingModels}), prefabs {Prefabs}, materials +{MaterialsCreated} ~{MaterialsUpdated}, textures configured {Textures}");
                sb.AppendLine($"  world: instances placed {Placed}, skipped {Skipped}, textured pieces {PiecesTextured}, walk colliders {Colliders}");
                sb.AppendLine($"  missing textures {MissingTextures.Count}" + (MissingTextures.Count > 0 ? ": " + string.Join(", ", MissingTextures.Take(30)) + (MissingTextures.Count > 30 ? " ..." : "") : ""));
                foreach (var w in Warnings.Take(40))
                    sb.AppendLine("  warn: " + w);
                if (Warnings.Count > 40)
                    sb.AppendLine($"  ... {Warnings.Count - 40} more warnings");
                return sb.ToString();
            }
        }

        // ------------------------------------------------------------------ entry

        public static string Run(Config cfg)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before importing the world map assets.");
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var report = new Report();

            if (!LoadPlacements(cfg, report, out var assets, out var instances))
                return report + "  ABORTED: placements file is missing or invalid.";

            // models that exist but are not listed in the json use defaults
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { cfg.ModelsDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (!p.EndsWith(cfg.ModelExtension, StringComparison.OrdinalIgnoreCase)) continue;
                string n = Path.GetFileNameWithoutExtension(p);
                if (!assets.ContainsKey(n))
                {
                    report.Warnings.Add($"{n}: model not listed in placements json (defaults used)");
                    assets[n] = new AssetInfo { Name = n };
                }
            }
            foreach (var a in assets.Values)
            {
                a.ModelPath = $"{cfg.ModelsDir}/{a.Name}{cfg.ModelExtension}";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(a.ModelPath) == null)
                {
                    report.MissingModels++;
                    report.Warnings.Add($"{a.Name}: model not found at {a.ModelPath}");
                    a.ModelPath = null;
                }
            }
            var ready = assets.Values.Where(a => a.ModelPath != null).OrderBy(a => a.Name, StringComparer.Ordinal).ToList();
            report.Assets = ready.Count;

            // ---- phase 1: import settings (batched, one reimport)
            AssetDatabase.StartAssetEditing();
            try
            {
                ConfigureTextures(cfg, report);
                var reference = AssetImporter.GetAtPath(cfg.WorldModelPath) as ModelImporter;
                foreach (var a in ready)
                    ConfigureModel(a.ModelPath, reference);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // ---- phase 2: materials + prefabs
            SceneFlowSetup.EnsureFolder(cfg.MaterialDir);
            SceneFlowSetup.EnsureFolder(cfg.AssetPrefabDir);
            var prefabs = new Dictionary<string, GameObject>();
            var preview = EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (var a in ready)
                {
                    try
                    {
                        var prefab = BuildAssetPrefab(cfg, a, preview, report);
                        if (prefab != null) { prefabs[a.Name] = prefab; report.Prefabs++; }
                    }
                    catch (Exception e)
                    {
                        report.Warnings.Add($"{a.Name}: prefab build failed - {e.Message}");
                        Debug.LogException(e);
                    }
                }

                if (cfg.RebuildWorld)
                    RebuildWorld(cfg, prefabs, instances, preview, report);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report.ToString();
        }

        // ------------------------------------------------------------------ placements

        private static bool LoadPlacements(Config cfg, Report report, out Dictionary<string, AssetInfo> assets, out List<Instance> instances)
        {
            assets = new Dictionary<string, AssetInfo>();
            instances = new List<Instance>();
            if (!File.Exists(cfg.PlacementsPath))
            {
                report.Warnings.Add("placements file not found: " + cfg.PlacementsPath);
                return false;
            }
            try
            {
                var root = JObject.Parse(File.ReadAllText(cfg.PlacementsPath));
                foreach (var a in root["assets"] ?? new JArray())
                {
                    string name = (string)a["name"];
                    if (string.IsNullOrEmpty(name)) { report.Warnings.Add("asset entry without a name"); continue; }
                    float lod1 = a["lod1_screen"] != null ? (float)a["lod1_screen"] : DefaultLod1Screen;
                    if (!(lod1 > CullScreenHeight && lod1 < 1f))
                    {
                        report.Warnings.Add($"{name}: lod1_screen {lod1} outside ({CullScreenHeight}, 1), using {DefaultLod1Screen}");
                        lod1 = DefaultLod1Screen;
                    }
                    assets[name] = new AssetInfo { Name = name, Wind = a["wind"] != null && (bool)a["wind"], Emission = a["emission"] != null && (bool)a["emission"], Lod1Screen = lod1 };
                }
                foreach (var i in root["instances"] ?? new JArray())
                {
                    var inst = new Instance { Asset = (string)i["asset"], Parent = (string)i["parent"] };
                    var p = i["position"]; var r = i["rotation"]; var s = i["scale"];
                    if (string.IsNullOrEmpty(inst.Asset) || p == null || p.Count() != 3) { report.Skipped++; continue; }
                    inst.Position = new Vector3((float)p[0], (float)p[1], (float)p[2]);
                    if (r != null && r.Count() == 4)
                    {
                        var q = new Quaternion((float)r[0], (float)r[1], (float)r[2], (float)r[3]);
                        float mag = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                        inst.Rotation = mag > 1e-4f ? new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag) : Quaternion.identity;
                    }
                    if (s != null && s.Count() == 3)
                        inst.Scale = new Vector3((float)s[0], (float)s[1], (float)s[2]);
                    instances.Add(inst);
                }
            }
            catch (Exception e)
            {
                report.Warnings.Add("placements json invalid: " + e.Message);
                return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ import settings

        private static void ConfigureTextures(Config cfg, Report report)
        {
            if (!AssetDatabase.IsValidFolder(cfg.TextureDir)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { cfg.TextureDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).StartsWith("T_WM_", StringComparison.Ordinal)) continue;
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;

                int size = ReadPngMaxSide(path);
                int max = size > 0 ? Mathf.Clamp(Mathf.NextPowerOfTwo(size), 32, MaxTextureSize) : 2048;
                bool dirty = false;
                void Set<T>(T current, T wanted, Action apply)
                {
                    if (EqualityComparer<T>.Default.Equals(current, wanted)) return;
                    apply();
                    dirty = true;
                }
                Set(ti.textureType, TextureImporterType.Default, () => ti.textureType = TextureImporterType.Default);
                Set(ti.sRGBTexture, true, () => ti.sRGBTexture = true);
                Set(ti.mipmapEnabled, true, () => ti.mipmapEnabled = true);
                Set(ti.maxTextureSize, max, () => ti.maxTextureSize = max);
                Set(ti.textureCompression, TextureImporterCompression.Compressed, () => ti.textureCompression = TextureImporterCompression.Compressed);
                Set(ti.crunchedCompression, false, () => ti.crunchedCompression = false);
                Set(ti.wrapMode, TextureWrapMode.Clamp, () => ti.wrapMode = TextureWrapMode.Clamp);
                Set(ti.filterMode, FilterMode.Bilinear, () => ti.filterMode = FilterMode.Bilinear);
                Set(ti.anisoLevel, 4, () => ti.anisoLevel = 4);
                Set(ti.isReadable, false, () => ti.isReadable = false);
                // mobile / web budgets: 8K terrain albedos drop to 4096, everything else to 2048 (Standalone keeps the default size)
                int mobileMax = Mathf.Min(max, max >= 8192 ? MobileTerrainMaxSize : MobileMaxTextureSize);
                foreach (var platform in new[] { "Android", "iPhone", "WebGL" })
                {
                    int wanted = platform == "WebGL" ? Mathf.Min(max, MobileMaxTextureSize) : mobileMax;
                    var ps = ti.GetPlatformTextureSettings(platform);
                    if (ps.overridden && ps.maxTextureSize == wanted && ps.format == TextureImporterFormat.Automatic) continue;
                    ps.overridden = true;
                    ps.maxTextureSize = wanted;
                    ps.format = TextureImporterFormat.Automatic;
                    ps.textureCompression = TextureImporterCompression.Compressed;
                    ti.SetPlatformTextureSettings(ps);
                    dirty = true;
                }
                if (dirty)
                {
                    ti.SaveAndReimport();
                    report.Textures++;
                }
            }
        }

        /// <summary>Longest side of a PNG read from its IHDR chunk (0 when unreadable).</summary>
        private static int ReadPngMaxSide(string assetPath)
        {
            try
            {
                using var fs = File.OpenRead(assetPath);
                var h = new byte[24];
                if (fs.Read(h, 0, 24) < 24 || h[1] != 'P' || h[2] != 'N' || h[3] != 'G') return 0;
                int w = (h[16] << 24) | (h[17] << 16) | (h[18] << 8) | h[19];
                int hh = (h[20] << 24) | (h[21] << 16) | (h[22] << 8) | h[23];
                return Mathf.Max(w, hh);
            }
            catch (IOException) { return 0; }
        }

        private static void ConfigureModel(string path, ModelImporter reference)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) return;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // materials come from M_WM_* assets
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;                // toon shader has no normal map
            mi.importBlendShapes = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.generateSecondaryUV = false; // no lightmap UVs needed
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.weldVertices = true;
            mi.preserveHierarchy = true;
            mi.addCollider = false;
            if (reference != null) // same axis / unit recipe as WM_World.fbx
            {
                mi.useFileScale = reference.useFileScale;
                mi.globalScale = reference.globalScale;
                mi.bakeAxisConversion = reference.bakeAxisConversion;
            }
            mi.SaveAndReimport();
        }

        // ------------------------------------------------------------------ asset prefab

        private sealed class Part
        {
            public Transform Node;
            public MeshFilter Filter;
            public int Lod;
            public string Group;
            public MotionKind Motion;
        }

        private enum MotionKind { None, Spin, Bob }

        private static GameObject BuildAssetPrefab(Config cfg, AssetInfo a, Scene scene, Report report)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(a.ModelPath);
            var parts = new List<Part>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.name;
                var m = LodToken.Match(n);
                parts.Add(new Part
                {
                    Node = mf.transform,
                    Filter = mf,
                    Lod = m.Success ? int.Parse(m.Groups[1].Value) : 0,
                    Group = LodToken.Replace(n, string.Empty),
                    Motion = n.Contains("_Spin") ? MotionKind.Spin : n.Contains("_Bob") ? MotionKind.Bob : MotionKind.None,
                });
            }
            if (parts.Count == 0)
            {
                report.Warnings.Add($"{a.Name}: model has no meshes");
                return null;
            }
            bool hasLod1 = parts.Any(p => p.Lod >= 1);
            if (parts.Any(p => p.Lod > 1))
                report.Warnings.Add($"{a.Name}: LOD index above 1 ignored");
            if (!parts.Any(p => p.Lod == 0))
            {
                report.Warnings.Add($"{a.Name}: no LOD0 mesh");
                return null;
            }

            var tex = LoadTexture(cfg, $"T_{a.Name}_Albedo.png");
            var emissionTex = LoadTexture(cfg, $"T_{a.Name}_Emission.png");
            if (tex == null) report.MissingTextures.Add($"T_{a.Name}_Albedo");
            if (a.Emission && emissionTex == null) report.MissingTextures.Add($"T_{a.Name}_Emission");

            var lod0Meshes = parts.Where(p => p.Lod == 0).Select(p => p.Filter.sharedMesh).ToList();
            bool hasColours = lod0Meshes.All(m => m.HasVertexAttribute(VertexAttribute.Color));
            if (a.Wind && !hasColours)
                report.Warnings.Add($"{a.Name}: wind asset has no vertex colours, height weighting used");

            var main = EnsureMaterial(cfg, a.Name, a.Wind ? WorldMapAmbient.WindShader : ToonShader, tex, emissionTex, a, hasColours, lod0Meshes, report);
            Material transparent = null;

            var root = new GameObject(a.Name);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                var lodRenderers = new List<Renderer>[] { new List<Renderer>(), new List<Renderer>() };
                var lodRoots = new Transform[2];
                for (int i = 0; i < 2; i++)
                {
                    var g = new GameObject("LOD" + i);
                    g.transform.SetParent(root.transform, false);
                    lodRoots[i] = g.transform;
                }
                var motionPivots = new Dictionary<string, Transform>();
                var lod1Groups = new HashSet<string>(parts.Where(p => p.Lod == 1).Select(p => p.Group));
                Matrix4x4 toRoot = model.transform.worldToLocalMatrix;

                foreach (var p in parts.Where(p => p.Lod <= 1).OrderBy(p => p.Lod).ThenBy(p => p.Node.name, StringComparer.Ordinal))
                {
                    Matrix4x4 local = toRoot * p.Node.localToWorldMatrix;
                    Transform parent = p.Motion == MotionKind.None ? lodRoots[p.Lod] : GetPivot(root.transform, motionPivots, p.Group, local);
                    Matrix4x4 rel = p.Motion == MotionKind.None ? local : parent.worldToLocalMatrix * (root.transform.localToWorldMatrix * local);

                    var go = new GameObject(p.Motion == MotionKind.None ? p.Node.name : "LOD" + p.Lod);
                    go.transform.SetParent(parent, false);
                    go.transform.localPosition = rel.GetPosition();
                    go.transform.localRotation = rel.rotation;
                    go.transform.localScale = rel.lossyScale;
                    go.AddComponent<MeshFilter>().sharedMesh = p.Filter.sharedMesh;
                    var r = go.AddComponent<MeshRenderer>();
                    var mats = new Material[Mathf.Max(1, p.Filter.sharedMesh.subMeshCount)];
                    for (int s = 0; s < mats.Length; s++)
                    {
                        if (s == 1 && mats.Length == 2)
                            mats[s] = transparent ??= EnsureTransparentMaterial(cfg, a.Name, tex, report);
                        else
                            mats[s] = main;
                    }
                    r.sharedMaterials = mats;
                    if (a.Emission) go.AddComponent<AmbientPulse>();
                    lodRenderers[p.Lod].Add(r);
                    // a moving part without its own LOD1 mesh keeps its LOD0 mesh in both levels
                    if (p.Lod == 0 && hasLod1 && p.Motion != MotionKind.None && !lod1Groups.Contains(p.Group))
                        lodRenderers[1].Add(r);
                }
                if (lodRenderers[0].Count == 0) throw new InvalidOperationException("no LOD0 renderers");
                if (lodRoots[1].childCount == 0 && !lodRenderers[1].Any()) UnityEngine.Object.DestroyImmediate(lodRoots[1].gameObject);
                if (lodRoots[0].childCount == 0) UnityEngine.Object.DestroyImmediate(lodRoots[0].gameObject);

                var group = root.AddComponent<LODGroup>();
                LOD[] lods = hasLod1 && lodRenderers[1].Count > 0
                    ? new[] { new LOD(a.Lod1Screen, lodRenderers[0].ToArray()), new LOD(CullScreenHeight, lodRenderers[1].ToArray()) }
                    : new[] { new LOD(CullScreenHeight, lodRenderers[0].ToArray()) };
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.None;
                group.RecalculateBounds();

                WorldMapAmbient.ConfigureMotion(root);

                string path = $"{cfg.AssetPrefabDir}/{a.Name}.prefab";
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
                if (!ok) throw new InvalidOperationException("SaveAsPrefabAsset failed for " + path);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Transform GetPivot(Transform root, Dictionary<string, Transform> pivots, string group, Matrix4x4 local)
        {
            if (pivots.TryGetValue(group, out var t)) return t;
            var go = new GameObject(group);
            go.transform.SetParent(root, false);
            go.transform.localPosition = local.GetPosition();
            go.transform.localRotation = local.rotation;
            go.transform.localScale = Vector3.one; // renderers carry the scale
            pivots[group] = go.transform;
            return go.transform;
        }

        private static Texture2D LoadTexture(Config cfg, string file) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{cfg.TextureDir}/{file}");

        // ------------------------------------------------------------------ materials

        private static (float strength, float speed, float flutter, bool twoSided) WindFor(string asset)
        {
            string n = asset.ToLowerInvariant();
            if (n.Contains("banner") || n.Contains("flag")) return (0.22f, 2.8f, 0.45f, true);
            if (n.Contains("sail") || n.Contains("boat")) return (0.08f, 1.3f, 0.10f, true);
            if (n.Contains("palm")) return (0.07f, 1.0f, 0.05f, true);
            if (n.Contains("flower")) return (0.03f, 1.4f, 0.08f, false);
            if (n.Contains("grass")) return (0.05f, 1.6f, 0.10f, true);
            if (n.Contains("pine")) return (0.025f, 0.7f, 0.0f, false);
            return (0.035f, 0.8f, 0.0f, false);
        }

        private static Material EnsureMaterial(Config cfg, string asset, string shaderName, Texture2D albedo, Texture2D emission,
            AssetInfo info, bool hasColours, List<Mesh> lod0, Report report)
        {
            string path = $"{cfg.MaterialDir}/M_{asset}.mat";
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Shader not found: " + shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool fresh = mat == null || mat.shader != shader;
            if (mat == null)
            {
                mat = new Material(shader) { name = $"M_{asset}" };
                AssetDatabase.CreateAsset(mat, path);
                report.MaterialsCreated++;
            }
            else
            {
                if (mat.shader != shader) mat.shader = shader;
                report.MaterialsUpdated++;
            }
            mat.SetTexture("_BaseMap", albedo);
            if (fresh && albedo == null && mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.75f, 0.75f, 0.75f, 1f));
            if (emission != null)
            {
                mat.SetTexture("_EmissionMap", emission);
                if (fresh || mat.GetColor("_EmissionColor").maxColorComponent <= 0f)
                    mat.SetColor("_EmissionColor", Color.white);
            }
            else
            {
                mat.SetTexture("_EmissionMap", null);
                mat.SetColor("_EmissionColor", Color.black);
            }
            mat.enableInstancing = true;
            if (shaderName == WorldMapAmbient.WindShader)
            {
                if (fresh)
                {
                    var w = WindFor(asset);
                    mat.SetFloat("_WindStrength", w.strength);
                    mat.SetFloat("_WindSpeed", w.speed);
                    mat.SetFloat("_Flutter", w.flutter);
                    mat.SetFloat("_Cull", (float)(w.twoSided ? CullMode.Off : CullMode.Back));
                }
                mat.SetFloat("_HeightWeight", hasColours ? 0f : 1f);
                if (!hasColours && lod0.Count > 0)
                {
                    float lo = lod0.Min(m => m.bounds.min.y), hi = lod0.Max(m => m.bounds.max.y);
                    mat.SetVector("_HeightRange", new Vector4(lo + (hi - lo) * 0.25f, hi, 0f, 0f));
                }
            }
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat); // write now: later imports must not reload the stale on-disk copy
            return mat;
        }

        /// <summary>Second slot for see-through parts (glass, ice): URP Lit, alpha blended, no shadows.</summary>
        private static Material EnsureTransparentMaterial(Config cfg, string asset, Texture2D albedo, Report report)
        {
            string path = $"{cfg.MaterialDir}/M_{asset}_Transparent.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(UrpLitShader) ?? throw new InvalidOperationException("URP Lit not found")) { name = $"M_{asset}_Transparent" };
                AssetDatabase.CreateAsset(mat, path);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetFloat("_Smoothness", 0.2f);
                mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.6f));
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetShaderPassEnabled("ShadowCaster", false);
                mat.renderQueue = (int)RenderQueue.Transparent;
                report.MaterialsCreated++;
            }
            else
            {
                report.MaterialsUpdated++;
            }
            mat.SetTexture("_BaseMap", albedo);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssetIfDirty(mat); // write now: later imports must not reload the stale on-disk copy
            return mat;
        }

        // ------------------------------------------------------------------ world prefab

        private static void RebuildWorld(Config cfg, Dictionary<string, GameObject> prefabs, List<Instance> instances, Scene scene, Report report)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(cfg.WorldModelPath);
            if (model == null)
            {
                report.Warnings.Add("world model not found, WM_World.prefab untouched: " + cfg.WorldModelPath);
                return;
            }
            // keep the previous (v2) prefab once, so the old art can be restored
            string backup = cfg.WorldPrefabPath.Replace(".prefab", "_v2_backup.prefab");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(cfg.WorldPrefabPath) != null && AssetDatabase.LoadAssetAtPath<GameObject>(backup) == null)
                AssetDatabase.CopyAsset(cfg.WorldPrefabPath, backup);

            var world = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            try
            {
                world.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                world.transform.localScale = Vector3.one;
                var byName = new Dictionary<string, Transform>();
                foreach (var t in world.GetComponentsInChildren<Transform>(true))
                    if (!byName.ContainsKey(t.name)) byName[t.name] = t;

                SceneFlowSetup.EnsureFolder(cfg.MaterialDir);
                foreach (var mf in world.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.name.EndsWith(WorldMapBuilder.WalkSuffix, StringComparison.Ordinal) && mf.GetComponent<Collider>() == null)
                    {
                        mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                        report.Colliders++;
                    }
                    ApplyPieceMaterial(cfg, mf, report);
                }

                var missingParents = new HashSet<string>();
                var counters = new Dictionary<string, int>();
                foreach (var inst in instances)
                {
                    if (!prefabs.TryGetValue(inst.Asset, out var prefab))
                    {
                        report.Skipped++;
                        if (missingParents.Add("asset:" + inst.Asset))
                            report.Warnings.Add($"instances of {inst.Asset} skipped (no prefab)");
                        continue;
                    }
                    Transform parent = world.transform;
                    if (!string.IsNullOrEmpty(inst.Parent))
                    {
                        if (!byName.TryGetValue(inst.Parent, out parent))
                        {
                            var g = new GameObject(inst.Parent);
                            g.transform.SetParent(world.transform, false);
                            byName[inst.Parent] = parent = g.transform;
                            report.Warnings.Add($"parent group '{inst.Parent}' not in WM_World.fbx, created empty");
                        }
                    }
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    counters[inst.Asset] = counters.TryGetValue(inst.Asset, out int c) ? c + 1 : 1;
                    go.name = $"{inst.Asset}_{counters[inst.Asset]:D3}";
                    go.transform.SetPositionAndRotation(inst.Position, inst.Rotation);
                    go.transform.localScale = inst.Scale;
                    report.Placed++;
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(world, cfg.WorldPrefabPath, out bool ok);
                if (!ok || saved == null)
                    report.Warnings.Add("saving world prefab failed: " + cfg.WorldPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(world);
            }
        }

        /// <summary>
        /// World pieces share group atlases (e.g. terrain, paths and joints of a region all use T_WM_Terrain_R01). The group is taken from the
        /// slot's source material name M_WM_&lt;Group&gt; (falls back to the object name). A slot whose group has T_WM_&lt;Group&gt;_Albedo.png gets
        /// M_WM_&lt;Group&gt; (Toon Lit); water, lava and transparent slots and untextured pieces keep their v2 materials.
        /// </summary>
        private static void ApplyPieceMaterial(Config cfg, MeshFilter mf, Report report)
        {
            if (!mf.TryGetComponent<MeshRenderer>(out var r)) return;
            var slots = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < slots.Length; i++)
            {
                string n = slots[i] != null ? slots[i].name : string.Empty;
                bool keep = n.EndsWith("_Transparent", StringComparison.Ordinal) || n.Contains("Water") || n.Contains("Foam") || n.Contains("Lava");
                if (keep) continue;
                string group = n.StartsWith("M_WM_", StringComparison.Ordinal) ? n.Substring(5) : mf.name;
                var tex = LoadTexture(cfg, $"T_WM_{group}_Albedo.png");
                if (tex == null) continue;
                slots[i] = EnsureMaterial(cfg, "WM_" + group, ToonShader, tex, LoadTexture(cfg, $"T_WM_{group}_Emission.png"),
                    new AssetInfo { Name = group }, true, new List<Mesh>(), report);
                changed = true;
            }
            if (!changed) return;
            r.sharedMaterials = slots;
            report.PiecesTextured++;
        }
    }
}
