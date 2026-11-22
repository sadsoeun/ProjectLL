using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Builds stage scenes from the JSON layouts in Assets/_Project/Data/StageLayouts/ (contract:
    /// Docs/stage-layout-schema.md). Menu: Tools/MyGame/Build Stages From Layouts.
    /// Code entry points: StageLayoutBuilder.BuildAll(), StageLayoutBuilder.BuildFromFile(path).
    /// Scenes are overwritten on every build; validation errors abort before anything is touched.
    /// </summary>
    public static partial class StageLayoutBuilder
    {
        public const string LayoutsDir = "Assets/_Project/Data/StageLayouts";
        public const string LevelPrefabDir = "Assets/_Project/Prefabs/Level";
        private const string MeshDir = "Assets/_Project/Art/Meshes";
        private const string SpikeMeshPath = MeshDir + "/SpikeUnit.asset";

        private const float BlockDepth = 4f;
        private const float PlayerSpawnHeight = 1.2f;   // capsule centre above the surface (height 2 + margin)
        private const float CameraEndMargin = 12f;      // camera centre stops this far before the end of the course
        private const float SpikeUnitWidth = 0.5f;
        private const float SpikeDepth = 1.5f;

        private sealed class Loaded
        {
            public string AssetPath;
            public string Raw;
            public StageLayoutData Data;
            public string SceneName;
        }

        // ------------------------------------------------------------------ entry points

        [MenuItem("Tools/MyGame/Build Stages From Layouts")]
        public static void BuildAllMenu()
        {
            Debug.Log(BuildAll());
        }

        /// <summary>True if at least one layout JSON exists.</summary>
        public static bool HasLayouts() => FindLayoutFiles().Count > 0;

        /// <summary>
        /// Validates and builds every layout in LayoutsDir and registers the stage scenes in Build Settings
        /// (after 06_InGame). Returns the report; throws InvalidOperationException on validation errors.
        /// </summary>
        public static string BuildAll()
        {
            var files = FindLayoutFiles();
            if (files.Count == 0)
                throw new InvalidOperationException($"No layout files (*.json) in {LayoutsDir}.");

            var loaded = new List<Loaded>();
            var errors = new List<string>();
            var warnings = new List<string>();
            foreach (var path in files)
                loaded.Add(LoadAndValidate(path, Path.GetFileNameWithoutExtension(path), null, errors, warnings));
            CheckDuplicateStageIds(loaded, errors);
            ThrowIfErrors(errors, warnings);

            return Build(loaded, warnings, registerBuildSettings: true, reopenPreviousScene: true);
        }

        /// <summary>
        /// Builds a single layout file (any project-relative path), e.g. the self-test layout under
        /// Assets/_Project/Editor/TestData/. Output: Stage_&lt;stageId&gt;.unity. Build Settings are not touched.
        /// </summary>
        public static string BuildFromFile(string path) => BuildFromFile(path, null);

        /// <summary>Like BuildFromFile but writes the scene under the given scene name (e.g. "Stage_W1_S1" to play it in-game).</summary>
        public static string BuildFromFile(string path, string sceneNameOverride)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var l = LoadAndValidate(path, null, sceneNameOverride, errors, warnings);
            ThrowIfErrors(errors, warnings);
            return Build(new List<Loaded> { l }, warnings, registerBuildSettings: false, reopenPreviousScene: true);
        }

        // ------------------------------------------------------------------ loading / validation

        private static List<string> FindLayoutFiles()
        {
            var result = new List<string>();
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", LayoutsDir));
            if (!Directory.Exists(abs))
                return result;
            foreach (var f in Directory.GetFiles(abs, "*.json"))
                result.Add(LayoutsDir + "/" + Path.GetFileName(f));
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        private static Loaded LoadAndValidate(string assetPath, string expectedStageId, string sceneNameOverride,
            List<string> errors, List<string> warnings)
        {
            string abs = Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath));
            if (!File.Exists(abs))
                throw new FileNotFoundException("Layout file not found: " + assetPath);
            string raw = File.ReadAllText(abs);
            StageLayoutData data = null;
            try
            {
                data = JsonUtility.FromJson<StageLayoutData>(raw);
            }
            catch (Exception e)
            {
                errors.Add($"{assetPath}: JSON parse error: {e.Message}");
            }
            if (data == null)
            {
                errors.Add($"{assetPath}: JSON could not be parsed.");
                return new Loaded { AssetPath = assetPath, Raw = raw };
            }
            StageLayoutValidator.Validate(Path.GetFileName(assetPath), raw, data, expectedStageId, errors, warnings);
            return new Loaded
            {
                AssetPath = assetPath,
                Raw = raw,
                Data = data,
                SceneName = sceneNameOverride ?? ("Stage_" + data.stageId),
            };
        }

        private static void CheckDuplicateStageIds(List<Loaded> loaded, List<string> errors)
        {
            var seen = new HashSet<string>();
            foreach (var l in loaded)
            {
                if (l.Data != null && !string.IsNullOrEmpty(l.Data.stageId) && !seen.Add(l.Data.stageId))
                    errors.Add($"duplicate stageId '{l.Data.stageId}' ({l.AssetPath}).");
            }
        }

        private static void ThrowIfErrors(List<string> errors, List<string> warnings)
        {
            foreach (var w in warnings)
                Debug.LogWarning("[StageLayoutBuilder] " + w);
            if (errors.Count == 0)
                return;
            var sb = new StringBuilder($"Stage layout validation failed ({errors.Count} error(s)); nothing was built:\n");
            foreach (var e in errors)
                sb.AppendLine("  ERROR " + e);
            throw new InvalidOperationException(sb.ToString());
        }

        // ------------------------------------------------------------------ build driver

        private static string Build(List<Loaded> layouts, List<string> warnings, bool registerBuildSettings, bool reopenPreviousScene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building stages.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Scripts are compiling. Try again after compilation.");

            SceneFlowSetup.SaveDirtyScenesQuietly();
            string previousScene = SceneManager.GetActiveScene().path;

            SceneFlowSetup.EnsureFolder(SceneFlowSetup.StageScenesDir);
            SceneFlowSetup.EnsureFolder(LevelPrefabDir);
            SceneFlowSetup.EnsureFolder(MeshDir);
            SceneFlowSetup.EnsureFolder(SceneFlowSetup.MaterialDir);

            EnsureSharedAssets();
            foreach (var l in layouts)
                PrepareBoss(l.Data); // boss prefabs are created here: making them later would replace the scene being built
            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            var scenePaths = new List<string>();
            foreach (var l in layouts)
            {
                string scenePath = $"{SceneFlowSetup.StageScenesDir}/{l.SceneName}.unity";
                var counts = BuildScene(l.Data, scenePath);
                scenePaths.Add(scenePath);
                report.AppendLine($"  {l.Data.stageId} -> {scenePath}  ({counts})");
            }

            if (registerBuildSettings)
                RegisterStageScenes(scenePaths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (reopenPreviousScene && !string.IsNullOrEmpty(previousScene) && File.Exists(previousScene))
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

            var head = new StringBuilder($"[StageLayoutBuilder] Built {layouts.Count} stage scene(s), {warnings.Count} warning(s):\n");
            head.Append(report);
            foreach (var w in warnings)
                head.AppendLine("  WARN " + w);
            return head.ToString();
        }

        private static void RegisterStageScenes(List<string> builtScenePaths)
        {
            var list = EditorBuildSettings.scenes.ToList();
            list.RemoveAll(s => builtScenePaths.Contains(s.path));
            int inGame = list.FindIndex(s => s.path.EndsWith("/06_InGame.unity", StringComparison.Ordinal));
            int insertAt = inGame >= 0 ? inGame + 1 : list.Count;
            foreach (var p in builtScenePaths)
            {
                list.Insert(insertAt++, new EditorBuildSettingsScene(p, true));
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ scene building

        private sealed class SceneContext
        {
            public Material Ground, Platform, Wall;
            public GameObject Coin, Special, Checkpoint, Spikes, Mover, Faller, Goal, SpikeTrap, FloorSwitch, Gate;
            public MonsterTable Monsters;
            public StageThemeKit Theme;   // null = primitive visuals only (layout without "theme")
            public PropTable Props;       // loaded only when the layout has decorations
            public Material SpikeMat;
            public Mesh SpikeMesh;
            public readonly Dictionary<string, Transform> Sections = new Dictionary<string, Transform>();
            public LayoutSection[] SectionList;
            public Transform Unsectioned;
        }

        private static string BuildScene(StageLayoutData d, string scenePath)
        {
            var scene = SceneFlowSetup.NewScene(); // Single mode: asset references from before this call may be destroyed
            SceneFlowSetup.ConfigureAmbient();

            var ctx = LoadContext(d);

            var lighting = new GameObject("Lighting").transform;
            var sun = SceneFlowSetup.CreateDirectionalLight(lighting);
            ApplyAtmosphere(ctx.Theme, lighting, sun, d.length);
            ApplyScreenWeather(ctx.Theme, lighting);

            foreach (var s in ctx.SectionList)
                ctx.Sections[s.id] = new GameObject(s.id).transform;

            // Solid geometry
            foreach (var b in Arr(d.blocks))
            {
                var mat = b.kind == "ground" ? ctx.Ground : b.kind == "wall" ? ctx.Wall : ctx.Platform;
                var go = SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, b.id, SectionFor(ctx, b.section, b.id, b.x, b.x + b.w),
                    new Vector3(b.x + b.w * 0.5f, b.y - b.h * 0.5f, 0f), new Vector3(b.w, b.h, BlockDepth), mat, true);
                go.isStatic = b.surface != "crumble"; // a crumbling block shakes and hides its visuals
                if (b.surface == "ice")
                    go.AddComponent<IceSurface>(); // slippery footing for the player (schema v1.5)
                ApplyBlockArt(ctx, go, b, d.blocks);
                ApplySurfaceGimmick(go, b, ctx.Theme); // bounce / crumble / conveyor (schema v1.6), after the art so it can wrap it
            }
            bool apron = BuildStartApron(ctx, Arr(d.blocks));
            BuildBackFill(ctx, Arr(d.blocks), apron);
            BuildPlatformSupports(ctx, Arr(d.blocks), Arr(d.decorations));
            BuildAutoDressing(ctx, d, apron);

            // Gimmicks
            int coinNo = 0;
            foreach (var c in Arr(d.coins))
            {
                var go = Spawn(ctx.Coin, $"Coin_{++coinNo:000}", SectionFor(ctx, c.section, null, c.x, c.x), new Vector3(c.x, c.y, 0f));
                ApplySpinArt(go, "Disc", ctx.Theme != null ? ctx.Theme.coinVisual : null);
            }
            foreach (var s in Arr(d.specials))
            {
                var go = Spawn(ctx.Special, s.id, SectionFor(ctx, s.section, s.id, s.x, s.x), new Vector3(s.x, s.y, 0f));
                EditorWire.Set(go.GetComponent<SpecialCollectible>(), "slot", s.slot);
                // The prefab's golden leaf is the special collectible's art in every world (theme gemVisual no longer applied).
            }
            foreach (var c in Arr(d.checkpoints))
            {
                var go = Spawn(ctx.Checkpoint, c.id, SectionFor(ctx, c.section, c.id, c.x, c.x), new Vector3(c.x, c.y, 0f));
                EditorWire.Set(go.GetComponent<Checkpoint>(), "id", c.id);
                ApplyCheckpointArt(ctx, go);
            }
            foreach (var s in Arr(d.spikes))
                BuildSpikes(ctx, s);
            foreach (var t in Arr(d.spikeTraps))
                BuildSpikeTrap(ctx, t);
            foreach (var sw in Arr(d.switches))
                BuildSwitch(ctx, sw);
            foreach (var g in Arr(d.gates))
                BuildGate(ctx, g);
            foreach (var m in Arr(d.movers))
            {
                var go = Spawn(ctx.Mover, m.id, SectionFor(ctx, m.section, m.id, m.x, m.x + m.w), new Vector3(m.x + m.w * 0.5f, m.y - 0.25f, 0f));
                go.transform.localScale = new Vector3(m.w, 0.5f, BlockDepth);
                var comp = go.GetComponent<Mover>();
                EditorWire.Set(comp, "axis", m.axis == "y" ? 1 : 0);
                EditorWire.Set(comp, "distance", m.distance);
                EditorWire.Set(comp, "speed", m.speed);
                EditorWire.Set(comp, "wait", m.wait);
                ApplyMoverArt(ctx, go, m.w);
            }
            foreach (var e in Arr(d.enemies))
            {
                var go = Spawn(MonsterPrefab(ctx, e.ResolvedMonsterId), e.id, SectionFor(ctx, e.section, e.id, e.x, e.x + e.range), new Vector3(e.x, e.y, 0f));
                var comp = go.GetComponent<Monster>();
                EditorWire.Set(comp, "patrolRange", e.range); // per-placement override of the table value
                if (e.speed > 0f)
                    EditorWire.Set(comp, "moveSpeed", e.speed);
            }
            foreach (var f in Arr(d.fallers))
            {
                var go = Spawn(ctx.Faller, f.id, SectionFor(ctx, f.section, f.id, f.x, f.x), new Vector3(f.x, f.y, 0f));
                var comp = go.GetComponent<Faller>();
                EditorWire.Set(comp, "triggerHalfWidth", f.triggerHalfWidth);
                EditorWire.Set(comp, "delay", f.delay);
                EditorWire.Set(comp, "respawn", f.respawn);
                ApplyFallerArt(ctx, go);
            }

            // Stage entry: spawn, bounds, goal
            var spawn = new GameObject("PlayerSpawnPoint");
            spawn.transform.position = new Vector3(d.spawn.x, d.spawn.y + PlayerSpawnHeight, 0f);

            var boundsGo = new GameObject("StageBounds");
            var root = boundsGo.AddComponent<StageRoot>();
            EditorWire.Set(root, "playerSpawnPoint", spawn.transform);
            EditorWire.Set(root, "cameraMinX", 0f);
            EditorWire.Set(root, "cameraMaxX", Mathf.Max(0f, d.length - CameraEndMargin));
            EditorWire.Set(root, "cameraMinY", d.cameraMinY);
            EditorWire.Set(root, "levelMinX", apron ? -StartApronLength : 0f);
            EditorWire.Set(root, "levelMaxX", d.length);
            EditorWire.Set(root, "killY", d.killY);

            var goalGo = Spawn(ctx.Goal, "Goal", SectionFor(ctx, null, null, d.goal.x, d.goal.x), new Vector3(d.goal.x, d.goal.y + 1.5f, 0f));
            EditorWire.Set(goalGo.GetComponent<GoalTrigger>(), "stage", root);
            ApplyGoalArt(ctx, goalGo);
            if (d.HasBoss)
            {
                goalGo.SetActive(false); // a boss stage clears when the boss is defeated, not at a goal trigger
                BuildBoss(ctx, d);
            }

            int decorationCount = BuildDecorations(ctx, d);

            // Invisible end walls so the player cannot leave the course sideways.
            var walls = new GameObject("Boundaries").transform;
            CreateWall("Boundary_Left", walls, -0.5f);
            CreateWall("Boundary_Right", walls, d.length + 0.5f);

            SceneFlowSetup.SaveScene(scene, scenePath);

            string counts = $"blocks {Arr(d.blocks).Length}, coins {Arr(d.coins).Length}, specials {Arr(d.specials).Length}, checkpoints {Arr(d.checkpoints).Length}, " +
                            $"spikes {Arr(d.spikes).Length}, movers {Arr(d.movers).Length}, enemies {Arr(d.enemies).Length}, fallers {Arr(d.fallers).Length}";
            counts += $", spikeTraps {Arr(d.spikeTraps).Length}, switches {Arr(d.switches).Length}, gates {Arr(d.gates).Length}, boss {(d.HasBoss ? d.boss.bossId : 0)}";
            if (ctx.Theme != null)
                counts += $", theme {d.theme}";
            if (Arr(d.decorations).Length > 0)
                counts += $", decorations {decorationCount}/{d.decorations.Length}";
            return counts;
        }

        private static T[] Arr<T>(T[] a) => a ?? Array.Empty<T>();

        private static SceneContext LoadContext(StageLayoutData d)
        {
            // Everything is re-resolved from disk: NewScene(Single) unloads assets created earlier in the run.
            var ctx = new SceneContext
            {
                Ground = SceneFlowSetup.LitMaterial("M_Ground", Palette.Ground),
                Platform = SceneFlowSetup.LitMaterial("M_Platform", Palette.Platform),
                Wall = SceneFlowSetup.LitMaterial("M_Wall", Palette.Wall),
                SpikeMat = SceneFlowSetup.LitMaterial("M_Spike", Palette.Spike),
                SpikeMesh = AssetDatabase.LoadAssetAtPath<Mesh>(SpikeMeshPath),
                Coin = LoadPrefab("Coin"),
                Special = LoadPrefab("SpecialCollectible"),
                Checkpoint = LoadPrefab("Checkpoint"),
                Spikes = LoadPrefab("Spikes"),
                Mover = LoadPrefab("Mover"),
                Monsters = MonsterTableTool.Load(new List<string>()),
                Faller = LoadPrefab("Faller"),
                Goal = LoadPrefab("Goal"),
                SpikeTrap = LoadPrefab("SpikeTrap"),
                FloorSwitch = LoadPrefab("FloorSwitch"),
                Gate = LoadPrefab("SlidingGate"),
                SectionList = Arr(d.sections),
            };
            if (ctx.SpikeMesh == null)
                throw new InvalidOperationException("Spike mesh missing at " + SpikeMeshPath);
            ctx.Theme = LoadTheme(d.theme);
            if (Arr(d.decorations).Length > 0)
                ctx.Props = PropCatalog.Load(new List<string>());
            return ctx;
        }

        /// <summary>Prefab of a monster table row. Monster prefabs are made by Tools/MyGame/Monsters, not by this builder.</summary>
        private static GameObject MonsterPrefab(SceneContext ctx, int monsterId)
        {
            if (!ctx.Monsters.TryGet(monsterId, out var record) || !record.IsImplemented)
                throw new InvalidOperationException($"Monster {monsterId} is not an Implemented row of {MonsterTableTool.TablePath}.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(record.prefabPath);
            if (prefab == null || prefab.GetComponent<Monster>() == null)
                throw new InvalidOperationException($"Monster prefab missing or outdated: {record.prefabPath} (run Tools/MyGame/Monsters/Build Missing Monster Assets).");
            return prefab;
        }

        private static GameObject LoadPrefab(string name)
        {
            string path = $"{LevelPrefabDir}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("Prefab missing: " + path);
            return prefab;
        }

        private static GameObject Spawn(GameObject prefab, string name, Transform parent, Vector3 position)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.position = position;
            return go;
        }

        /// <summary>
        /// Section parent: explicit "section" field, else the longest section id that prefixes the element id,
        /// else the section whose x range contains the element, else an "Unsectioned" root.
        /// </summary>
        private static Transform SectionFor(SceneContext ctx, string explicitSection, string id, float x0, float x1)
        {
            if (!string.IsNullOrEmpty(explicitSection) && ctx.Sections.TryGetValue(explicitSection, out var t))
                return t;

            if (!string.IsNullOrEmpty(id))
            {
                LayoutSection best = null;
                foreach (var s in ctx.SectionList)
                {
                    if (id.StartsWith(s.id + "_", StringComparison.Ordinal) && (best == null || s.id.Length > best.id.Length))
                        best = s;
                }
                if (best != null)
                    return ctx.Sections[best.id];
            }

            float mid = (x0 + x1) * 0.5f;
            foreach (var s in ctx.SectionList)
            {
                if (mid >= s.startX && mid < s.endX)
                    return ctx.Sections[s.id];
            }
            if (ctx.SectionList.Length > 0 && mid >= ctx.SectionList[ctx.SectionList.Length - 1].endX)
                return ctx.Sections[ctx.SectionList[ctx.SectionList.Length - 1].id];

            if (ctx.Unsectioned == null)
                ctx.Unsectioned = new GameObject("Unsectioned").transform;
            return ctx.Unsectioned;
        }

        private static void BuildSpikes(SceneContext ctx, LayoutSpike s)
        {
            var go = Spawn(ctx.Spikes, s.id, SectionFor(ctx, s.section, s.id, s.x, s.x + s.w), new Vector3(s.x + s.w * 0.5f, s.y, 0f));
            var box = go.GetComponent<BoxCollider>();
            // Slightly forgiving hit box: a bit narrower and lower than the visible spikes.
            EditorWire.Set(box, "m_Size", new Vector3(Mathf.Max(0.1f, s.w - 0.1f), 0.45f, SpikeDepth));
            EditorWire.Set(box, "m_Center", new Vector3(0f, 0.225f, 0f));

            int count = Mathf.Max(1, Mathf.RoundToInt(s.w / SpikeUnitWidth));
            float unitW = s.w / count;
            for (int i = 0; i < count; i++)
            {
                var unit = new GameObject($"Spike_{i + 1:00}");
                unit.transform.SetParent(go.transform, false);
                unit.transform.localPosition = new Vector3(-s.w * 0.5f + unitW * (i + 0.5f), 0f, 0f);
                unit.transform.localScale = new Vector3(unitW, 1f, SpikeDepth);
                unit.AddComponent<MeshFilter>().sharedMesh = ctx.SpikeMesh;
                unit.AddComponent<MeshRenderer>().sharedMaterial = ctx.SpikeMat;
            }
            ApplyStaticSpikeArt(ctx, go, s.w);
        }

        private static void CreateWall(string name, Transform parent, float x)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 20f, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(1f, 80f, BlockDepth);
        }
    }
}
