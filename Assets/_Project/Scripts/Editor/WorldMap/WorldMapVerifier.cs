using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Checks that every stage is joined into one unbroken route (Tools/MyGame/World Map/Verify World Map):
    ///   G* layout graph (25 nodes, 24 edges, one chain, region links through each boss)
    ///   D* StageDatabase (unlock chain, start node)
    ///   K* arrow-key navigation (every graph neighbour reachable, nothing extra, all nodes reachable from the start)
    ///   S* 05_WorldMap scene (nodes, paths, continuous routes ending on the pads, controller wiring)
    ///   W* ground: walkable "_Walk" surface under every route sample and every pad (only when the art is in the scene)
    /// </summary>
    public static class WorldMapVerifier
    {
        private const float MaxSampleGap = 0.3f;   // m between consecutive route points
        private const float MaxStep = 0.5f;        // m height change between consecutive samples
        private const float PadGroundTolerance = 0.35f;

        [MenuItem("Tools/MyGame/World Map/Verify World Map")]
        public static void VerifyMenu()
        {
            string report = Verify(out bool ok);
            if (ok) Debug.Log(report); else Debug.LogError(report);
        }

        public static string Verify(out bool ok)
        {
            var sb = new StringBuilder("[WorldMapVerifier]\n");
            int fails = 0;
            void Check(string id, bool pass, string what)
            {
                sb.AppendLine($"{(pass ? "PASS" : "FAIL")} {id} {what}");
                if (!pass) fails++;
            }

            var layout = WorldMapLayout.Load();
            var ids = layout.Nodes.Select(n => n.StageId).ToList();

            // ---------------------------------------------------------------- G: layout graph
            Check("G1", layout.Nodes.Count == 25 && layout.Nodes.Count(n => n.IsBoss) == 5,
                $"25 nodes incl. 5 bosses (nodes {layout.Nodes.Count}, bosses {layout.Nodes.Count(n => n.IsBoss)})");
            Check("G2", ids.Distinct().Count() == ids.Count, "node ids are unique");
            Check("G3", layout.Edges.Count == 24, $"24 edges ({layout.Edges.Count})");
            var badEnds = layout.Edges.Where(e => !ids.Contains(e.From) || !ids.Contains(e.To)).ToList();
            Check("G4", badEnds.Count == 0, "every edge joins two existing nodes" + Missing(badEnds.Select(e => e.From + "->" + e.To)));

            var adj = ids.ToDictionary(i => i, i => new HashSet<string>());
            foreach (var e in layout.Edges.Except(badEnds))
            {
                adj[e.From].Add(e.To);
                adj[e.To].Add(e.From);
            }
            var reached = Flood(ids[0], id => adj[id]);
            Check("G5", reached.Count == ids.Count, $"graph is connected ({reached.Count}/{ids.Count} reachable from {ids[0]})" + Missing(ids.Except(reached)));
            Check("G6", layout.Edges.Count == ids.Count - 1 && reached.Count == ids.Count, "no loops (connected with nodes - 1 edges)");
            var wrongDegree = ids.Where((id, i) => adj[id].Count != (i == 0 || i == ids.Count - 1 ? 1 : 2)).ToList();
            Check("G7", wrongDegree.Count == 0, "first and last node have 1 neighbour, all others 2" + Missing(wrongDegree));
            bool chainOrder = Enumerable.Range(0, ids.Count - 1).All(i => adj[ids[i]].Contains(ids[i + 1]));
            Check("G8", chainOrder, $"route runs in stage order {ids[0]} -> ... -> {ids[ids.Count - 1]}");
            var crossings = layout.Edges.Where(e => layout.Find(e.From).Region != layout.Find(e.To).Region).ToList();
            bool viaBoss = crossings.All(e => layout.Find(e.From).IsBoss && layout.Find(e.To).Index == 1);
            Check("G9", crossings.Count == 4 && viaBoss, $"4 region links, each from the region boss to the next region's first stage ({crossings.Count})");

            // ---------------------------------------------------------------- D: database
            var db = AssetDatabase.LoadAssetAtPath<StageDatabase>(SceneFlowSetup.DatabasePath);
            Check("D1", db != null && db.Validate(out _), "StageDatabase exists and validates");
            if (db != null)
            {
                Check("D2", db.Nodes.Count == 25 && db.Stages.Count == 25, $"25 nodes and 25 stages in the database ({db.Nodes.Count}/{db.Stages.Count})");
                Check("D3", db.StartNodeId == ids[0], $"start node is {ids[0]} ({db.StartNodeId})");
                var badPrereq = new List<string>();
                for (int i = 0; i < ids.Count; i++)
                {
                    if (!db.TryGetNode(ids[i], out var nd)) { badPrereq.Add(ids[i] + "(missing)"); continue; }
                    var expected = i == 0 ? new string[0] : new[] { ids[i - 1] };
                    if (!nd.PrerequisiteStageIds.SequenceEqual(expected)) badPrereq.Add(ids[i]);
                }
                Check("D4", badPrereq.Count == 0, "each node unlocks after the previous stage" + Missing(badPrereq));

                // ------------------------------------------------------------ K: arrow-key navigation
                var dirs = new[] { MapDirection.Up, MapDirection.Down, MapDirection.Left, MapDirection.Right };
                var keyMismatch = new List<string>();
                foreach (var id in ids)
                {
                    if (!db.TryGetNode(id, out var nd)) continue;
                    var viaKeys = dirs.Select(nd.GetNeighborId).Where(s => !string.IsNullOrEmpty(s)).ToList();
                    if (viaKeys.Count != viaKeys.Distinct().Count() || !new HashSet<string>(viaKeys).SetEquals(adj[id]))
                        keyMismatch.Add($"{id}[{string.Join(",", viaKeys)}]");
                }
                Check("K1", keyMismatch.Count == 0, "arrow keys lead to exactly the graph neighbours (no missing, extra or duplicate)" + Missing(keyMismatch));
                var keyReach = Flood(ids[0], id => db.TryGetNode(id, out var nd)
                    ? dirs.Select(nd.GetNeighborId).Where(s => !string.IsNullOrEmpty(s)) : Enumerable.Empty<string>());
                Check("K2", keyReach.Count == ids.Count, $"every node can be walked to from {ids[0]} with the arrow keys ({keyReach.Count}/{ids.Count})" + Missing(ids.Except(keyReach)));
            }

            // ---------------------------------------------------------------- S: scene
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != WorldMapBuilder.ScenePath)
                scene = EditorSceneManager.OpenScene(WorldMapBuilder.ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            var nodes = Object.FindObjectsByType<WorldMapNode>(FindObjectsSortMode.None);
            var paths = Object.FindObjectsByType<WorldMapPath>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var nodeById = nodes.Where(n => n.Data != null).GroupBy(n => n.Data.NodeId).ToDictionary(g => g.Key, g => g.First());
            Check("S1", nodes.Length == 25 && ids.All(nodeById.ContainsKey), $"25 node objects, one per stage ({nodes.Length})" + Missing(ids.Where(i => !nodeById.ContainsKey(i))));
            var misplaced = ids.Where(i => nodeById.ContainsKey(i) && Vector3.Distance(nodeById[i].Anchor, layout.Find(i).Position) > 0.01f).ToList();
            Check("S2", misplaced.Count == 0, "node objects sit at the layout positions" + Missing(misplaced));

            var pathFor = new Dictionary<WorldMapLayout.Edge, WorldMapPath>();
            var missingPaths = new List<string>();
            foreach (var e in layout.Edges)
            {
                var matches = paths.Where(p => p.Connects(e.From, e.To)).ToList();
                if (matches.Count == 1) pathFor[e] = matches[0];
                else missingPaths.Add($"{e.From}-{e.To}(x{matches.Count})");
            }
            Check("S3", paths.Length == 24 && missingPaths.Count == 0, $"exactly one path object per edge ({paths.Length} paths)" + Missing(missingPaths));

            var badEndsScene = new List<string>();
            var gaps = new List<string>();
            foreach (var kv in pathFor)
            {
                var route = kv.Value.GetRoute(kv.Key.From);
                if (route.Length < 2) { badEndsScene.Add(kv.Key.From + "-" + kv.Key.To + "(empty)"); continue; }
                if (Vector3.Distance(route[0], layout.Find(kv.Key.From).Position) > 0.01f
                    || Vector3.Distance(route[route.Length - 1], layout.Find(kv.Key.To).Position) > 0.01f)
                    badEndsScene.Add(kv.Key.From + "-" + kv.Key.To);
                float worst = 0f;
                for (int i = 1; i < route.Length; i++)
                    worst = Mathf.Max(worst, Flat(route[i - 1], route[i]));
                if (worst > MaxSampleGap) gaps.Add($"{kv.Key.From}-{kv.Key.To}({worst:F2}m)");
            }
            Check("S4", badEndsScene.Count == 0, "every route starts and ends exactly on its two pads" + Missing(badEndsScene));
            Check("S5", gaps.Count == 0, $"routes are continuous (point spacing <= {MaxSampleGap} m)" + Missing(gaps));

            var ctrl = Object.FindFirstObjectByType<WorldMapController>();
            bool wired = false;
            if (ctrl != null)
            {
                var so = new SerializedObject(ctrl);
                var n = so.FindProperty("nodes");
                var p = so.FindProperty("paths");
                wired = n.arraySize == 25 && p.arraySize == 24
                        && Enumerable.Range(0, n.arraySize).All(i => n.GetArrayElementAtIndex(i).objectReferenceValue != null)
                        && Enumerable.Range(0, p.arraySize).All(i => p.GetArrayElementAtIndex(i).objectReferenceValue != null);
            }
            Check("S6", wired, "WorldMapController references all 25 nodes and 24 paths");

            // ---------------------------------------------------------------- W: ground under the routes
            bool hasArt = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Any(c => c.name.EndsWith(WorldMapBuilder.WalkSuffix));
            if (!hasArt)
            {
                sb.AppendLine("SKIP W* no '_Walk' colliders in the scene yet (world art not placed)");
            }
            else
            {
                var offGround = new List<string>();
                var steep = new List<string>();
                int samples = 0;
                foreach (var kv in pathFor)
                {
                    var route = kv.Value.GetRoute(kv.Key.From);
                    int miss = 0;
                    float worstStep = 0f;
                    for (int i = 1; i < route.Length - 1; i++)
                    {
                        samples++;
                        if (!WorldMapBuilder.TryGroundHeight(route[i], out float y) || Mathf.Abs(y - route[i].y) > 0.05f) miss++;
                        worstStep = Mathf.Max(worstStep, Mathf.Abs(route[i].y - route[i - 1].y));
                    }
                    if (miss > 0) offGround.Add($"{kv.Key.From}-{kv.Key.To}({miss})");
                    if (worstStep > MaxStep) steep.Add($"{kv.Key.From}-{kv.Key.To}({worstStep:F2}m)");
                }
                Check("W1", offGround.Count == 0, $"walkable ground under every route sample ({samples} samples)" + Missing(offGround));
                Check("W2", steep.Count == 0, $"no step higher than {MaxStep} m along a route" + Missing(steep));
                var floating = ids.Where(i => !WorldMapBuilder.TryGroundHeight(layout.Find(i).Position, out float y)
                                              || Mathf.Abs(y - layout.Find(i).Position.y) > PadGroundTolerance).ToList();
                Check("W3", floating.Count == 0, $"every pad rests on walkable ground (within {PadGroundTolerance} m)" + Missing(floating));
            }

            ok = fails == 0;
            sb.AppendLine(ok ? "RESULT: ALL PASS" : $"RESULT: {fails} FAIL");
            return sb.ToString();
        }

        private static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static string Missing(IEnumerable<string> items)
        {
            var list = items.ToList();
            return list.Count == 0 ? string.Empty : " -> " + string.Join(", ", list.Take(12)) + (list.Count > 12 ? " ..." : "");
        }

        private static HashSet<string> Flood(string start, System.Func<string, IEnumerable<string>> next)
        {
            var seen = new HashSet<string> { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);
            while (queue.Count > 0)
                foreach (var n in next(queue.Dequeue()))
                    if (seen.Add(n)) queue.Enqueue(n);
            return seen;
        }
    }
}
