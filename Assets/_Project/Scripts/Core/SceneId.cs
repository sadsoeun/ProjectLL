using System;

namespace MyGame
{
    /// <summary>Main scenes in flow order. Code never uses scene name strings directly.</summary>
    public enum SceneId
    {
        Logo = 0,
        LanguageSelect = 1,
        Title = 2,
        Loading = 3,
        WorldMap = 4,
        InGame = 5,
    }

    public static class SceneIdExtensions
    {
        public static string ToSceneName(this SceneId id)
        {
            switch (id)
            {
                case SceneId.Logo: return "01_Logo";
                case SceneId.LanguageSelect: return "02_LanguageSelect";
                case SceneId.Title: return "03_Title";
                case SceneId.Loading: return "04_Loading";
                case SceneId.WorldMap: return "05_WorldMap";
                case SceneId.InGame: return "06_InGame";
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown SceneId");
            }
        }
    }
}
