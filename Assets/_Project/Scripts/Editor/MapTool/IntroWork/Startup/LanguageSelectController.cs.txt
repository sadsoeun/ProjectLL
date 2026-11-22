using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// First-run language selection (spec 5.2). Focus previews a language;
    /// "hasSelectedLanguage" is written only when Confirm is pressed.
    /// </summary>
    public sealed class LanguageSelectController : MonoBehaviour
    {
        [SerializeField] private LanguageButton[] languageButtons;
        [SerializeField] private Button confirmButton;
        [SerializeField] private LocalizedText errorText;
        [SerializeField] private UiFocusKeeper focus;

        private string previewCode;
        private bool introPlaying;

        private void Awake()
        {
            Assert.IsNotNull(languageButtons, "LanguageSelectController.languageButtons is not assigned.");
            Assert.IsNotNull(confirmButton, "LanguageSelectController.confirmButton is not assigned.");
            Assert.IsNotNull(errorText, "LanguageSelectController.errorText is not assigned.");
            Assert.IsNotNull(focus, "LanguageSelectController.focus is not assigned.");
            foreach (var b in languageButtons)
            {
                b.Bind(this);
                var button = b.GetComponent<Button>();
                button.onClick.AddListener(() => focus.Select(confirmButton.gameObject));
            }
            confirmButton.onClick.AddListener(OnConfirm);
            errorText.gameObject.SetActive(false);
        }

        private void Start()
        {
            var systems = GameSystems.Instance;
            systems.InputMode.SetMode(InputMode.UI);

            // Start on the system language if supported, otherwise English.
            string initial = LocalizationTable.FromSystemLanguage(Application.systemLanguage);
            previewCode = initial;
            systems.Localization.SetLanguage(initial);
            foreach (var b in languageButtons)
            {
                if (b.LanguageCode == initial)
                {
                    focus.Select(b.gameObject);
                    return;
                }
            }
            focus.Select(languageButtons[0].gameObject);
        }

        public void Preview(string code)
        {
            if (introPlaying || !GameSystems.Exists || string.IsNullOrEmpty(code))
                return;
            previewCode = code;
            GameSystems.Instance.Localization.SetLanguage(code);
            foreach(var item in languageButtons)
            {
                int index=LocalizationTable.IndexOf(item.LanguageCode);
                item.GetComponentInChildren<LocalizedText>().SetText((item.LanguageCode==code ? "✓  " : "")+LocalizationTable.NativeNames[index]);
                GoldUiTheme.MarkLanguage(item.GetComponent<Button>(),item.LanguageCode==code);
            }
        }

        private void OnConfirm()
        {
            var systems = GameSystems.Instance;
            if (introPlaying || systems.Loader.IsBusy)
                return;
            if (!LocalizationTable.IsSupported(previewCode))
            {
                Debug.LogError($"[LanguageSelect] Unsupported language '{previewCode}'.");
                return;
            }

            var data = systems.Settings.Data;
            data.languageCode = previewCode;
            data.hasSelectedLanguage = true;
            if (!systems.Settings.Save())
            {
                // Saving failed: stay here so the user can retry (spec 3.2 requires a successful save).
                data.hasSelectedLanguage = false;
                errorText.gameObject.SetActive(true);
                errorText.SetKey("lang.save_failed");
                return;
            }
            introPlaying = true;
            focus.enabled = false;
            confirmButton.interactable = false;
            foreach (var button in languageButtons)
                button.GetComponent<Button>().interactable = false;
            IntroSequencePlayer.Play(systems, () => systems.Loader.Load(SceneId.Title));
        }
    }
}
