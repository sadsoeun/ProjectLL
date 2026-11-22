using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Builds the FIVE REALMS world map from Data/WorldMap/world-layout.json:
    ///   1. data  - StageData + WorldMapNodeData for all 25 nodes (linear unlock chain, arrow-key neighbours), StageDatabase.
    ///   2. scene - 05_WorldMap: world art prefab (+ ambient animation, WorldMapAmbient), node pads + labels, path routes + markers, avatar and camera.
    ///      The UI, pause menu and controller wiring of the scene are kept; only the world part is replaced.
    /// Menu: Tools/MyGame/World Map/Build World Map (then run Verify World Map).
    /// Route heights are sampled from the art's "_Walk" colliders; without art the JSON heights are interpolated.
    /// </summary>
    public static class WorldMapBuilder
    {
        public const string ScenePath = SceneFlowSetup.ScenesDir + "/05_WorldMap.unity";
        public const string PrefabDir = "Assets/_Project/Prefabs/WorldMap/";
        public const string WorldPrefabPath = PrefabDir + "WM_World.prefab";
        public const string WalkSuffix = "_Walk";

        /// <summary>Route sample spacing (m); the verifier checks ground under every sample.</summary>
        public const float SampleStep = 0.25f;
        private const float DotSpacing = 1.1f;
        private const float DotClearance = 1.3f; // no markers this close to a pad
        private const float RayTop = 60f;

        // The map camera looks north and down; screen right = +X, screen up = +Z.
        private static readonly Vector3 CameraOffset = new Vector3(0f, 17f, -12f);
        private const float CameraPitch = 55f;
        /// <summary>Zoom range as multiples of CameraOffset (~6 m close-up to ~62 m overview); fog follows the camera distance.</summary>
        private const float MinZoom = 0.3f, MaxZoom = 3f;

        [MenuItem("Tools/MyGame/World Map/Build World Map")]
        public static void BuildMenu() => Debug.Log(Build());

        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building the world map.");
            var layout = WorldMapLayout.Load();
            var report = new System.Text.StringBuilder("[WorldMapBuilder]\n");

            SceneFlowSetup.SaveDirtyScenesQuietly();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // The world art comes first: route heights are sampled from its walkable colliders.
            var env = FindRoot("MapEnvironment");
            bool hasArt = BuildEnvironment(env.transform);
            Physics.SyncTransforms();
            report.AppendLine(hasArt ? "  art: " + WorldPrefabPath : "  art: MISSING (placeholder sea; heights from JSON)");
            if (hasArt)
                report.AppendLine(WorldMapAmbient.Apply(env.transform.GetChild(0).gameObject));

            var routes = new Dictionary<WorldMapLayout.Edge, Vector3[]>();
            foreach (var e in layout.Edges)
                routes[e] = SampleRoute(layout.Polyline(e), out _);

            var db = BuildData(layout, routes);
            report.AppendLine($"  data: {db.Stages.Count} stages, {db.Nodes.Count} nodes, start {db.StartNodeId}");

            var nodes = BuildNodes(layout, db);
            var paths = BuildPaths(layout, routes);
            report.AppendLine($"  scene: {nodes.Count} nodes, {paths.Count} paths");

            ConfigureAvatarAndCamera(layout);
            var ctrl = UnityEngine.Object.FindFirstObjectByType<WorldMapController>()
                       ?? throw new InvalidOperationException("05_WorldMap has no WorldMapController.");
            EditorWire.Set(ctrl, "nodes", nodes);
            EditorWire.Set(ctrl, "paths", paths);
            report.AppendLine("  " + WorldMapCameraSetup.Apply(ctrl));
            report.Append(WorldMapLightingSetup.Apply()); // region lighting (keeps tuned presets)

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            report.AppendLine("  saved " + ScenePath);
            return report.ToString();
        }

        private static GameObject FindRoot(string name)
        {
            var go = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);
            return go != null ? go : new GameObject(name);
        }

        private static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------------ environment

        private static bool BuildEnvironment(Transform env)
        {
            ClearChildren(env);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldPrefabPath);
            if (prefab != null)
            {
                var world = (GameObject)PrefabUtility.InstantiatePrefab(prefab, env);
                world.transform.localPosition = Vector3.zero;
                world.transform.localRotation = Quaternion.identity;
                world.transform.localScale = Vector3.one;
                HideCollisionTerrain(world.transform);
                return true;
            }
            // Placeholder until the art exists: a sea plane under the 120 x 90 m layout.
            var sea = SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "PlaceholderSea", env, new Vector3(60f, -0.1f, 45f),
                new Vector3(160f, 0.2f, 130f), SceneFlowSetup.LitMaterial("M_WorldMapSea", new Color(0.18f, 0.55f, 0.85f)), false);
            sea.isStatic = true;
            return false;
        }

        /// <summary>
        /// Art v2 splits each region into "Terrain_R0x_Walk" (light collision surface) and "Terrain_R0x_Surface_Deco"
        /// (detailed visual surface 1 cm above it); the collision surface keeps its collider but is not drawn.
        /// </summary>
        private static void HideCollisionTerrain(Transform world)
        {
            var all = world.GetComponentsInChildren<Transform>(true);
            foreach (var t in all)
            {
                if (!t.name.StartsWith("Terrain_") || !t.name.EndsWith(WalkSuffix)) continue;
                string surface = t.name.Substring(0, t.name.Length - WalkSuffix.Length) + "_Surface_Deco";
                if (all.Any(s => s.name == surface) && t.TryGetComponent<MeshRenderer>(out var r))
                    r.enabled = false;
            }
        }

        // ------------------------------------------------------------------ routes

        /// <summary>
        /// Resamples the polyline every SampleStep metres and snaps each sample to the walkable ground below it.
        /// <paramref name="misses"/> counts samples with no "_Walk" ground (those keep the interpolated height).
        /// </summary>
        public static Vector3[] SampleRoute(List<Vector3> polyline, out int misses)
        {
            misses = 0;
            var samples = new List<Vector3>();
            for (int i = 0; i < polyline.Count - 1; i++)
            {
                Vector3 a = polyline[i], b = polyline[i + 1];
                float len = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / SampleStep));
                for (int s = 0; s < steps; s++)
                    samples.Add(Vector3.Lerp(a, b, s / (float)steps));
            }
            samples.Add(polyline[polyline.Count - 1]);

            // Ends stay exactly on the pad anchors; inner samples follow the ground.
            for (int i = 1; i < samples.Count - 1; i++)
            {
                if (TryGroundHeight(samples[i], out float y))
                    samples[i] = new Vector3(samples[i].x, y, samples[i].z);
                else
                    misses++;
            }
            return samples.ToArray();
        }

        /// <summary>Highest "_Walk" surface under the point (non-walkable colliders are ignored).</summary>
        public static bool TryGroundHeight(Vector3 p, out float y)
        {
            y = 0f;
            bool found = false;
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, RayTop, p.z), Vector3.down, RayTop + 20f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.name.EndsWith(WalkSuffix, StringComparison.Ordinal))
                    continue;
                if (!found || hit.point.y > y)
                    y = hit.point.y;
                found = true;
            }
            return found;
        }

        // ------------------------------------------------------------------ data

        private static StageDatabase BuildData(WorldMapLayout layout, Dictionary<WorldMapLayout.Edge, Vector3[]> routes)
        {
            var stages = new List<StageData>();
            var nodes = new List<WorldMapNodeData>();
            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                var n = layout.Nodes[i];
                string stagePath = $"{SceneFlowSetup.StageDataDir}/StageData_{n.StageId}.asset";
                bool existed = AssetDatabase.LoadAssetAtPath<StageData>(stagePath) != null;
                var stage = SceneFlowSetup.LoadOrCreateAsset<StageData>(stagePath);
                if (!existed) // keep hand-tuned values of the stages that already exist
                {
                    EditorWire.Set(stage, "stageId", n.StageId);
                    EditorWire.Set(stage, "displayNameKey", $"stage.{n.StageId}.name");
                    EditorWire.Set(stage, "sceneName", $"Stage_{n.StageId}");
                    EditorWire.Set(stage, "collectibleCount", StageSession.SpecialSlotCount);
                    EditorUtility.SetDirty(stage);
                }
                stages.Add(stage);

                var node = SceneFlowSetup.LoadOrCreateAsset<WorldMapNodeData>($"{SceneFlowSetup.NodeDataDir}/Node_{n.StageId}.asset");
                EditorWire.Set(node, "nodeId", n.StageId);
                EditorWire.Set(node, "stage", stage);
                EditorWire.Set(node, "prerequisiteStageIds", PrerequisitesOf(layout, n.StageId));
                var dirs = AssignDirections(layout, n.StageId, routes);
                EditorWire.Set(node, "upNodeId", dirs[MapDirection.Up]);
                EditorWire.Set(node, "downNodeId", dirs[MapDirection.Down]);
                EditorWire.Set(node, "leftNodeId", dirs[MapDirection.Left]);
                EditorWire.Set(node, "rightNodeId", dirs[MapDirection.Right]);
                EditorWire.Set(node, "mapPosition", n.Position);
                EditorUtility.SetDirty(node);
                nodes.Add(node);
            }

            var db = SceneFlowSetup.LoadOrCreateAsset<StageDatabase>(SceneFlowSetup.DatabasePath);
            EditorWire.Set(db, "stages", stages);
            EditorWire.Set(db, "nodes", nodes);
            EditorWire.Set(db, "startNodeId", layout.Nodes[0].StageId);
            EditorUtility.SetDirty(db);
            if (!db.Validate(out string report))
                throw new InvalidOperationException("StageDatabase validation failed:\n" + report);
            return db;
        }

        /// <summary>A node opens when the node before it on the route is cleared (edges are directed from -> to).</summary>
        private static string[] PrerequisitesOf(WorldMapLayout layout, string stageId) =>
            layout.Edges.Where(e => e.To == stageId).Select(e => e.From).ToArray();

        /// <summary>
        /// Gives each neighbour the arrow key closest to the direction the route leaves this node in (screen right = +X,
        /// up = +Z). If two neighbours want the same key, the better match keeps it and the other takes its next best.
        /// </summary>
        public static Dictionary<MapDirection, string> AssignDirections(WorldMapLayout layout, string stageId,
            Dictionary<WorldMapLayout.Edge, Vector3[]> routes)
        {
            var result = new Dictionary<MapDirection, string>
            {
                [MapDirection.Up] = string.Empty, [MapDirection.Down] = string.Empty,
                [MapDirection.Left] = string.Empty, [MapDirection.Right] = string.Empty,
            };
            var wishes = new List<(string id, List<(MapDirection dir, float score)> ranked)>();
            foreach (var e in layout.Edges.Where(e => e.From == stageId || e.To == stageId))
            {
                string other = e.From == stageId ? e.To : e.From;
                var route = routes[e];
                if (e.To == stageId)
                    route = route.Reverse().ToArray();
                Vector2 heading = LeavingDirection(route);
                var ranked = new[] { MapDirection.Right, MapDirection.Left, MapDirection.Up, MapDirection.Down }
                    .Select(d => (d, Vector2.Dot(heading, ScreenVector(d))))
                    .OrderByDescending(x => x.Item2).ToList();
                wishes.Add((other, ranked));
            }
            // Strongest preference first.
            foreach (var w in wishes.OrderByDescending(w => w.ranked[0].score))
            {
                var free = w.ranked.First(r => result[r.dir] == string.Empty);
                result[free.dir] = w.id;
            }
            return result;
        }

        private static Vector2 ScreenVector(MapDirection d) => d switch
        {
            MapDirection.Right => Vector2.right,
            MapDirection.Left => Vector2.left,
            MapDirection.Up => Vector2.up,
            _ => Vector2.down,
        };

        /// <summary>Heading (XZ) from the start of the route to the point ~2.5 m along it.</summary>
        private static Vector2 LeavingDirection(Vector3[] route)
        {
            Vector3 start = route[0];
            float walked = 0f;
            for (int i = 1; i < route.Length; i++)
            {
                walked += Vector2.Distance(new Vector2(route[i - 1].x, route[i - 1].z), new Vector2(route[i].x, route[i].z));
                if (walked >= 2.5f || i == route.Length - 1)
                    return new Vector2(route[i].x - start.x, route[i].z - start.z).normalized;
            }
            return Vector2.right;
        }

        // ------------------------------------------------------------------ nodes

        private static List<WorldMapNode> BuildNodes(WorldMapLayout layout, StageDatabase db)
        {
            var root = FindRoot("Nodes").transform;
            ClearChildren(root);
            WorldMapNodePads.EnsureAssets();
            var pole = SceneFlowSetup.LitMaterial("M_Path", new Color(0.92f, 0.86f, 0.68f));
            var flagMat = SceneFlowSetup.LitMaterial("M_Flag", new Color(0.9f, 0.2f, 0.2f));
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var result = new List<WorldMapNode>();
            foreach (var n in layout.Nodes)
            {
                db.TryGetNode(n.StageId, out var data);
                var go = new GameObject("Node_" + n.StageId);
                go.transform.SetParent(root, false);
                go.transform.position = n.Position;

                var flag = new GameObject("Flag");
                flag.transform.SetParent(go.transform, false);
                flag.transform.localPosition = new Vector3(n.Radius * 0.55f, 0f, n.Radius * 0.55f); // back-right, clear of the avatar
                SceneFlowSetup.CreatePrimitive(PrimitiveType.Cylinder, "Pole", flag.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.06f, 0.6f, 0.06f), pole, false);
                SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Banner", flag.transform, new Vector3(0.2f, 1.0f, 0f), new Vector3(0.4f, 0.25f, 0.04f), flagMat, false);
                flag.SetActive(false);

                CreateLabel(n, go.transform, font);

                var node = go.AddComponent<WorldMapNode>();
                EditorWire.Set(node, "data", data);
                EditorWire.Set(node, "clearedFlag", flag);
                WorldMapNodePads.Attach(node, n.IsBoss);
                result.Add(node);
            }
            return result;
        }

        /// <summary>"1-1" style id floating above the pad, tilted to face the map camera.</summary>
        private static void CreateLabel(WorldMapLayout.Node n, Transform parent, Font font)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            go.transform.rotation = Quaternion.Euler(CameraPitch, 0f, 0f);
            var text = go.AddComponent<TextMesh>();
            text.text = n.LayoutId;
            text.font = font;
            text.fontSize = 64;
            text.characterSize = 0.06f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontStyle = FontStyle.Bold;
            text.color = n.IsBoss ? new Color(1f, 0.9f, 0.85f) : Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        // ------------------------------------------------------------------ paths

        private static List<WorldMapPath> BuildPaths(WorldMapLayout layout, Dictionary<WorldMapLayout.Edge, Vector3[]> routes)
        {
            var root = FindRoot("Paths").transform;
            ClearChildren(root);
            var dotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "WM_PathDot.prefab");
            var dotMat = SceneFlowSetup.LitMaterial("M_Path", new Color(0.92f, 0.86f, 0.68f));

            var result = new List<WorldMapPath>();
            foreach (var e in layout.Edges)
            {
                var route = routes[e];
                var go = new GameObject($"Path_{e.From}_{e.To}");
                go.transform.SetParent(root, false);
                var path = go.AddComponent<WorldMapPath>();
                EditorWire.Set(path, "fromNodeId", e.From);
                EditorWire.Set(path, "toNodeId", e.To);
                EditorWire.Set(path, "routePoints", route);
                PlaceDots(route, go.transform, dotPrefab, dotMat);
                result.Add(path);
            }
            return result;
        }

        private static void PlaceDots(Vector3[] route, Transform parent, GameObject prefab, Material mat)
        {
            float total = 0f;
            var cumulative = new float[route.Length];
            for (int i = 1; i < route.Length; i++)
                cumulative[i] = total += Vector3.Distance(route[i - 1], route[i]);

            int index = 0;
            for (float d = DotClearance; d <= total - DotClearance; d += DotSpacing)
            {
                int i = 1;
                while (i < route.Length - 1 && cumulative[i] < d)
                    i++;
                float t = Mathf.InverseLerp(cumulative[i - 1], cumulative[i], d);
                Vector3 p = Vector3.Lerp(route[i - 1], route[i], t);
                Vector3 dir = route[i] - route[i - 1];
                dir.y = 0f;
                var rot = dir.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(dir.normalized, Vector3.up) : Quaternion.identity;

                GameObject dot;
                if (prefab != null)
                {
                    dot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    dot.transform.SetPositionAndRotation(p + Vector3.up * 0.02f, rot);
                }
                else
                {
                    dot = SceneFlowSetup.CreatePrimitive(PrimitiveType.Cylinder, "Dot", parent, Vector3.zero, new Vector3(0.28f, 0.03f, 0.45f), mat, false);
                    dot.transform.SetPositionAndRotation(p + Vector3.up * 0.05f, rot);
                }
                dot.name = $"Dot_{index++:D3}";
            }
        }

        // ------------------------------------------------------------------ avatar / camera

        private static void ConfigureAvatarAndCamera(WorldMapLayout layout)
        {
            Vector3 start = layout.Nodes[0].Position;
            var player = UnityEngine.Object.FindFirstObjectByType<MapPlayer>()
                         ?? throw new InvalidOperationException("05_WorldMap has no MapPlayer.");
            player.transform.position = start;
            var visual = player.transform.Find("Visual");
            if (visual != null)
                visual.localPosition = Vector3.zero; // the anchor is the pad top

            var follow = UnityEngine.Object.FindFirstObjectByType<CameraFollow>()
                         ?? throw new InvalidOperationException("05_WorldMap has no CameraFollow.");
            EditorWire.Set(follow, "offset", CameraOffset);
            EditorWire.Set(follow, "minZoom", MinZoom);
            EditorWire.Set(follow, "maxZoom", MaxZoom);
            follow.transform.SetPositionAndRotation(start + CameraOffset, Quaternion.Euler(CameraPitch, 0f, 0f));
            var cam = follow.GetComponent<Camera>();
            if (cam != null)
                cam.farClipPlane = 400f;
        }
    }
}
