using System;
using System.IO;
using System.Linq;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Creates the monster assets the monster table points at: the Behavior graphs, the monster prefabs (visual,
    /// hit box, Monster + BehaviorGraphAgent) and a small AI test scene. Values are not set here; they come from
    /// the table (MonsterTableTool.Apply runs at the end).
    ///   Tools/MyGame/Monsters/Build Missing Monster Assets   - creates only what does not exist (keeps hand edits)
    ///   Tools/MyGame/Monsters/Rebuild All Monster Assets     - regenerates graphs, prefabs and the test scene
    ///
    /// Graphs (Start repeats):
    ///   PatrolEnemyAI (1001): Patrol Zone (never ends).
    ///   ChaserEnemyAI (1002): Patrol Until Target Spotted -> Chase While Visible -> Reset Chaser Pattern -> Wait.
    /// Uses Behavior authoring internals, which the package exposes to Assembly-CSharp-Editor (checked against
    /// com.unity.behavior 1.0.16).
    /// </summary>
    public static class MonsterSetup
    {
        public const string AiDir = "Assets/_Project/AI";
        public const string PatrolGraphPath = AiDir + "/PatrolEnemyAI.asset";
        public const string ChaserGraphPath = AiDir + "/ChaserEnemyAI.asset";
        public const string PatrolPrefabPath = StageLayoutBuilder.LevelPrefabDir + "/PatrolEnemy.prefab";
        public const string ChaserPrefabPath = StageLayoutBuilder.LevelPrefabDir + "/ChaserEnemy.prefab";
        public const string ProjectilePrefabPath = StageLayoutBuilder.LevelPrefabDir + "/MonsterProjectile.prefab";
        public const string TestScenePath = "Assets/_Project/Scenes/Tests/AI_ChaserTest.unity";

        private const float ResetWaitSeconds = 0.8f;

        private const string SproutBeanVisualPath = "Assets/_Project/Prefabs/Monsters/Stage01/SproutBean_Visual.prefab";
        private const float SproutBeanHeight = 0.8f;
        private static readonly Color SproutBeanColor = new Color(0.45f, 0.78f, 0.30f);
        public const string BerryBeetleVisualPath = "Assets/_Project/Prefabs/Monsters/Stage01/BerryBeetle_Visual.prefab";
        public const string BerryBeetleEyesName = "BerryBeetle_Eyes"; // separate EyeWhite mesh: the chase alert recolours it
        public const float BerryBeetleHeight = 0.7f;
        private static readonly Color PatrolColor = new Color(0.50f, 0.12f, 0.62f);
        private static readonly Color ChaserColor = new Color(0.78f, 0.16f, 0.28f);
        private static readonly Color AlertColor = new Color(1f, 0.85f, 0.1f);
        private static readonly Color ProjectileColor = new Color(1f, 0.32f, 0.05f);

        [MenuItem("Tools/MyGame/Monsters/Build Missing Monster Assets")]
        public static void BuildMissingMenu() => Debug.Log(Build(false));

        [MenuItem("Tools/MyGame/Monsters/Rebuild All Monster Assets")]
        public static void RebuildMenu() => Debug.Log(Build(true));

        /// <summary>Creates missing (or with force: all) monster assets, then applies the monster table. Returns the report.</summary>
        /// <summary>Rebuilds only the patroller prefab (keeps graphs, other prefabs and the test scene), then reapplies the table.</summary>
        [MenuItem("Tools/MyGame/Monsters/Rebuild Patrol Prefab")]
        public static void RebuildPatrolMenu() => Debug.Log(RebuildPatrol());

        public static string RebuildPatrol()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play Mode and wait for compilation first.");
            SceneFlowSetup.SaveDirtyScenesQuietly();
            string previousScene = SceneManager.GetActiveScene().path;
            BuildPatrolPrefab();
            AssetDatabase.SaveAssets();
            string report = "[MonsterSetup] prefab: " + PatrolPrefabPath + "\n" + MonsterTableTool.Apply();
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            return report;
        }

        public static string Build(bool force)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building monster assets.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Scripts are compiling. Try again after compilation.");

            SceneFlowSetup.SaveDirtyScenesQuietly();
            string previousScene = SceneManager.GetActiveScene().path;

            SceneFlowSetup.EnsureFolder(AiDir);
            SceneFlowSetup.EnsureFolder(StageLayoutBuilder.LevelPrefabDir);
            SceneFlowSetup.EnsureFolder(SceneFlowSetup.MaterialDir);
            SceneFlowSetup.EnsureFolder(Path.GetDirectoryName(TestScenePath).Replace('\\', '/'));

            var report = new System.Text.StringBuilder("[MonsterSetup]\n");
            if (force || !Exists(PatrolGraphPath))
            {
                BuildPatrolGraph();
                report.AppendLine("  graph: " + PatrolGraphPath);
            }
            if (force || !Exists(ChaserGraphPath))
            {
                BuildChaserGraph();
                report.AppendLine("  graph: " + ChaserGraphPath);
            }
            if (force || AssetDatabase.LoadAssetAtPath<MonsterProjectile>(ProjectilePrefabPath) == null)
            {
                BuildProjectilePrefab();
                report.AppendLine("  prefab: " + ProjectilePrefabPath);
            }
            if (force || !HasMonster(PatrolPrefabPath))
            {
                BuildPatrolPrefab();
                report.AppendLine("  prefab: " + PatrolPrefabPath);
            }
            if (force || !HasMonster(ChaserPrefabPath))
            {
                BuildChaserPrefab();
                report.AppendLine("  prefab: " + ChaserPrefabPath);
            }
            AssetDatabase.SaveAssets();

            report.AppendLine(MonsterTableTool.Apply());

            if (force || !Exists(TestScenePath))
            {
                BuildTestScene();
                report.AppendLine("  test scene: " + TestScenePath);
            }
            AssetDatabase.SaveAssets();

            if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            return report.ToString();
        }

        private static bool Exists(string path) => AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null;

        /// <summary>True if the prefab exists and is already a Behavior monster (the old PatrolEnemy prefab is not).</summary>
        private static bool HasMonster(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null && prefab.GetComponent<Monster>() != null;
        }

        /// <summary>Runtime graph of a graph asset (what BehaviorGraphAgent references), or null.</summary>
        internal static BehaviorGraph LoadRuntimeGraph(string graphPath)
        {
            var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(graphPath);
            return graph != null ? graph.RuntimeGraph : null;
        }

        // ------------------------------------------------------------------ Behavior graphs

        private sealed class GraphContext
        {
            public BehaviorAuthoringGraph Graph;
            public SequenceNodeModel Sequence;
            public VariableModel Self;
            public VariableModel Target;
            private int count;

            public BehaviorGraphNodeModel Add<T>() where T : Unity.Behavior.Action
            {
                NodeInfo info = Unity.Behavior.NodeRegistry.GetInfo(typeof(T))
                                ?? throw new InvalidOperationException($"Behavior node {typeof(T).Name} is not registered (compile errors?).");
                var node = (BehaviorGraphNodeModel)Graph.CreateNode(info.ModelType, new Vector2(0f, 180f + 60f * count), null, new object[] { info });
                Graph.AddNodeToSequence(node, Sequence, count++);
                return node;
            }
        }

        private static void BuildPatrolGraph()
        {
            BuildGraph(PatrolGraphPath, "Blind patroller: walks back and forth inside its zone forever.", ctx =>
            {
                ctx.Add<PatrolZoneAction>().SetField("Self", ctx.Self, typeof(GameObject));
            });
        }

        private static void BuildChaserGraph()
        {
            BuildGraph(ChaserGraphPath, "Wander/chase monster: patrol until the target is spotted, chase while it is visible, " +
                                        "reset the pattern when the sight line is blocked, then patrol again.", ctx =>
            {
                var patrol = ctx.Add<PatrolUntilTargetSpottedAction>();
                patrol.SetField("Self", ctx.Self, typeof(GameObject));
                patrol.SetField("Target", ctx.Target, typeof(GameObject));

                var chase = ctx.Add<ChaseWhileVisibleAction>();
                chase.SetField("Self", ctx.Self, typeof(GameObject));
                chase.SetField("Target", ctx.Target, typeof(GameObject));

                ctx.Add<ResetChaserPatternAction>().SetField("Self", ctx.Self, typeof(GameObject));
                ctx.Add<WaitAction>().SetField("SecondsToWait", ResetWaitSeconds);
            });
        }

        /// <summary>
        /// (Re)creates a graph asset: Start (repeat) -> one sequence filled by <paramref name="populate"/>.
        /// Blackboard: Self (graph owner) + Target (GameObject, pushed in by Monster at runtime).
        /// </summary>
        private static void BuildGraph(string path, string description, System.Action<GraphContext> populate)
        {
            if (Exists(path))
                AssetDatabase.DeleteAsset(path);

            var created = ScriptableObject.CreateInstance<BehaviorAuthoringGraph>();
            created.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var graph = AssetDatabase.LoadAssetAtPath<BehaviorAuthoringGraph>(path);
            if (graph == null)
                throw new InvalidOperationException("Behavior graph asset could not be created at " + path);
            graph.ValidateAsset(); // blackboard, runtime graph sub-asset and the Start node
            graph.Description = description;

            var blackboard = graph.Blackboard;
            GraphAssetProcessor.EnsureBlackboardGraphOwnerVariable(blackboard);
            var ctx = new GraphContext
            {
                Graph = graph,
                Self = blackboard.Variables.First(v => v.ID == BehaviorGraph.k_GraphSelfOwnerID),
                Target = new TypedVariableModel<GameObject> { ID = SerializableGUID.Generate(), Name = Monster.TargetVariable },
            };
            blackboard.Variables.Add(ctx.Target);
            blackboard.SetAssetDirty();

            var start = graph.Roots.OfType<StartNodeModel>().FirstOrDefault()
                        ?? throw new InvalidOperationException("Behavior graph has no Start node.");
            start.Repeat = true;
            start.Position = Vector2.zero;
            if (!start.TryDefaultOutputPortModel(out PortModel startOut))
                throw new InvalidOperationException("Start node has no output port.");
            ctx.Sequence = (SequenceNodeModel)graph.CreateNode(typeof(SequenceNodeModel), new Vector2(0f, 120f), startOut);

            populate(ctx);

            if (graph.BuildRuntimeGraph(true) == null)
                throw new InvalidOperationException($"Behavior runtime graph build failed for {path} (see Console).");
            graph.SaveAsset();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ prefabs

        private static void BuildPatrolPrefab()
        {
            SceneFlowSetup.NewScene(); // scratch scene for the prefab source
            var bodyMat = SceneFlowSetup.LitMaterial("M_Enemy", PatrolColor);
            var eyeMat = SceneFlowSetup.LitMaterial("M_EnemyEye", Color.white);

            var root = NewMonsterRoot("PatrolEnemy", out var visual);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SproutBeanVisualPath);
            if (model == null)
            {
                // Placeholder until the Stage01 monster art is imported.
                SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Body", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 1f, 0.9f), bodyMat, false);
                SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "EyeL", visual, new Vector3(-0.2f, 0.65f, 0.46f), new Vector3(0.2f, 0.2f, 0.08f), eyeMat, false);
                SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "EyeR", visual, new Vector3(0.2f, 0.65f, 0.46f), new Vector3(0.2f, 0.2f, 0.08f), eyeMat, false);
            }

            var comp = FinishMonster(root, visual, model != null ? SproutBeanColor : PatrolColor, PatrolGraphPath);
            EditorWire.Set(comp, "turnPause", 0f); // keeps the original non-stop ping-pong of the stage patroller
            if (model != null)
                AttachAnimatedModel(root, visual, comp, model, SproutBeanHeight);
            SceneFlowSetup.SavePrefab(root, PatrolPrefabPath);
        }

        /// <summary>
        /// Puts an animated Stage01 visual prefab (Animator: Speed / Hit / Defeat, faces +Z, pivot at the feet) under
        /// the Visual root, fits the hit box to the body height and lets the clips replace the scale squash.
        /// </summary>
        internal static void AttachAnimatedModel(GameObject root, Transform visual, Monster comp, GameObject model, float height)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, visual);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            var animator = instance.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            var box = root.GetComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, 0f);
            box.size = new Vector3(0.9f, height, 0.9f);
            EditorWire.Set(comp, "topHeight", height);
            EditorWire.Set(comp, "eyeHeight", height * 0.65f);
            EditorWire.Set(comp, "animator", animator);
            EditorWire.Set(comp, "faceCameraAngle", 30f);
            EditorWire.Set(comp, "defeatHideDelay", 1f);
            visual.localRotation = Quaternion.Euler(0f, 120f, 0f);
        }

        private static void BuildChaserPrefab()
        {
            SceneFlowSetup.NewScene();
            var bodyMat = SceneFlowSetup.LitMaterial("M_ChaserEnemy", ChaserColor);
            var eyeMat = SceneFlowSetup.LitMaterial("M_EnemyEye", Color.white);
            var alertMat = SceneFlowSetup.LitMaterial("M_ChaserEnemyAlert", AlertColor);
            alertMat.EnableKeyword("_EMISSION");
            alertMat.SetColor("_EmissionColor", AlertColor * 0.8f);
            EditorUtility.SetDirty(alertMat);

            var root = NewMonsterRoot("ChaserEnemy", out var visual);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(BerryBeetleVisualPath);
            Renderer[] alert;
            if (model == null)
            {
                // Placeholder until the BerryBeetle art is imported.
                SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Body", visual, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 1f, 0.9f), bodyMat, false);
                SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Horn", visual, new Vector3(0f, 1.08f, 0.1f), new Vector3(0.25f, 0.18f, 0.25f), bodyMat, false);
                var eyeL = SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "EyeL", visual, new Vector3(-0.2f, 0.65f, 0.46f), new Vector3(0.2f, 0.12f, 0.08f), eyeMat, false);
                var eyeR = SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "EyeR", visual, new Vector3(0.2f, 0.65f, 0.46f), new Vector3(0.2f, 0.12f, 0.08f), eyeMat, false);
                alert = new Renderer[] { eyeL.GetComponent<MeshRenderer>(), eyeR.GetComponent<MeshRenderer>() };
            }
            else
                alert = null;

            var comp = FinishMonster(root, visual, ChaserColor, ChaserGraphPath);
            if (model != null)
            {
                AttachAnimatedModel(root, visual, comp, model, BerryBeetleHeight);
                alert = visual.GetComponentsInChildren<Renderer>(true).Where(r => r.name == BerryBeetleEyesName).ToArray();
            }
            EditorWire.Set(comp, "alertMaterial", alertMat);
            var so = new SerializedObject(comp);
            var renderers = so.FindProperty("alertRenderers");
            renderers.arraySize = alert.Length;
            for (int i = 0; i < alert.Length; i++)
                renderers.GetArrayElementAtIndex(i).objectReferenceValue = alert[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            SceneFlowSetup.SavePrefab(root, ChaserPrefabPath);
        }

        /// <summary>
        /// Gameplay prefabs of the two extra Stage01 monsters from their existing visual prefabs: 1007 Spike Chestnut
        /// (cannot be stomped) and 1006 Wing Bean (low, slow flyer). Both run the patrol graph; the table row supplies the
        /// stats and traits. Rebuilds the prefabs, then applies the monster table (icons included).
        /// </summary>
        [MenuItem("Tools/MyGame/Monsters/Build Stage01 Extra Monsters")]
        public static void BuildStage01ExtrasMenu() => Debug.Log(BuildStage01Extras());

        public static string BuildStage01Extras()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Stop Play Mode and wait for compilation first.");
            SceneFlowSetup.SaveDirtyScenesQuietly();
            string previousScene = SceneManager.GetActiveScene().path;
            var report = new System.Text.StringBuilder("[MonsterSetup] Stage01 extras");
            report.AppendLine(BuildStage01Extra("SpikeChestnut", new Color(0.55f, 0.36f, 0.18f), null));
            report.AppendLine(BuildStage01Extra("WingBean", SproutBeanColor, comp =>
            {
                // Gentler than the later bats: this is the first flyer the player meets.
                EditorWire.Set(comp, "hoverHeight", 1.6f);
                EditorWire.Set(comp, "diveAnticipateSeconds", 0.8f);
                EditorWire.Set(comp, "diveSpeed", 8f);
                EditorWire.Set(comp, "diveCooldown", 3f);
            }));
            AssetDatabase.SaveAssets();
            report.AppendLine(MonsterTableTool.Apply());
            if (!string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            return report.ToString();
        }

        private static string BuildStage01Extra(string name, Color burst, Action<Monster> tune)
        {
            string visualPath = $"Assets/_Project/Prefabs/Monsters/Stage01/{name}_Visual.prefab";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(visualPath)
                        ?? throw new InvalidOperationException("visual prefab not found: " + visualPath);
            SceneFlowSetup.NewScene(); // scratch scene for the prefab source
            var root = NewMonsterRoot(name + "Enemy", out var visual);
            var comp = FinishMonster(root, visual, burst, PatrolGraphPath);
            float height = 0.8f;
            var probe = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                var renderers = probe.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    height = Mathf.Clamp(bounds.max.y, 0.4f, 1.4f);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            AttachAnimatedModel(root, visual, comp, model, height);
            tune?.Invoke(comp);
            string path = $"{StageLayoutBuilder.LevelPrefabDir}/{name}Enemy.prefab";
            SceneFlowSetup.SavePrefab(root, path);
            return $"  prefab: {path} (height {height:0.00})";
        }

        /// <summary>Glowing orange ball (0.4 m) with a trigger sphere; assigned to monsters through Monster.projectilePrefab.</summary>
        private static void BuildProjectilePrefab()
        {
            SceneFlowSetup.NewScene();
            var mat = SceneFlowSetup.LitMaterial("M_MonsterProjectile", ProjectileColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", ProjectileColor * 1.5f);
            EditorUtility.SetDirty(mat);

            var root = new GameObject("MonsterProjectile");
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var sphere = root.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.2f;
            var ball = SceneFlowSetup.CreatePrimitive(PrimitiveType.Sphere, "Visual", root.transform, Vector3.zero, Vector3.one * 0.4f, mat, false);
            ball.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.AddComponent<MonsterProjectile>();
            SceneFlowSetup.SavePrefab(root, ProjectilePrefabPath);
        }

        /// <summary>Kinematic body + trigger hit box (0.9 x 1 x 0.9, pivot at the feet) + the Visual root that turns with the facing.</summary>
        internal static GameObject NewMonsterRoot(string name, out Transform visual)
        {
            var root = new GameObject(name);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.5f, 0f);
            box.size = new Vector3(0.9f, 1f, 0.9f);

            visual = new GameObject("Visual").transform; // squashed on stomp; yaw flips with the facing direction
            visual.SetParent(root.transform, false);
            visual.localRotation = Quaternion.Euler(0f, 90f, 0f);
            return root;
        }

        internal static Monster FinishMonster(GameObject root, Transform visual, Color burstColor, string graphPath)
        {
            var runtimeGraph = LoadRuntimeGraph(graphPath)
                               ?? throw new InvalidOperationException("Behavior graph missing or not built: " + graphPath);
            var agent = root.AddComponent<BehaviorGraphAgent>();
            EditorWire.Set(agent, "m_Graph", runtimeGraph);

            var comp = root.AddComponent<Monster>();
            EditorWire.Set(comp, "visual", visual);
            EditorWire.Set(comp, "defeatBurst", MakeDefeatBurst(root.transform, burstColor));
            return comp;
        }

        /// <summary>Placeholder defeat effect: a one-shot burst of cube shards (sibling of the visual).</summary>
        private static ParticleSystem MakeDefeatBurst(Transform parent, Color color)
        {
            var go = new GameObject("DefeatBurst");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.white);
            main.gravityModifier = 1.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 20) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.35f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // hemisphere opens upward

            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var spin = ps.rotationOverLifetime;
            spin.enabled = true;
            spin.separateAxes = true;
            spin.x = new ParticleSystem.MinMaxCurve(-8f, 8f);
            spin.y = new ParticleSystem.MinMaxCurve(-8f, 8f);
            spin.z = new ParticleSystem.MinMaxCurve(-8f, 8f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            renderer.sharedMaterial = ParticleMat("M_EnemyBurst");
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        private static Material ParticleMat(string name)
        {
            string path = $"{SceneFlowSetup.MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null)
                    throw new InvalidOperationException("URP Particles/Unlit shader not found.");
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", Color.white); // particle colours come from the system's start colour
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ test scene

        /// <summary>
        /// Flat floor, a pillar that blocks the sight line, one chaser and a stand-in target (no PlayerMotor, so
        /// contact does nothing). Move "TestTarget" in Play mode to watch Patrol -> Chase -> Reset -> Patrol.
        /// </summary>
        private static void BuildTestScene()
        {
            var scene = SceneFlowSetup.NewScene();
            // Re-resolved from disk: NewScene(Single) unloads assets loaded earlier in the run.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChaserPrefabPath);
            SceneFlowSetup.ConfigureAmbient();
            SceneFlowSetup.CreateDirectionalLight();
            SceneFlowSetup.CreateCamera("Main Camera", new Color(0.55f, 0.75f, 0.95f), new Vector3(8f, 3f, -18f), new Vector3(5f, 0f, 0f));

            var ground = SceneFlowSetup.LitMaterial("M_Ground", new Color(0.35f, 0.62f, 0.30f));
            var wall = SceneFlowSetup.LitMaterial("M_Wall", new Color(0.50f, 0.52f, 0.58f));
            var level = new GameObject("Level").transform;
            SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Floor", level, new Vector3(8f, -0.5f, 0f), new Vector3(30f, 1f, 4f), ground, true);
            SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "SightBlocker", level, new Vector3(11f, 1.5f, 0f), new Vector3(1f, 3f, 4f), wall, true);

            var monster = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            monster.name = "ChaserEnemy";
            monster.transform.position = new Vector3(2f, 0f, 0f);

            var targetMat = SceneFlowSetup.LitMaterial("M_TestTarget", new Color(0.2f, 0.45f, 0.95f));
            var target = SceneFlowSetup.CreatePrimitive(PrimitiveType.Capsule, "TestTarget", null, new Vector3(8f, 1f, 0f), Vector3.one, targetMat, true);
            var comp = monster.GetComponent<Monster>();
            EditorWire.Set(comp, "targetOverride", target);
            EditorWire.Set(comp, "logModeChanges", true);

            SceneFlowSetup.SaveScene(scene, TestScenePath);
        }
    }
}
