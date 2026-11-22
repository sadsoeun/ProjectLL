using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Yes/No dialog. Cancel (Esc / B) counts as "No".</summary>
    public sealed class ConfirmPopup : PopupBase
    {
        [SerializeField] private LocalizedText message;
        [SerializeField] private Button yesButton;
        [SerializeField] private Button noButton;

        private Action onYes;
        private Action onNo;
        private bool resolved;

        private void Awake()
        {
            Assert.IsNotNull(message, "ConfirmPopup.message is not assigned.");
            Assert.IsNotNull(yesButton, "ConfirmPopup.yesButton is not assigned.");
            Assert.IsNotNull(noButton, "ConfirmPopup.noButton is not assigned.");
            yesButton.onClick.AddListener(OnYesClicked);
            noButton.onClick.AddListener(OnNoClicked);
        }

        public void Open(PopupHost host, string messageKey, Action yes, Action no)
        {
            onYes = yes;
            onNo = no;
            message.SetKey(messageKey);
            string action="common.yes", cancel="ui.cancel";
            switch(messageKey)
            {
                case "title.confirm_quit": case "worldmap.confirm_quit": action="title.quit";break;
                case "title.confirm_overwrite":action="title.new_game";break;
                case "pause.confirm_exit":action="pause.exit_map";break;
                case "worldmap.confirm_load":action="pause.load";break;
                case "ui.save_settings_failed":case "ui.save_progress_failed":action="ui.retry";cancel="ui.dismiss";break;
            }
            UiKit.Key(yesButton.transform,action);UiKit.Key(noButton.transform,cancel);
            // Default focus on "No" so an accidental Enter does not destroy data.
            BeginOpen(host, noButton.gameObject);
        }

        protected override void OnCancelRequested() => OnNoClicked();

        private void OnYesClicked() => Resolve(true);
        private void OnNoClicked() => Resolve(false);

        private void Resolve(bool yes)
        {
            if (resolved)
                return;
            resolved = true;
            var callback = yes ? onYes : onNo;
            CloseThen(callback);
        }
    }
}
