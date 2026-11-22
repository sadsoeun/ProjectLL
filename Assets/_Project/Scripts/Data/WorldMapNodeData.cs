using UnityEngine;

namespace MyGame
{
    public enum MapDirection
    {
        None = 0,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>One node on the world map: which stage it enters, what unlocks it, and its neighbours.</summary>
    [CreateAssetMenu(menuName = "MyGame/World Map Node Data", fileName = "WorldMapNodeData")]
    public sealed class WorldMapNodeData : ScriptableObject
    {
        [SerializeField] private string nodeId = "W1_S1";
        [SerializeField] private StageData stage;
        [Tooltip("Stage ids that must be cleared before this node opens. Empty = open from the start.")]
        [SerializeField] private string[] prerequisiteStageIds = new string[0];
        [SerializeField] private string upNodeId;
        [SerializeField] private string downNodeId;
        [SerializeField] private string leftNodeId;
        [SerializeField] private string rightNodeId;
        [Tooltip("Position of the node marker in the WorldMap scene.")]
        [SerializeField] private Vector3 mapPosition;

        public string NodeId => nodeId;
        public StageData Stage => stage;
        public string[] PrerequisiteStageIds => prerequisiteStageIds;
        public Vector3 MapPosition => mapPosition;

        public string GetNeighborId(MapDirection direction)
        {
            switch (direction)
            {
                case MapDirection.Up: return upNodeId;
                case MapDirection.Down: return downNodeId;
                case MapDirection.Left: return leftNodeId;
                case MapDirection.Right: return rightNodeId;
                default: return null;
            }
        }
    }
}
