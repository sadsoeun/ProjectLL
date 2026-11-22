using System;

namespace MyGame
{
    public enum LoadReason
    {
        None = 0,
        NewGame,
        Continue,
        StageEnter,
        StageClear,
        StageExit,
        GameOver,
        Failed,
    }

    /// <summary>Immutable description of a scene transition handed to the Loading scene.</summary>
    public sealed class LoadRequest
    {
        public SceneId TargetScene { get; }
        /// <summary>Stage to play (InGame) or the stage that was just cleared (WorldMap after StageClear).</summary>
        public string StageId { get; }
        public LoadReason Reason { get; }
        public System.Collections.Generic.IReadOnlyList<string> NewlyUnlockedNodeIds { get; }
        /// <summary>StageClear only: this clear mastered <see cref="StageId"/> for the first time (master presentation).</summary>
        public bool NewlyMastered { get; }
        public bool FirstClear { get; }

        public LoadRequest(SceneId targetScene, string stageId = null, LoadReason reason = LoadReason.None, string[] newlyUnlockedNodeIds = null, bool newlyMastered = false, bool firstClear = false)
        {
            FirstClear = firstClear;
            NewlyMastered = newlyMastered;
            if (targetScene == SceneId.Loading)
                throw new ArgumentException("Loading scene cannot be a load target.", nameof(targetScene));
            TargetScene = targetScene;
            StageId = stageId;
            Reason = reason;
            NewlyUnlockedNodeIds = Array.AsReadOnly(newlyUnlockedNodeIds == null ? Array.Empty<string>() : (string[])newlyUnlockedNodeIds.Clone());
        }

        public override string ToString() => $"LoadRequest({TargetScene}, stage={StageId ?? "-"}, reason={Reason}{(NewlyMastered ? ", newly mastered" : "")})";
    }
}
