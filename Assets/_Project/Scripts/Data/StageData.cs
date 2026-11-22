using UnityEngine;

namespace MyGame
{
    /// <summary>Static description of one stage (read by InGame, Loading and WorldMap).</summary>
    [CreateAssetMenu(menuName = "MyGame/Stage Data", fileName = "StageData")]
    public sealed class StageData : ScriptableObject
    {
        [SerializeField] private string stageId = "W1_S1";
        [Tooltip("Localization key for the stage name.")]
        [SerializeField] private string displayNameKey = "stage.W1_S1.name";
        [Tooltip("File name (without extension) of the additive stage scene. Must be in Build Settings.")]
        [SerializeField] private string sceneName = "Stage_W1_S1";
        [SerializeField] private AudioClip bgm;
        [Tooltip("Seconds. 0 means no time limit.")]
        [SerializeField, Min(0f)] private float timeLimit;
        [SerializeField, Min(0)] private int collectibleCount;
        [Tooltip("Play the ending on return to the world map after this stage's first clear.")]
        [SerializeField] private bool playOutroOnFirstClear;

        public string StageId => stageId;
        public string DisplayNameKey => displayNameKey;
        public string SceneName => sceneName;
        public AudioClip Bgm => bgm;
        public float TimeLimit => timeLimit;
        public int CollectibleCount => collectibleCount;
        public bool PlayOutroOnFirstClear => playOutroOnFirstClear;
    }
}
