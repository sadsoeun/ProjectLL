using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>Lists the boss data assets (Data/Bosses/Boss_{id}.asset) for the map tool and the validator.</summary>
    internal static class BossCatalog
    {
        public const string Dir = "Assets/_Project/Data/Bosses";

        public static string PathFor(int bossId) => $"{Dir}/Boss_{bossId}.asset";

        public static BossData Load(int bossId) => AssetDatabase.LoadAssetAtPath<BossData>(PathFor(bossId));

        /// <summary>Ids of all existing BossData assets, sorted.</summary>
        public static List<int> FindIds()
        {
            var ids = new List<int>();
            if (!AssetDatabase.IsValidFolder(Dir))
                return ids;
            foreach (var guid in AssetDatabase.FindAssets("t:BossData", new[] { Dir }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<BossData>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !ids.Contains(asset.bossId))
                    ids.Add(asset.bossId);
            }
            ids.Sort();
            return ids;
        }

        public static string LabelFor(int bossId)
        {
            LocalizationTable.TryGet("ko", $"boss.{bossId}.name", out string name);
            return name != null ? $"{bossId} {name}" : bossId.ToString();
        }
    }

    /// <summary>
    /// Creates the boss data assets (9001 Sprout King, 9002 Sun Guardian, 9003 Frost Mammoth, 9004 Coral Hermit King, 9005 Ember Ironback) with their default tuning, the simple hazard
    /// prefabs (shockwave, acorn, sandstone chunk, shadow marker) and the capsule placeholder boss used until the real
    /// model is imported (Tools/MyGame/Art/Import Bosses). Existing assets are never overwritten.
    /// Menu: Tools/MyGame/Bosses/Create Default Boss Data And Prefabs.
    /// </summary>
    public static class BossSetup
    {
        public const string PrefabDir = "Assets/_Project/Prefabs/Bosses";
        private const string WavePath = PrefabDir + "/BossWave.prefab";
        private const string AcornPath = PrefabDir + "/BossFallingAcorn.prefab";
        private const string ChunkPath = PrefabDir + "/BossFallingChunk.prefab";
        private const string MarkerPath = PrefabDir + "/BossShadowMarker.prefab";
        private const string StunStarMaterialPath = "Assets/_Project/Art/Materials/M_BossStunStar.mat";

        [MenuItem("Tools/MyGame/Bosses/Create Default Boss Data And Prefabs")]
        public static void Menu() => Debug.Log(EnsureAssets());

        /// <summary>Creates whatever is missing. Safe to run repeatedly. Returns a short report.</summary>
        public static string EnsureAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating boss assets.");
            SceneFlowSetup.EnsureFolder(BossCatalog.Dir);
            SceneFlowSetup.EnsureFolder(PrefabDir);
            SceneFlowSetup.EnsureFolder(SceneFlowSetup.MaterialDir);

            var made = new List<string>();
            EnsureHazardPrefab(WavePath, made, MakeWave);
            EnsureHazardPrefab(AcornPath, made, MakeAcorn);
            EnsureHazardPrefab(ChunkPath, made, MakeChunk);
            EnsureHazardPrefab(MarkerPath, made, MakeMarker);

            EnsureData(9001, made, SetupSproutKing);
            EnsureData(9002, made, SetupSunGuardian);
            EnsureData(9003, made, SetupFrostMammoth);
            EnsureData(9004, made, SetupCoralHermitKing);
            EnsureData(9005, made, SetupEmberIronback);
            AssetDatabase.SaveAssets();
            return made.Count == 0 ? "[BossSetup] Boss data and hazard prefabs already exist." : "[BossSetup] Created: " + string.Join(", ", made);
        }

        private static void EnsureData(int id, List<string> made, Action<BossData> setup)
        {
            string path = BossCatalog.PathFor(id);
            if (AssetDatabase.LoadAssetAtPath<BossData>(path) != null)
                return;
            var data = ScriptableObject.CreateInstance<BossData>();
            data.bossId = id;
            data.nameKey = $"boss.{id}.name";
            data.stunStarMaterial = AssetDatabase.LoadAssetAtPath<Material>(StunStarMaterialPath);
            setup(data);
            AssetDatabase.CreateAsset(data, path);
            made.Add(path);
        }

        private static GameObject LoadPrefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        private static void SetupSproutKing(BossData d)
        {
            d.bodyWidth = 2.6f;      // the model is ~4 m wide with cape and arms; the lethal body is narrower
            d.bodyHeight = 3.5f;
            d.phases = new[]
            {
                new BossPhase { moveSpeed = 2.5f, walkSeconds = 1.5f, stunSeconds = 2f, attacks = new[] { BossAttackKind.Charge }, rainCount = 3 },
                new BossPhase { moveSpeed = 3.2f, walkSeconds = 1.2f, stunSeconds = 1.7f, attacks = new[] { BossAttackKind.Rain, BossAttackKind.Charge }, rainCount = 4 },
                new BossPhase { moveSpeed = 4f, walkSeconds = 0.9f, stunSeconds = 1.5f, attacks = new[] { BossAttackKind.Charge, BossAttackKind.Rain, BossAttackKind.Charge }, rainCount = 5 },
            };
            d.fallingPrefab = LoadPrefab(AcornPath);
            d.markerPrefab = LoadPrefab(MarkerPath);
            d.wavePrefab = LoadPrefab(WavePath);
        }

        private static void SetupSunGuardian(BossData d)
        {
            d.bodyWidth = 3f;
            d.bodyHeight = 4.1f;
            d.phases = new[]
            {
                new BossPhase { moveSpeed = 2.2f, walkSeconds = 1.5f, stunSeconds = 2f, attacks = new[] { BossAttackKind.Slam }, rainCount = 3 },
                new BossPhase { moveSpeed = 3f, walkSeconds = 1.2f, stunSeconds = 1.7f, attacks = new[] { BossAttackKind.Slam, BossAttackKind.Rain }, rainCount = 4 },
                new BossPhase { moveSpeed = 3.8f, walkSeconds = 0.9f, stunSeconds = 1.5f, attacks = new[] { BossAttackKind.Rain, BossAttackKind.Slam, BossAttackKind.Rain }, rainCount = 5 },
            };
            d.wavePrefab = LoadPrefab(WavePath);
            d.fallingPrefab = LoadPrefab(ChunkPath);
            d.markerPrefab = LoadPrefab(MarkerPath);
        }

        // ------------------------------------------------------------------ hazard prefabs

        private static void EnsureHazardPrefab(string path, List<string> made, Func<GameObject> build)
        {
            if (LoadPrefab(path) != null)
                return;
            SceneFlowSetup.NewScene(); // scratch scene for the prefab source
            SceneFlowSetup.SavePrefab(build(), path);
            made.Add(path);
        }

        private static GameObject HazardRoot(string name)
        {
            var root = new GameObject(name);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            return root;
        }

        private static GameObject MakeWave()
        {
            var root = HazardRoot("BossWave");
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(0.8f, 1f, 4f);
            root.AddComponent<BossHazard>();
            var mat = SceneFlowSetup.LitMaterial("M_BossWave", new Color(1f, 0.45f, 0.1f));
            SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Visual", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 2.4f), mat, false);
            return root;
        }

        private static GameObject MakeAcorn()
        {
            var root = HazardRoot("BossFallingAcorn");
            var sphere = root.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.45f;
            root.AddComponent<BossHazard>();
            var mat = SceneFlowSetup.LitMaterial("M_BossAcorn", new Color(0.55f, 0.32f, 0.12f));
            SceneFlowSetup.CreatePrimitive(PrimitiveType.Sphere, "Visual", root.transform, Vector3.zero, Vector3.one * 0.9f, mat, false);
            return root;
        }

        private static GameObject MakeChunk()
        {
            var root = HazardRoot("BossFallingChunk");
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * 1.1f;
            root.AddComponent<BossHazard>();
            var mat = SceneFlowSetup.LitMaterial("M_BossChunk", new Color(0.82f, 0.62f, 0.35f));
            var visual = SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Visual", root.transform, Vector3.zero, Vector3.one * 1.1f, mat, false);
            visual.transform.localRotation = Quaternion.Euler(20f, 35f, 15f);
            return root;
        }

        private static GameObject MakeMarker()
        {
            var root = new GameObject("BossShadowMarker");
            var mat = SceneFlowSetup.LitMaterial("M_BossMarker", new Color(0.08f, 0.05f, 0.08f));
            SceneFlowSetup.CreatePrimitive(PrimitiveType.Cylinder, "Disc", root.transform, Vector3.zero, new Vector3(2f, 0.01f, 2f), mat, false);
            return root;
        }

        private static void SetupFrostMammoth(BossData d)
        {
            // Charges like a battering ram, stomps out an ice-spike wave, and shakes icicles down from the citadel ceiling.
            d.bodyWidth = 3.2f;
            d.bodyHeight = 3.4f;
            d.chargeSpeed = 12f;
            d.phases = new[]
            {
                new BossPhase { moveSpeed = 2.4f, walkSeconds = 1.5f, stunSeconds = 2f, attacks = new[] { BossAttackKind.Charge }, rainCount = 3 },
                new BossPhase { moveSpeed = 3.1f, walkSeconds = 1.2f, stunSeconds = 1.7f, attacks = new[] { BossAttackKind.Slam, BossAttackKind.Rain }, rainCount = 4 },
                new BossPhase { moveSpeed = 3.9f, walkSeconds = 0.9f, stunSeconds = 1.5f, attacks = new[] { BossAttackKind.Charge, BossAttackKind.Rain, BossAttackKind.Slam }, rainCount = 5 },
            };
            d.fallingPrefab = LoadPrefab(ChunkPath);
            d.markerPrefab = LoadPrefab(MarkerPath);
            d.wavePrefab = LoadPrefab(WavePath);
        }

        private static void SetupCoralHermitKing(BossData d)
        {
            // Slides at the player shell-first, hammers a low foam wave with its big claw, and rattles coral chunks loose overhead.
            d.bodyWidth = 3.4f;
            d.bodyHeight = 3f;
            d.chargeSpeed = 11.5f;
            d.phases = new[]
            {
                new BossPhase { moveSpeed = 2.5f, walkSeconds = 1.5f, stunSeconds = 2f, attacks = new[] { BossAttackKind.Slam }, rainCount = 3 },
                new BossPhase { moveSpeed = 3.2f, walkSeconds = 1.2f, stunSeconds = 1.7f, attacks = new[] { BossAttackKind.Charge, BossAttackKind.Rain }, rainCount = 4 },
                new BossPhase { moveSpeed = 4f, walkSeconds = 0.9f, stunSeconds = 1.5f, attacks = new[] { BossAttackKind.Slam, BossAttackKind.Charge, BossAttackKind.Rain }, rainCount = 5 },
            };
            d.fallingPrefab = LoadPrefab(ChunkPath);
            d.markerPrefab = LoadPrefab(MarkerPath);
            d.wavePrefab = LoadPrefab(WavePath);
        }

        private static void SetupEmberIronback(BossData d)
        {
            // Final boss: a basalt-armoured lizard that rams, slams a magma shockwave, shakes burning chunks from the citadel and
            // (phases 2-3) charges up and drives its horn into the floor so lava columns erupt outward cell by cell.
            // Same rules as the other bosses, a notch faster (shorter walks, shorter stuns) since it closes the game.
            d.bodyWidth = 3.6f;
            d.slamImpactDelay = 0.6f; // dedicated Slam take: front feet hit the floor at 0.60 s
            d.bodyHeight = 3.5f;
            d.chargeSpeed = 12f;
            d.phases = new[]
            {
                new BossPhase { moveSpeed = 2.6f, walkSeconds = 1.4f, stunSeconds = 1.4f, attacks = new[] { BossAttackKind.Charge, BossAttackKind.Slam }, rainCount = 3 },
                new BossPhase { moveSpeed = 3.3f, walkSeconds = 1.1f, stunSeconds = 1.1f, attacks = new[] { BossAttackKind.Rain, BossAttackKind.Eruption, BossAttackKind.Charge }, rainCount = 4 },
                new BossPhase { moveSpeed = 4.1f, walkSeconds = 0.8f, stunSeconds = 0.9f, attacks = new[] { BossAttackKind.Eruption, BossAttackKind.Rain, BossAttackKind.Charge, BossAttackKind.Slam }, rainCount = 5 },
            };
            d.fallingPrefab = LoadPrefab(ChunkPath);
            d.markerPrefab = LoadPrefab(MarkerPath);
            d.wavePrefab = LoadPrefab(WavePath);
        }

        // ------------------------------------------------------------------ boss prefab

        /// <summary>The real prefab of the boss, or a generated capsule placeholder (created on demand).</summary>
        internal static GameObject PrefabFor(BossData data)
        {
            if (data.prefab != null)
                return data.prefab;
            string path = $"{PrefabDir}/BossPlaceholder_{data.bossId}.prefab";
            var existing = LoadPrefab(path);
            if (existing != null)
                return existing;
            SceneFlowSetup.EnsureFolder(PrefabDir);
            SceneFlowSetup.NewScene();
            var root = new GameObject($"BossPlaceholder_{data.bossId}");
            root.AddComponent<Rigidbody>();
            root.AddComponent<BoxCollider>();
            var controller = root.AddComponent<BossController>();

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var mat = SceneFlowSetup.LitMaterial("M_BossPlaceholder", new Color(0.55f, 0.35f, 0.3f));
            var body = SceneFlowSetup.CreatePrimitive(PrimitiveType.Capsule, "Body", visual,
                new Vector3(0f, data.bodyHeight * 0.5f, 0f), new Vector3(data.bodyWidth, data.bodyHeight * 0.5f, data.bodyWidth), mat, false);
            var eyeMat = SceneFlowSetup.LitMaterial("M_BossPlaceholderEye", Color.white);
            SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Eye", visual,
                new Vector3(0f, data.bodyHeight * 0.75f, data.bodyWidth * 0.5f), new Vector3(data.bodyWidth * 0.5f, 0.3f, 0.2f), eyeMat, false);
            var weak = new GameObject("WeakPoint").transform;
            weak.SetParent(root.transform, false);
            weak.localPosition = new Vector3(0f, data.bodyHeight, 0f);

            EditorWire.Set(controller, "visual", visual);
            EditorWire.Set(controller, "weakPoint", weak);
            EditorWire.Set(controller, "glowRenderers", new List<Renderer> { body.GetComponent<MeshRenderer>() });
            EditorWire.Set(controller, "tintBase", true);
            return SceneFlowSetup.SavePrefab(root, path);
        }
    }
}
