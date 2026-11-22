using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyGame
{
    public sealed class VoiceSlotUnlockNotice : MonoBehaviour, IPointerClickHandler
    {
        private const float RevealDuration = 1.35f;
        private RectTransform content;
        private CanvasGroup overlay, heading, copy, hint;
        private LocalizedText slotName;
        private VoiceUnlockGraphic emblem;
        private bool canDismiss, dismissed;

        public static VoiceSlotUnlockNotice Create(Transform parent)
        {
            var root = UiKit.Rect("VoiceSlotUnlockNotice", parent);
            GoldUiTheme.Stretch(root);
            var view = UiKit.Ensure<VoiceSlotUnlockNotice>(root);
            view.Build();
            root.gameObject.SetActive(false);
            return view;
        }

        private void Build()
        {
            overlay = gameObject.AddComponent<CanvasGroup>();
            gameObject.AddComponent<Image>().color = new Color(.025f, .020f, .029f, .91f);
            content = UiKit.Rect("Composition", transform);
            var art = UiKit.Rect("VoiceEmblem", content);
            UiKit.Place(art, .5f, .5f, 0, 33, 1180, 600);
            emblem = UiKit.Ensure<VoiceUnlockGraphic>(art);
            emblem.raycastTarget = false;
            var title = UiKit.Label(content, "Heading", "voice.unlock.heading", 32, TextAnchor.MiddleCenter);
            UiKit.Place(title.transform, .5f, .5f, 0, 196, 820, 52);
            title.GetComponent<Text>().color = new Color(1f, .83f, .54f);
            heading = title.gameObject.AddComponent<CanvasGroup>();
            var details = UiKit.Rect("Details", content);
            GoldUiTheme.Stretch(details);
            copy = details.gameObject.AddComponent<CanvasGroup>();
            slotName = UiKit.Label(details, "SlotName", null, 57, TextAnchor.MiddleCenter);
            UiKit.Place(slotName.transform, .5f, .5f, 0, -125, 1000, 80);
            var studio = UiKit.Label(details, "Studio", "voice.unlock.studio", 26, TextAnchor.MiddleCenter);
            UiKit.Place(studio.transform, .5f, .5f, 0, -190, 980, 48);
            studio.GetComponent<Text>().color = new Color(.86f, .82f, .74f);
            var prompt = UiKit.Label(content, "Continue", "worldmap.unlock_continue", 21, TextAnchor.MiddleCenter);
            UiKit.Place(prompt.transform, .5f, .5f, 0, -268, 980, 40);
            prompt.GetComponent<Text>().color = new Color(.83f, .74f, .59f);
            hint = prompt.gameObject.AddComponent<CanvasGroup>();
        }

        public IEnumerator Show(VoiceSlot slot)
        {
            dismissed = canDismiss = false;
            slotName.SetKey(VoiceTextKeys.NameKey(slot));
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Fit();
            float time = 0;
            bool sounded = false;
            while (!dismissed)
            {
                overlay.alpha = Mathf.SmoothStep(0, 1, time / .24f);
                heading.alpha = Mathf.SmoothStep(0, 1, (time - .18f) / .32f);
                copy.alpha = Mathf.SmoothStep(0, 1, (time - .67f) / .38f);
                copy.transform.localPosition = Vector3.down * ((1 - copy.alpha) * 14);
                hint.alpha = Mathf.SmoothStep(0, 1, (time - 1.08f) / .27f);
                emblem.Sample(time);
                if (!sounded && time >= .54f)
                {
                    sounded = true;
                    Sfx.Play(SfxId.Special);
                }
                canDismiss = time >= RevealDuration;
                yield return null;
                time += Time.unscaledDeltaTime;
            }
            canDismiss = false;
            for (float exit = 0; exit < .23f; exit += Time.unscaledDeltaTime)
            {
                overlay.alpha = 1 - Mathf.SmoothStep(0, 1, exit / .23f);
                yield return null;
            }
            gameObject.SetActive(false);
        }

        private void OnRectTransformDimensionsChange() { if (content != null) Fit(); }
        private void Fit() => UiKit.Fit(content, (RectTransform)transform, 1180, 650);
        public void Dismiss() { if (canDismiss) dismissed = true; }
        public void OnPointerClick(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left) Dismiss(); }
    }
}
