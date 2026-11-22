using System;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// Asks for the ranking nickname (first game start, or "change name" in the ranking popup).
    /// The field starts with the current name (the Steam name before one was chosen), so a gamepad player can simply confirm.
    /// Built at runtime; no prefab.
    /// </summary>
    public sealed class NicknamePopup : PopupBase
    {
        private const float Width = 920f, Height = 500f;

        private RectTransform window;
        private InputField field;
        private Button confirmButton, cancelButton;
        private LocalizedText notice;
        private Action<bool> onClosed;
        private bool mustChoose, confirmed;
        private InputMode previousMode;

        public static NicknamePopup Create(Transform parent)
        {
            var rt = GoldUiTheme.Rect("NicknamePopup", parent);
            GoldUiTheme.Stretch(rt);
            rt.gameObject.AddComponent<Image>().color = GoldUiTheme.Dim;
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt.gameObject.AddComponent<NicknamePopup>();
        }

        /// <param name="required">True on the first game start: the popup closes only with a valid name.</param>
        /// <param name="closedCallback">Receives true when a name was stored.</param>
        public void Open(PopupHost host, bool required, Action<bool> closedCallback)
        {
            var systems = GameSystems.Instance;
            mustChoose = required;
            onClosed = closedCallback;
            previousMode = systems.InputMode.Mode;
            systems.InputMode.SetMode(InputMode.UI);

            window = GoldUiTheme.Rect("Window", transform);
            window.gameObject.AddComponent<Image>().color = GoldUiTheme.Panel;
            var title = UiKit.Label(window, "Title", "nickname.title", UiKit.HeadingSize, TextAnchor.MiddleCenter);
            var desc = UiKit.Label(window, "Desc", "nickname.desc", UiKit.CaptionSize, TextAnchor.MiddleCenter);
            notice = UiKit.Label(window, "Notice", null, 22, TextAnchor.MiddleCenter);

            var box = GoldUiTheme.Rect("NameField", window);
            var boxImage = box.gameObject.AddComponent<Image>();
            boxImage.color = new Color(.051f, .060f, .065f);
            var text = UiKit.Label(box, "Text", null, 40, TextAnchor.MiddleCenter);
            var hint = UiKit.Label(box, "Placeholder", "nickname.placeholder", 30, TextAnchor.MiddleCenter);
            field = box.gameObject.AddComponent<InputField>();
            field.targetGraphic = boxImage;
            field.textComponent = text.GetComponent<Text>();
            field.placeholder = hint.GetComponent<Text>();
            field.characterLimit = RankingBoards.NicknameMaxLength;
            field.lineType = InputField.LineType.SingleLine;
            field.text = systems.Ranking.Nickname;
            field.onValueChanged.AddListener(_ => RefreshState());
            field.onSubmit.AddListener(_ => Confirm());

            confirmButton = UiKit.Button(window, "ConfirmButton", "lang.confirm");
            confirmButton.onClick.AddListener(Confirm);
            cancelButton = UiKit.Button(window, "CancelButton", "common.close");
            cancelButton.onClick.AddListener(Close);
            cancelButton.gameObject.SetActive(!mustChoose);

            BeginOpen(host, field.gameObject);

            // after the theme pass of BeginOpen, which restyles every text and button
            UiKit.Fit(window, (RectTransform)transform, Width, Height);
            UiKit.Place(title.transform, .5f, 1, 0, -34, Width - 120, 64);
            UiKit.TextStyle(title.transform, 44, TextAnchor.MiddleCenter);
            UiKit.Place(desc.transform, .5f, 1, 0, -108, Width - 140, 72);
            UiKit.TextStyle(desc.transform, 25, TextAnchor.MiddleCenter);
            desc.GetComponent<Text>().color = UiKit.Muted;
            UiKit.Place(box, .5f, 1, 0, -200, 600, 84);
            GoldUiTheme.Stretch((RectTransform)text.transform);
            GoldUiTheme.Stretch((RectTransform)hint.transform);
            UiKit.TextStyle(text.transform, 40, TextAnchor.MiddleCenter);
            UiKit.TextStyle(hint.transform, 28, TextAnchor.MiddleCenter);
            hint.GetComponent<Text>().color = new Color(.77f, .75f, .70f, .55f);
            UiKit.Place(notice.transform, .5f, 1, 0, -296, Width - 140, 40);
            UiKit.TextStyle(notice.transform, 22, TextAnchor.MiddleCenter);
            notice.GetComponent<Text>().color = UiKit.Warning;
            if (mustChoose)
                UiKit.Place(confirmButton.transform, .5f, 0, 0, 44, 340, 86);
            else
            {
                UiKit.Place(confirmButton.transform, .5f, 0, -180, 44, 320, 86);
                UiKit.Place(cancelButton.transform, .5f, 0, 180, 44, 320, 86);
                UiKit.ButtonStyle(cancelButton);
            }
            UiKit.ButtonStyle(confirmButton, true);

            var fieldNav = new Navigation { mode = Navigation.Mode.Explicit, selectOnDown = confirmButton };
            field.navigation = fieldNav;
            confirmButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit, selectOnUp = field,
                selectOnRight = mustChoose ? null : cancelButton, selectOnLeft = mustChoose ? null : cancelButton,
            };
            cancelButton.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = field, selectOnLeft = confirmButton, selectOnRight = confirmButton };

            field.ActivateInputField();
            field.MoveTextEnd(false);
            RefreshState();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (window != null)
                UiKit.Fit(window, (RectTransform)transform, Width, Height);
        }

        private void RefreshState()
        {
            confirmButton.interactable = RankingBoards.CleanNickname(field.text).Length > 0;
            notice.SetText(string.Empty);
        }

        private void Confirm()
        {
            if (IsClosed)
                return;
            if (RankingBoards.CleanNickname(field.text).Length == 0)
            {
                field.ActivateInputField();
                return;
            }
            if (!GameSystems.Instance.Ranking.SetNickname(field.text))
            {
                notice.SetKey("lang.save_failed");
                return;
            }
            confirmed = true;
            Close();
        }

        protected override void OnCancelRequested()
        {
            if (!mustChoose)
                Close();
        }

        protected override void OnClosed()
        {
            if (GameSystems.Exists)
                GameSystems.Instance.InputMode.SetMode(previousMode);
            onClosed?.Invoke(confirmed);
        }
    }
}
