using UnityEngine;

namespace MyGame
{
    /// <summary>Ends the application (stops Play Mode in the Editor, where Application.Quit is ignored).</summary>
    public static class AppQuit
    {
        public static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
