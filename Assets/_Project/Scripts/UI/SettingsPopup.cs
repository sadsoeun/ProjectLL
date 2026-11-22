using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Language, volume and display settings. Saves settings.json when closed (spec 5.3).</summary>
    public sealed class SettingsPopup : PopupBase
    {
        [SerializeField] private Button[] languageButtons;   // order = LocalizationTable.LanguageCodes
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle fullscreenToggle;
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private Button closeButton;

        [Header("Effect volume preview")]
        [Tooltip("Sound played while the effect volume slider moves.")]
        [SerializeField] private SfxId sfxPreviewSound = SfxId.Coin;
        [Tooltip("Shortest time between two preview sounds while dragging (seconds, unscaled).")]
        [SerializeField, Min(0.05f)] private float sfxPreviewInterval = 0.18f;

        private float lastSfxPreview = -10f;
        private Action onClosed;
        private SettingsService settings;
        private LocalizationService localization;

        private void Awake()
        {
            Assert.IsNotNull(languageButtons, "SettingsPopup.languageButtons is not assigned.");
            Assert.AreEqual(LocalizationTable.LanguageCodes.Length, languageButtons.Length);
            Assert.IsNotNull(masterSlider);
            Assert.IsNotNull(bgmSlider);
            Assert.IsNotNull(sfxSlider);
            Assert.IsNotNull(fullscreenToggle);
            Assert.IsNotNull(vsyncToggle);
            Assert.IsNotNull(closeButton);

            for (int i = 0; i < languageButtons.Length; i++)
            {
                string code = LocalizationTable.LanguageCodes[i];
                languageButtons[i].onClick.AddListener(() => OnLanguageClicked(code));
            }
            masterSlider.onValueChanged.AddListener(v => { settings.Data.masterVolume = v; settings.NotifyChanged(); });
            bgmSlider.onValueChanged.AddListener(v => { settings.Data.bgmVolume = v; settings.NotifyChanged(); });
            sfxSlider.onValueChanged.AddListener(v => { settings.Data.sfxVolume = v; settings.NotifyChanged(); PreviewSfx(); });
            fullscreenToggle.onValueChanged.AddListener(v => { settings.Data.fullscreen = v; settings.NotifyChanged(); });
            vsyncToggle.onValueChanged.AddListener(v => { settings.Data.vSync = v; settings.NotifyChanged(); });
            closeButton.onClick.AddListener(RequestClose);
        }

        public void Open(PopupHost host, Action closedCallback)
        {
            var systems = GameSystems.Instance;
            Assert.IsNotNull(systems);
            settings = systems.Settings;
            localization = systems.Localization;
            onClosed = closedCallback;

            var d = settings.Data;
            masterSlider.SetValueWithoutNotify(d.masterVolume);
            bgmSlider.SetValueWithoutNotify(d.bgmVolume);
            sfxSlider.SetValueWithoutNotify(d.sfxVolume);
            fullscreenToggle.SetIsOnWithoutNotify(d.fullscreen);
            vsyncToggle.SetIsOnWithoutNotify(d.vSync);
            UpdateLanguageMarkers();

            int current = Mathf.Max(0, LocalizationTable.IndexOf(localization.CurrentLanguage));
            BeginOpen(host, masterSlider.gameObject);
            GetComponent<SettingsPresentation>()?.RefreshValues();
        }

        /// <summary>Lets the player hear the new effect volume while the slider moves (not on every drag frame).</summary>
        private void PreviewSfx()
        {
            if (Time.unscaledTime - lastSfxPreview < sfxPreviewInterval)
                return;
            lastSfxPreview = Time.unscaledTime;
            Sfx.Play(sfxPreviewSound);
        }

        private void OnLanguageClicked(string code)
        {
            localization.SetLanguage(code);
            settings.Data.languageCode = code;
            settings.Data.hasSelectedLanguage = true;
            UpdateLanguageMarkers();
            GetComponent<SettingsPresentation>()?.RefreshValues();
            GetComponent<SettingsPresentation>()?.CloseLanguages();
        }

        private void UpdateLanguageMarkers()
        {
            string current = localization.CurrentLanguage;
            for (int i = 0; i < languageButtons.Length; i++)
            {
                GoldUiTheme.MarkLanguage(languageButtons[i], LocalizationTable.LanguageCodes[i] == current);
            }
        }

        private void RequestClose()
        {
            if(GetComponent<SettingsPresentation>()?.CloseResolutions()==true)return; // Esc / B closes the open list first
            if(GetComponent<SettingsPresentation>()?.CloseLanguages()==true)return;
            Close();
        }
        protected override void OnCancelRequested()=>RequestClose();

        protected override void OnClosed()
        {
            if (settings != null && !settings.Save())
            {
                Debug.LogError("[SettingsPopup] Failed to save settings.json.");
                var service=settings;
                UiFeedback.SaveFailed("ui.save_settings_failed",service.Save);
            }
            onClosed?.Invoke();
        }
    }
}
