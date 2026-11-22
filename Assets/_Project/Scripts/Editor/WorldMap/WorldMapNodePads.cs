using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// World map node pads v2 (carved stone pedestal, gold trim, glowing inset + ground halo).
    /// Source: ArtSource/WorldMap/WM_NodePads_v2.blend -> Art/WorldMap/Models/NodePads/WM_NodePad_{Normal,Boss}.fbx.
    /// Menu: Tools/MyGame/World Map/Upgrade Node Pads (builds materials/prefabs, swaps the pads in 05_WorldMap in place).
    /// WorldMapBuilder and SceneFlowSetup create nodes through <see cref="Attach"/>.
    /// </summary>
    public static class WorldMapNodePads
    {
        private const string ModelDir = "Assets/_Project/Art/WorldMap/Models/NodePads/";
        private const string MaterialDir = "Assets/_Project/Art/WorldMap/Materials/NodePads/";
        private const string ToonShader = "MyGame/Toon/Lit";
        private const string RingShader = "MyGame/WorldMap/NodeRing";

        public static readonly Color NormalAccent = new Color32(0x36, 0xAE, 0xEA, 0xFF);
        public static readonly Color BossAccent = new Color32(0xEF, 0x60, 0x48, 0xFF);
        public static readonly Color StoneColor = new Color(0.80f, 0.83f, 0.90f);

        /// <summary>Pad outer radius (m) the halo is sized against.</summary>
        private const float NormalRadius = 0.83f, BossRadius = 1.0f;
        /// <summary>Halo quad edge = radius / this (the rune ring sits just outside the pad).</summary>
        private const float RingFit = 0.64f;
        private const float RingHeight = -0.01f; // pad-local; +6 cm over the anchor so uneven plaza edges never clip the halo
        /// <summary>
        /// The node anchor sits 4-8 cm above the walk ground; the pad model's block ring tops out at -3.5 cm, so it is
        /// raised to stand proud of the ground plaza (the avatar's feet sink ~7 cm into the inset, invisible from the map camera).
        /// </summary>
        private const float PadLift = 0.07f;

        private static string PrefabPath(bool boss) => WorldMapBuilder.PrefabDir + (boss ? "WM_NodePadBoss.prefab" : "WM_NodePadNormal.prefab");

        [MenuItem("Tools/MyGame/World Map/Upgrade Node Pads")]
        public static void UpgradeMenu() => Debug.Log(UpgradeScene());

        /// <summary>Rebuilds the pad assets and replaces every node's "Marker" in 05_WorldMap.</summary>
        public static string UpgradeScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before upgrading the node pads.");
            EnsureAssets();
            // Open additively so other open (possibly dirty) scenes are neither saved nor discarded.
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(WorldMapBuilder.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(WorldMapBuilder.ScenePath, OpenSceneMode.Additive);
            var nodes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<WorldMapNode>(true)).ToArray();
            foreach (var node in nodes)
            {
                var old = node.transform.Find("Marker");
                if (old != null)
                    UnityEngine.Object.DestroyImmediate(old.gameObject);
                Attach(node, node.Data.NodeId.EndsWith("_B", StringComparison.Ordinal));
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (opened)
                EditorSceneManager.CloseScene(scene, true);
            return $"[WorldMapNodePads] upgraded {nodes.Length} nodes in {WorldMapBuilder.ScenePath}";
        }

        /// <summary>Instantiates the pad prefab as the node's "Marker" and wires the node view.</summary>
        public static GameObject Attach(WorldMapNode node, bool boss)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(boss));
            if (prefab == null)
            {
                EnsureAssets();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(boss))
                         ?? throw new InvalidOperationException("Node pad prefab could not be created: " + PrefabPath(boss));
            }
            var pad = (GameObject)PrefabUtility.InstantiatePrefab(prefab, node.transform);
            pad.name = "Marker";
            pad.transform.localPosition = new Vector3(0f, PadLift, 0f);
            pad.transform.SetAsFirstSibling();

            var padRenderer = pad.transform.Find("Model").GetComponentInChildren<MeshRenderer>();
            var mats = padRenderer.sharedMaterials;
            EditorWire.Set(node, "padRenderer", padRenderer);
            EditorWire.Set(node, "stoneSlot", Slot(mats, "Stone"));
            EditorWire.Set(node, "trimSlot", Slot(mats, "Trim"));
            EditorWire.Set(node, "insetSlot", Slot(mats, "Inset"));
            EditorWire.Set(node, "ringRenderer", pad.transform.Find("Ring").GetComponent<MeshRenderer>());
            var theme = ThemeFor(node.Data.NodeId);
            EditorWire.Set(node, "accentColor", boss ? theme.bossAccent : theme.accent);
            EditorWire.Set(node, "stoneColor", theme.stone);
            EditorWire.Set(node, "trimColor", theme.trim);
            EditorWire.Set(node, "lockedStoneColor", Faded(theme.stone, 0.62f));
            EditorWire.Set(node, "lockedTrimColor", Faded(theme.trim, 0.55f));
            return pad;
        }

        // ------------------------------------------------------------------ world themes

        public readonly struct Theme
        {
            public readonly string prefix;
            public readonly Color stone, trim, accent, bossAccent;
            public Theme(string prefix, Color stone, Color trim, Color accent, Color bossAccent)
            {
                this.prefix = prefix; this.stone = stone; this.trim = trim; this.accent = accent; this.bossAccent = bossAccent;
            }
        }

        private static readonly Color Gold = new Color(1f, 0.76f, 0.30f);

        /// <summary>
        /// Per-world pad palette (longest node-id prefix wins). Stone contrasts with the cream node plazas and echoes
        /// the world's ground; the cleared inset stays gold in every world so "cleared" always reads the same.
        /// </summary>
        public static readonly Theme[] Themes =
        {
            new Theme("W1", new Color(0.60f, 0.78f, 0.56f), Gold, NormalAccent, BossAccent),                                            // Green Meadow: mossy stone
            new Theme("W2", new Color(0.90f, 0.60f, 0.42f), Gold, new Color(0.10f, 0.78f, 0.76f), BossAccent),                          // Sun Dunes: terracotta, turquoise
            new Theme("W3", new Color(0.66f, 0.81f, 0.98f), new Color(0.88f, 0.92f, 1f), new Color(0.35f, 0.86f, 1f), BossAccent),      // Frost Peaks: ice stone, silver
            new Theme("W4", new Color(0.98f, 0.67f, 0.62f), new Color(0.98f, 0.93f, 0.84f), new Color(0.08f, 0.80f, 0.70f), BossAccent),// Coral Coast: coral, pearl, aqua
            new Theme("W5", new Color(0.33f, 0.31f, 0.37f), new Color(0.95f, 0.56f, 0.26f), new Color(1f, 0.50f, 0.15f), new Color(0.95f, 0.18f, 0.32f)), // Ember Fortress: basalt, bronze, lava
        };

        private static readonly Theme DefaultTheme = new Theme("", StoneColor, Gold, NormalAccent, BossAccent);

        public static Theme ThemeFor(string nodeId)
        {
            var best = DefaultTheme;
            foreach (var t in Themes)
                if (nodeId.StartsWith(t.prefix, StringComparison.Ordinal) && t.prefix.Length > best.prefix.Length)
                    best = t;
            return best;
        }

        /// <summary>Locked variant: mostly desaturated and darkened, keeping a hint of the world hue.</summary>
        private static Color Faded(Color c, float brightness)
        {
            float g = c.grayscale;
            var f = Color.Lerp(new Color(g, g, g), c, 0.25f) * brightness;
            f.a = 1f;
            return f;
        }

        private static int Slot(Material[] mats, string key)
        {
            int i = Array.FindIndex(mats, m => m != null && m.name.Contains(key));
            if (i < 0)
                throw new InvalidOperationException($"Node pad has no '{key}' material slot ({string.Join(", ", mats.Select(m => m ? m.name : "null"))}).");
            return i;
        }

        // ------------------------------------------------------------------ assets

        public static void EnsureAssets()
        {
            if (!AssetDatabase.IsValidFolder(MaterialDir.TrimEnd('/')))
                AssetDatabase.CreateFolder("Assets/_Project/Art/WorldMap/Materials", "NodePads");

            var stone = ToonMaterial("M_NodePadStone", StoneColor, new Color(0.30f, 0.30f, 0.34f));
            var trim = ToonMaterial("M_NodePadTrim", new Color(1f, 0.76f, 0.30f), new Color(0.55f, 0.45f, 0.25f));
            var inset = ToonMaterial("M_NodePadInset", NormalAccent, new Color(0.45f, 0.55f, 0.7f));
            var ring = RingMaterial();

            BuildPrefab(false, stone, trim, inset, ring);
            BuildPrefab(true, stone, trim, inset, ring);
            AssetDatabase.SaveAssets();
        }

        private static void BuildPrefab(bool boss, Material stone, Material trim, Material inset, Material ring)
        {
            string fbx = ModelDir + (boss ? "WM_NodePad_Boss.fbx" : "WM_NodePad_Normal.fbx");
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx)
                           ?? throw new InvalidOperationException("Missing node pad model: " + fbx);
            if (importer.importAnimation || importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription
                || importer.importCameras || importer.importLights)
            {
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);

            var root = new GameObject(boss ? "WM_NodePadBoss" : "WM_NodePadNormal");
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                inst.name = "Model";
                var mr = inst.GetComponentInChildren<MeshRenderer>();
                // Slot order follows the FBX; map each imported slot to our material by its name.
                mr.sharedMaterials = mr.sharedMaterials.Select(m =>
                    m != null && m.name.Contains("Trim") ? trim :
                    m != null && m.name.Contains("Inset") ? inset : stone).ToArray();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                var quad = new GameObject("Ring");
                quad.transform.SetParent(root.transform, false);
                float size = 2f * (boss ? BossRadius : NormalRadius) / RingFit;
                quad.transform.localPosition = new Vector3(0f, RingHeight, 0f);
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localScale = new Vector3(size, size, 1f);
                quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var qr = quad.AddComponent<MeshRenderer>();
                qr.sharedMaterial = ring;
                qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                qr.receiveShadows = false;

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(boss));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material ToonMaterial(string name, Color color, Color rim)
        {
            var mat = LoadOrCreate(name, ToonShader);
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetColor("_RimColor", rim);
            mat.SetFloat("_RimPower", 3.5f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material RingMaterial()
        {
            var mat = LoadOrCreate("M_NodeRing", RingShader);
            mat.SetColor("_BaseColor", NormalAccent);
            mat.renderQueue = 3050;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material LoadOrCreate(string name, string shaderName)
        {
            var shader = Shader.Find(shaderName) ?? throw new InvalidOperationException("Shader not found: " + shaderName);
            string path = MaterialDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
                mat.shader = shader;
            return mat;
        }
    }
}
