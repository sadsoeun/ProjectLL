using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>
    /// Root of the persistent systems prefab. Owns references and initialises services in a fixed order.
    /// </summary>
    public sealed class GameSystems : MonoBehaviour
    {
        public static GameSystems Instance { get; private set; }
        public static bool Exists => Instance != null;

        [SerializeField] private SceneLoader sceneLoader;
        [SerializeField] private ScreenFader screenFader;
        [SerializeField] private SettingsService settingsService;
        [SerializeField] private SaveService saveService;
        [SerializeField] private LocalizationService localizationService;
        [SerializeField] private AudioService audioService;
        [SerializeField] private InputModeService inputModeService;
        [SerializeField] private PopupHost popupHost;
        [SerializeField] private StageDatabase stageDatabase;

        public SceneLoader Loader => sceneLoader;
        public ScreenFader Fader => screenFader;
        public SettingsService Settings => settingsService;
        public SaveService Save => saveService;
        public LocalizationService Localization => localizationService;
        public AudioService Audio => audioService;
        public InputModeService InputMode => inputModeService;
        public PopupHost Popups => popupHost;
        public StageDatabase StageDb => stageDatabase;
        /// <summary>Rankings (Steam leaderboards). Added at runtime, so the generated systems prefab needs no change.</summary>
        public RankingService Ranking { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Assert.IsNotNull(sceneLoader, "GameSystems.sceneLoader is not assigned.");
            Assert.IsNotNull(screenFader, "GameSystems.screenFader is not assigned.");
            Assert.IsNotNull(settingsService, "GameSystems.settingsService is not assigned.");
            Assert.IsNotNull(saveService, "GameSystems.saveService is not assigned.");
            Assert.IsNotNull(localizationService, "GameSystems.localizationService is not assigned.");
            Assert.IsNotNull(audioService, "GameSystems.audioService is not assigned.");
            Assert.IsNotNull(inputModeService, "GameSystems.inputModeService is not assigned.");
            Assert.IsNotNull(popupHost, "GameSystems.popupHost is not assigned.");
            Assert.IsNotNull(stageDatabase, "GameSystems.stageDatabase is not assigned.");

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Fixed initialisation order: settings -> language -> audio/input -> save -> loader.
            settingsService.Load();
            localizationService.Initialize(settingsService);
            audioService.Initialize(settingsService);
            inputModeService.Initialize();
            saveService.Initialize();
            Ranking = gameObject.AddComponent<RankingService>();
            Ranking.Initialize(settingsService, saveService, stageDatabase);
            sceneLoader.Initialize(screenFader, inputModeService, audioService);
            settingsService.ApplyAll();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
