using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    public static partial class StageLayoutBuilder
    {
        /// <summary>
        /// Colours by role (CLAUDE.md 5): player blue; terrain green/brown/grey; hazards red/purple/orange;
        /// collectibles gold coins and golden leaves; helpers teal and green.
        /// </summary>
        internal static class Palette
        {
            public static readonly Color Ground = new Color(0.35f, 0.62f, 0.30f);
            public static readonly Color Platform = new Color(0.62f, 0.45f, 0.28f);
            public static readonly Color Wall = new Color(0.50f, 0.52f, 0.58f);
            public static readonly Color Spike = new Color(0.90f, 0.12f, 0.12f);
            public static readonly Color Faller = new Color(0.95f, 0.45f, 0.05f);
            public static readonly Color Coin = new Color(1.00f, 0.78f, 0.05f);
            public static readonly Color Special = new Color(1.00f, 0.76f, 0.20f);
            public static readonly Color Mover = new Color(0.10f, 0.65f, 0.65f);
            public static readonly Color CheckpointPole = new Color(0.75f, 0.75f, 0.78f);
            public static readonly Color CheckpointOff = new Color(0.45f, 0.45f, 0.50f);
            public static readonly Color CheckpointOn = new Color(0.20f, 0.95f, 0.40f);
            public static readonly Color Goal = new Color(1f, 0.85f, 0.2f);
        }

        private static string PrefabPath(string name) => $"{LevelPrefabDir}/{name}.prefab";

        private static Material Mat(string name, Color color, bool emissive = false)
        {
            var m = SceneFlowSetup.LitMaterial(name, color);
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 0.6f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Creates (or refreshes) the gimmick prefabs, the spike mesh and their materials. Safe to run repeatedly.</summary>
        private static void EnsureSharedAssets()
        {
            // A fresh scene to build prefab sources in; nothing here is saved to a scene.
            SceneFlowSetup.NewScene();

            EnsureSpikeMesh();

            var ground = Mat("M_Ground", Palette.Ground);
            var platform = Mat("M_Platform", Palette.Platform);
            Mat("M_Wall", Palette.Wall);
            Mat("M_Spike", Palette.Spike);
            var faller = Mat("M_Faller", Palette.Faller);
            var coin = Mat("M_Coin", Palette.Coin, true);
            var special = Mat("M_Special", Palette.Special, true);
            var mover = Mat("M_Mover", Palette.Mover);
            var pole = Mat("M_CheckpointPole", Palette.CheckpointPole);
            var off = Mat("M_CheckpointOff", Palette.CheckpointOff);
            var on = Mat("M_CheckpointOn", Palette.CheckpointOn, true);
            var goal = Mat("M_Goal", Palette.Goal);
            _ = ground; _ = platform;

            SavePrefab(MakeCoin(coin), "Coin");
            SavePrefab(MakeSpecial(special), "SpecialCollectible");
            SavePrefab(MakeCheckpoint(pole, off, on), "Checkpoint");
            SavePrefab(MakeSpikes(), "Spikes");
            SavePrefab(MakeMover(mover), "Mover");
            SavePrefab(MakeFaller(faller), "Faller");
            SavePrefab(MakeGoal(goal), "Goal");
            SavePrefab(MakeSpikeTrap(), "SpikeTrap");
            SavePrefab(MakeFloorSwitch(Mat("M_SwitchPlate", new Color(1f, 0.85f, 0.25f)), Mat("M_SwitchBase", new Color(0.35f, 0.33f, 0.3f))), "FloorSwitch");
            SavePrefab(MakeSlidingGate(Mat("M_GateSlab", new Color(0.62f, 0.5f, 0.32f))), "SlidingGate");
            BossSetup.EnsureAssets();
        }

        private static void SavePrefab(GameObject root, string name)
        {
            string path = PrefabPath(name);
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
            UnityEngine.Object.DestroyImmediate(root);
            if (!success)
                throw new InvalidOperationException("Failed to save prefab " + path);
        }

        private static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider = false)
        {
            return SceneFlowSetup.CreatePrimitive(type, name, parent, pos, scale, mat, collider);
        }

        // ------------------------------------------------------------------ prefab sources

        private static GameObject MakeCoin(Material mat)
        {
            var root = new GameObject("Coin");
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;
            var spin = new GameObject("Spin").transform;
            spin.SetParent(root.transform, false);
            var disc = Prim(PrimitiveType.Cylinder, "Disc", spin, Vector3.zero, new Vector3(0.6f, 0.06f, 0.6f), mat);
            disc.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // flat face towards the camera
            var comp = root.AddComponent<Coin>();
            EditorWire.Set(comp, "spin", spin);
            return root;
        }

        private static GameObject MakeSpecial(Material mat)
        {
            var root = new GameObject("SpecialCollectible");
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.8f;
            var spin = new GameObject("Spin").transform;
            spin.SetParent(root.transform, false);
            var gem = new GameObject("GoldenLeaf");
            gem.transform.SetParent(spin,false);
            GoldenLeafAssets.Apply(gem);
            var comp = root.AddComponent<SpecialCollectible>();
            EditorWire.Set(comp, "spin", spin);
            SpecialCollectibleFx.Apply(root); // glow billboard + sparkles + collect burst
            return root;
        }

        public const string CheckpointFlagFbx = "Assets/_Project/Art/Environment/Common/CheckpointFlag.fbx";
        public const string CheckpointFlagEmblemMat = "Assets/_Project/Art/Environment/Common/Materials/M_CheckpointFlagEmblem.mat";

        /// <summary>
        /// Swallowtail cloth banner (1 x 0.6, pivot centre) instead of the cube, plus a star emblem child. The banner keeps a
        /// single material slot because Checkpoint swaps banner.sharedMaterial. No-op until the flag FBX is imported.
        /// </summary>
        internal static void ApplyFlagArt(GameObject banner)
        {
            var meshes = AssetDatabase.LoadAllAssetsAtPath(CheckpointFlagFbx).OfType<Mesh>().ToArray();
            var cloth = meshes.FirstOrDefault(m => m.name == "CheckpointFlag");
            var star = meshes.FirstOrDefault(m => m.name == "CheckpointFlagEmblem");
            if (cloth == null)
                return;
            banner.GetComponent<MeshFilter>().sharedMesh = cloth;
            banner.transform.localScale = Vector3.one;
            var col = banner.GetComponent<Collider>();
            if (col != null)
                UnityEngine.Object.DestroyImmediate(col);
            var emblemMat = AssetDatabase.LoadAssetAtPath<Material>(CheckpointFlagEmblemMat);
            if (star == null || emblemMat == null)
                return;
            var existing = banner.transform.Find("Emblem");
            var emblem = existing != null ? existing.gameObject : new GameObject("Emblem");
            emblem.transform.SetParent(banner.transform, false);
            var mf = emblem.GetComponent<MeshFilter>();
            if (mf == null) mf = emblem.AddComponent<MeshFilter>();
            mf.sharedMesh = star;
            var r = emblem.GetComponent<MeshRenderer>();
            if (r == null) r = emblem.AddComponent<MeshRenderer>();
            r.sharedMaterial = emblemMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static GameObject MakeCheckpoint(Material pole, Material off, Material on)
        {
            var root = new GameObject("Checkpoint");
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0f, 2f, 0f);
            col.size = new Vector3(1.5f, 4f, 3f);
            Prim(PrimitiveType.Cylinder, "Pole", root.transform, new Vector3(0f, 1.5f, 0f), new Vector3(0.12f, 1.5f, 0.12f), pole);
            var banner = Prim(PrimitiveType.Cube, "Banner", root.transform, new Vector3(0.55f, 1.4f, 0f), new Vector3(1.0f, 0.6f, 0.08f), off);
            ApplyFlagArt(banner);
            var respawn = new GameObject("RespawnPoint").transform;
            respawn.SetParent(root.transform, false);
            respawn.localPosition = new Vector3(0f, 1.2f, 0f);

            var comp = root.AddComponent<Checkpoint>();
            EditorWire.Set(comp, "respawnPoint", respawn);
            EditorWire.Set(comp, "banner", banner.GetComponent<MeshRenderer>());
            EditorWire.Set(comp, "inactiveMaterial", off);
            EditorWire.Set(comp, "activeMaterial", on);
            EditorWire.Set(comp, "bannerLowY", 1.4f);
            EditorWire.Set(comp, "bannerHighY", 2.7f);
            return root;
        }

        private static GameObject MakeSpikes()
        {
            // Spike units and the hit box size are added per instance (width comes from the layout).
            var root = new GameObject("Spikes");
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.225f, 0f);
            box.size = new Vector3(2f, 0.45f, SpikeDepth);
            root.AddComponent<HazardContact>();
            return root;
        }

        private static GameObject MakeMover(Material mat)
        {
            // Unit cube; the instance scale is (width, 0.5, 4). Kinematic body carries the player (see PlayerMotor).
            var root = Prim(PrimitiveType.Cube, "Mover", null, Vector3.zero, Vector3.one, mat, true);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            root.AddComponent<Mover>();
            return root;
        }

        private static GameObject MakeFaller(Material mat)
        {
            var root = new GameObject("Faller");
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * 1.4f;
            root.AddComponent<HazardContact>();

            var visual = new GameObject("Visual").transform; // two overlapping cubes = spiked star block
            visual.SetParent(root.transform, false);
            Prim(PrimitiveType.Cube, "BlockA", visual, Vector3.zero, new Vector3(1.1f, 1.1f, 1.1f), mat);
            var b = Prim(PrimitiveType.Cube, "BlockB", visual, Vector3.zero, new Vector3(1.1f, 1.1f, 1.1f), mat);
            b.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            var comp = root.AddComponent<Faller>();
            EditorWire.Set(comp, "visual", visual);
            return root;
        }

        private static GameObject MakeGoal(Material mat)
        {
            var root = Prim(PrimitiveType.Cube, "Goal", null, Vector3.zero, new Vector3(1.5f, 3f, 2f), mat, true);
            root.GetComponent<BoxCollider>().isTrigger = true;
            root.AddComponent<GoalTrigger>();
            return root;
        }

        // ------------------------------------------------------------------ spike mesh

        /// <summary>Four-sided pyramid, base 1 x 1, height 0.5, flat shaded. Scaled per instance by the builder.</summary>
        private static void EnsureSpikeMesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SpikeMeshPath);
            bool isNew = mesh == null;
            if (isNew)
                mesh = new Mesh { name = "SpikeUnit" };
            else
                mesh.Clear();

            var apex = new Vector3(0f, 0.5f, 0f);
            Vector3[] corners =
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f),
            };
            var verts = new Vector3[12];
            var tris = new int[12];
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = corners[i];
                Vector3 b = corners[(i + 1) % 4];
                Vector3 outward = (a + b) * 0.5f;
                // Unity front faces are clockwise: cross(v1 - v0, v2 - v0) points at the viewer.
                if (Vector3.Dot(Vector3.Cross(b - a, apex - a), outward) < 0f)
                {
                    Vector3 t = a; a = b; b = t;
                }
                verts[i * 3] = a;
                verts[i * 3 + 1] = b;
                verts[i * 3 + 2] = apex;
                tris[i * 3] = i * 3;
                tris[i * 3 + 1] = i * 3 + 1;
                tris[i * 3 + 2] = i * 3 + 2;
            }
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (isNew)
                AssetDatabase.CreateAsset(mesh, SpikeMeshPath);
            else
                EditorUtility.SetDirty(mesh);
        }
    }
}
