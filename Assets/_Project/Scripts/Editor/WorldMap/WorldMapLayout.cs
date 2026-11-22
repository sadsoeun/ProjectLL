using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// FIVE REALMS world layout (Data/WorldMap/world-layout.json): the authority for node positions and connectivity.
    /// JSON axes are X east / Y north / Z up; Unity positions are (x, z, y), so the world map art exported from
    /// Blender in JSON coordinates lines up with the generated nodes.
    /// Node ids "1-1" .. "1-B" map to stage ids "W1_S1" .. "W1_B" (the first four keep the existing Stage 1 ids).
    /// </summary>
    public sealed class WorldMapLayout
    {
        public const string LayoutPath = "Assets/_Project/Data/WorldMap/world-layout.json";

        public sealed class Node
        {
            public string LayoutId;   // "1-1", "1-B"
            public string StageId;    // "W1_S1", "W1_B"
            public int Region;
            public int Index;         // 1..5 within the region (5 = boss)
            public bool IsBoss;
            public Vector3 Position;  // Unity space, pad top
            public float Radius;
        }

        public sealed class Edge
        {
            public string From;       // stage ids
            public string To;
            public string Kind;
            public List<Vector3> Waypoints = new List<Vector3>(); // Unity space
        }

        public readonly List<Node> Nodes = new List<Node>();
        public readonly List<Edge> Edges = new List<Edge>();

        public static Vector3 ToUnity(JToken p) => new Vector3((float)p[0], (float)p[2], (float)p[1]);

        public static string ToStageId(string layoutId)
        {
            string[] parts = layoutId.Split('-');
            if (parts.Length != 2)
                throw new FormatException($"Bad node id '{layoutId}'.");
            return parts[1] == "B" ? $"W{parts[0]}_B" : $"W{parts[0]}_S{parts[1]}";
        }

        public static WorldMapLayout Load(string path = LayoutPath)
        {
            var root = JObject.Parse(File.ReadAllText(path));
            var layout = new WorldMapLayout();
            foreach (var n in root["nodes"])
            {
                string id = (string)n["id"];
                layout.Nodes.Add(new Node
                {
                    LayoutId = id,
                    StageId = ToStageId(id),
                    Region = (int)n["region"],
                    Index = (int)n["stage_index"],
                    IsBoss = (string)n["type"] == "boss",
                    Position = ToUnity(n["position"]),
                    Radius = (float)n["marker_radius"],
                });
            }
            foreach (var e in root["edges"])
            {
                var edge = new Edge
                {
                    From = ToStageId((string)e["from"]),
                    To = ToStageId((string)e["to"]),
                    Kind = (string)e["kind"],
                };
                foreach (var w in e["waypoints"])
                    edge.Waypoints.Add(ToUnity(w));
                layout.Edges.Add(edge);
            }
            return layout;
        }

        public Node Find(string stageId) => Nodes.Find(n => n.StageId == stageId);

        /// <summary>Node anchor, waypoints, node anchor (Unity space).</summary>
        public List<Vector3> Polyline(Edge e)
        {
            var points = new List<Vector3> { Find(e.From).Position };
            points.AddRange(e.Waypoints);
            points.Add(Find(e.To).Position);
            return points;
        }
    }
}
