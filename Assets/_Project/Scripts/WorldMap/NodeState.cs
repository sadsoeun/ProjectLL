namespace MyGame
{
    public enum NodeState
    {
        Locked,
        Open,
        Cleared,
    }

    /// <summary>Pure rule: derives a node's state from save progress (spec 5.5).</summary>
    public static class NodeStateResolver
    {
        public static NodeState Resolve(WorldMapNodeData node, SaveData save)
        {
            if (node == null || save == null)
                throw new System.ArgumentNullException(node == null ? nameof(node) : nameof(save));

            if (node.Stage != null && save.IsCleared(node.Stage.StageId))
                return NodeState.Cleared;

            var prerequisites = node.PrerequisiteStageIds;
            if (prerequisites != null)
            {
                for (int i = 0; i < prerequisites.Length; i++)
                {
                    if (!save.IsCleared(prerequisites[i]))
                        return NodeState.Locked;
                }
            }
            return NodeState.Open;
        }
    }
}
