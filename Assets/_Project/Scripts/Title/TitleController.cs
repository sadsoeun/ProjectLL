using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Main menu (spec 5.3): new game, continue, settings, quit.</summary>
    public sealed class TitleController : MonoBehaviour
    {
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private LocalizedText noticeText;
        [SerializeField] private Text versionText;
        [SerializeField] private UiFocusKeeper focus;
        [Tooltip("Looping title BGM (keeps playing into the world map when the same clip is used there).")]
        [SerializeField] private AudioClip bgm;
        [Tooltip("Seconds between the title screen settling (logo in place) and the player's recorded title call.")]
        [SerializeField, Min(0f)] private float titleCallDelay = 0.35f;

        private GameSystems systems;

        private void Awake()
        {
            Assert.IsNotNull(newGameButton, "TitleController.newGameButton is not assigned.");
            Assert.IsNotNull(continueButton, "TitleController.continueButton is not assigned.");
            Assert.IsNotNull(settingsButton, "TitleController.settingsButton is not assigned.");
            Assert.IsNotNull(quitButton, "TitleController.quitButton is not assigned.");
            Assert.IsNotNull(noticeText, "TitleController.noticeText is not assigned.");
            Assert.IsNotNull(versionText, "TitleController.versionText is not assigned.");
            Assert.IsNotNull(focus, "TitleController.focus is not assigned.");

            newGameButton.onClick.AddListener(OnNewGame);
            continueButton.onClick.AddListener(OnContinue);
            settingsButton.onClick.AddListener(OnSettings);
            quitButton.onClick.AddListener(OnQuit);
#if UNITY_WEBGL
            quitButton.gameObject.SetActive(false); // a browser tab cannot be closed by the game
#endif
        }

        private void Start()
        {
            systems = GameSystems.Instance;
            systems.InputMode.SetMode(InputMode.UI);
            versionText.text = "v" + Application.version;
            if (bgm != null)
                systems.Audio.PlayBgm(bgm);

            bool hasSave = systems.Save.Refresh();
            continueButton.interactable = hasSave;
            GoldUiTheme.StyleButton(hasSave ? continueButton : newGameButton, true);
            UiScreenLayout.Apply(newGameButton.GetComponentInParent<Canvas>().transform);
            ShowSaveNotice(systems.Save.LastStatus);

            // Continue gets focus when a save exists, otherwise New Game (spec 5.3).
            focus.Select((hasSave ? continueButton : newGameButton).gameObject);
            if (hasSave)
                StartCoroutine(TitleCall());
        }

        /// <summary>The player's own title call (Voice Studio, last slot), once the logo has landed. Silent when there is none.</summary>
        private IEnumerator TitleCall()
        {
            while (IntroTitleTransition.IsActive || systems.Loader.IsBusy)
                yield return null;
            yield return new WaitForSecondsRealtime(titleCallDelay);
            if (!systems.Loader.IsBusy)
                Voice.Play(VoiceSlot.TitleCall);
        }

        private void ShowSaveNotice(SaveLoadStatus status)
        {
            switch (status)
            {
                case SaveLoadStatus.Corrupted:
                    noticeText.gameObject.SetActive(true);
                    noticeText.SetKey("title.save_corrupted");
                    break;
                case SaveLoadStatus.RecoveredFromBackup:
                    noticeText.gameObject.SetActive(true);
                    noticeText.SetKey("title.save_recovered");
                    break;
                default:
                    noticeText.gameObject.SetActive(false);
                    break;
            }
        }

        private bool CanAct() => !IntroTitleTransition.IsActive && !systems.Loader.IsBusy && !systems.Popups.IsBlocking;

        private void OnNewGame()
        {
            if (!CanAct())
                return;
            if (systems.Save.HasSave)
                systems.Popups.OpenConfirm("title.confirm_overwrite", () => WithNickname(StartNewGame));
            else
                WithNickname(StartNewGame);
        }

        /// <summary>Asks for the ranking nickname once, before the first game start; afterwards runs the action directly.</summary>
        private void WithNickname(System.Action then)
        {
            if (!systems.Ranking.NeedsNickname)
            {
                then();
                return;
            }
            systems.Popups.OpenNickname(true, _ => then());
        }

        private void StartNewGame()
        {
            if (systems.Loader.IsBusy)
                return;
            if (!systems.Save.CreateNew(systems.StageDb.StartNodeId))
            {
                noticeText.gameObject.SetActive(true);
                noticeText.SetKey("title.save_failed");
                return;
            }
            VoiceLibrary.DeleteAll(); // the recordings belong to the save that was just replaced
            systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.NewGame));
        }

        private void OnContinue()
        {
            if (!CanAct())
                return;
            if (!systems.Save.HasSave)
            {
                Debug.LogWarning("[Title] Continue pressed without a valid save.");
                return;
            }
            WithNickname(() => systems.Loader.LoadWithLoading(new LoadRequest(SceneId.WorldMap, null, LoadReason.Continue)));
        }

        private void OnSettings()
        {
            if (!CanAct())
                return;
            systems.Popups.OpenSettings();
        }

        private void OnQuit()
        {
            if (!CanAct())
                return;
            systems.Popups.OpenConfirm("title.confirm_quit", AppQuit.Quit);
        }
    }
}
