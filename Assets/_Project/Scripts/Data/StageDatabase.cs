using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MyGame
{
    /// <summary>Lookup table for all StageData and WorldMapNodeData assets.</summary>
    [CreateAssetMenu(menuName = "MyGame/Stage Database", fileName = "StageDatabase")]
    public sealed class StageDatabase : ScriptableObject
    {
        [SerializeField] private List<StageData> stages = new List<StageData>();
        [SerializeField] private List<WorldMapNodeData> nodes = new List<WorldMapNodeData>();
        [SerializeField] private string startNodeId = "W1_S1";

        public IReadOnlyList<StageData> Stages => stages;
        public IReadOnlyList<WorldMapNodeData> Nodes => nodes;
        public string StartNodeId => startNodeId;

        public bool TryGetStage(string stageId, out StageData stage)
        {
            stage = null;
            if (string.IsNullOrEmpty(stageId))
                return false;
            for (int i = 0; i < stages.Count; i++)
            {
                if (stages[i] != null && stages[i].StageId == stageId)
                {
                    stage = stages[i];
                    return true;
                }
            }
            return false;
        }

        public bool TryGetNode(string nodeId, out WorldMapNodeData node)
        {
            node = null;
            if (string.IsNullOrEmpty(nodeId))
                return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].NodeId == nodeId)
                {
                    node = nodes[i];
                    return true;
                }
            }
            return false;
        }

        public bool TryGetNodeByStage(string stageId, out WorldMapNodeData node)
        {
            node = null;
            if (string.IsNullOrEmpty(stageId))
                return false;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null && nodes[i].Stage != null && nodes[i].Stage.StageId == stageId)
                {
                    node = nodes[i];
                    return true;
                }
            }
            return false;
        }

        /// <summary>Checks reference integrity. Returns true if the database is usable.</summary>
        public bool Validate(out string report)
        {
            var sb = new StringBuilder();
            var ids = new HashSet<string>();
            foreach (var s in stages)
            {
                if (s == null) { sb.AppendLine("null StageData entry"); continue; }
                if (!ids.Add(s.StageId)) sb.AppendLine($"duplicate stageId '{s.StageId}'");
                if (string.IsNullOrEmpty(s.SceneName)) sb.AppendLine($"stage '{s.StageId}' has empty sceneName");
            }
            var nodeIds = new HashSet<string>();
            foreach (var n in nodes)
            {
                if (n == null) { sb.AppendLine("null WorldMapNodeData entry"); continue; }
                if (!nodeIds.Add(n.NodeId)) sb.AppendLine($"duplicate nodeId '{n.NodeId}'");
                if (n.Stage == null) sb.AppendLine($"node '{n.NodeId}' has no StageData");
            }
            foreach (var n in nodes)
            {
                if (n == null) continue;
                foreach (var p in n.PrerequisiteStageIds)
                    if (!ids.Contains(p)) sb.AppendLine($"node '{n.NodeId}' prerequisite '{p}' is not a known stage");
                foreach (MapDirection d in new[] { MapDirection.Up, MapDirection.Down, MapDirection.Left, MapDirection.Right })
                {
                    string nb = n.GetNeighborId(d);
                    if (!string.IsNullOrEmpty(nb) && !nodeIds.Contains(nb))
                        sb.AppendLine($"node '{n.NodeId}' {d} neighbour '{nb}' does not exist");
                }
            }
            if (!nodeIds.Contains(startNodeId)) sb.AppendLine($"startNodeId '{startNodeId}' does not exist");
            report = sb.ToString();
            return sb.Length == 0;
        }
    }
}
