namespace MyGame
{
    /// <summary>Master clear: the stage is cleared and every special collectible of it has been found.</summary>
    public static class StageMastery
    {
        public static bool IsMastered(StageData stage, SaveData save)
        {
            if (stage == null || save == null || stage.CollectibleCount <= 0)
                return false;
            var record = save.FindStage(stage.StageId);
            return record != null && record.cleared && record.CollectedCount() >= stage.CollectibleCount;
        }
    }
}
