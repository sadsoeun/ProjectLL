using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Creates the persistent GameSystems prefab before the first scene loads (spec 4.3),
    /// so any scene can be played directly in the Editor.
    /// The prefab lives in a Resources folder because Addressables is not installed.
    /// </summary>
    public static class GameBootstrapper
    {
        public const string GameSystemsResourcePath = "GameSystems";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (GameSystems.Exists)
                return; // already created (e.g. domain reload disabled)

            var prefab = Resources.Load<GameObject>(GameSystemsResourcePath);
            if (prefab == null)
            {
                Debug.LogError("[GameBootstrapper] Resources/GameSystems.prefab not found. Run 'Tools/MyGame/Setup Scene Flow' first.");
                return;
            }

            var instance = Object.Instantiate(prefab);
            instance.name = "GameSystems";
            // GameSystems.Awake registers the instance and calls DontDestroyOnLoad.
            if (!GameSystems.Exists)
                Debug.LogError("[GameBootstrapper] GameSystems prefab has no valid GameSystems component.");
        }
    }
}
