using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>
    /// Opens modal popups on a persistent overlay canvas, so the same popups serve every scene.
    /// (Replaces the per-scene PopupRoot of the spec.)
    /// </summary>
    public sealed class PopupHost : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private SettingsPopup settingsPopupPrefab;
        [SerializeField] private ConfirmPopup confirmPopupPrefab;

        private int openCount;
        private int lastCloseFrame = -1;
        private bool settingsOpen;
        private bool voiceStudioOpen;
        private bool nicknameOpen;
        private bool rankingOpen;

        /// <summary>
        /// True while a popup is open and during the frame it closed, so the Esc press that closed
        /// a popup is not also handled by a menu underneath.
        /// </summary>
        public bool IsBlocking => openCount > 0 || Time.frameCount == lastCloseFrame;

        private void Awake()
        {
            Assert.IsNotNull(root, "PopupHost.root is not assigned.");
            Assert.IsNotNull(settingsPopupPrefab, "PopupHost.settingsPopupPrefab is not assigned.");
            Assert.IsNotNull(confirmPopupPrefab, "PopupHost.confirmPopupPrefab is not assigned.");
        }

        public void OpenSettings(Action onClosed = null)
        {
            if (settingsOpen)
                return;
            settingsOpen = true;
            var popup = Instantiate(settingsPopupPrefab, root, false);
            popup.Open(this, () =>
            {
                settingsOpen = false;
                onClosed?.Invoke();
            });
        }

        public void OpenConfirm(string messageKey, Action onYes, Action onNo = null)
        {
            if (string.IsNullOrEmpty(messageKey))
                throw new ArgumentException("messageKey is empty", nameof(messageKey));
            var popup = Instantiate(confirmPopupPrefab, root, false);
            popup.Open(this, messageKey, onYes, onNo);
        }

        /// <summary>Voice Studio (built at runtime, no prefab). Needs a loaded save: the slots follow its golden leaves.</summary>
        public void OpenVoiceStudio(Action onClosed = null)
        {
            if (!VoiceRecorder.Supported || voiceStudioOpen || !GameSystems.Exists || !GameSystems.Instance.Save.HasSave)
                return;
            voiceStudioOpen = true;
            VoiceStudioPopup.Create(root).Open(this, () =>
            {
                voiceStudioOpen = false;
                onClosed?.Invoke();
            });
        }

        /// <summary>Ranking nickname prompt (built at runtime). The callback receives true when a name was stored.</summary>
        public void OpenNickname(bool required, Action<bool> onClosed = null)
        {
            if (nicknameOpen)
                return;
            nicknameOpen = true;
            NicknamePopup.Create(root).Open(this, required, stored =>
            {
                nicknameOpen = false;
                onClosed?.Invoke(stored);
            });
        }

        /// <summary>Ranking popup (built at runtime). <paramref name="stageId"/> is the stage shown first; may be null.</summary>
        public void OpenRanking(string stageId, Action onClosed = null)
        {
            if (rankingOpen || !GameSystems.Exists)
                return;
            rankingOpen = true;
            RankingPopup.Create(root).Open(this, stageId, () =>
            {
                rankingOpen = false;
                onClosed?.Invoke();
            });
        }

        internal void NotifyOpened() => openCount++;

        internal void NotifyClosed()
        {
            if (openCount > 0)
                openCount--;
            lastCloseFrame = Time.frameCount;
        }
    }
}
