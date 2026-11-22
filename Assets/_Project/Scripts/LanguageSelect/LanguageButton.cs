using UnityEngine;
using UnityEngine.EventSystems;

namespace MyGame
{
    /// <summary>Reports focus/hover on a language button so the screen can preview that language.</summary>
    public sealed class LanguageButton : MonoBehaviour, ISelectHandler, IPointerEnterHandler
    {
        [SerializeField] private string languageCode;
        private LanguageSelectController owner;

        public string LanguageCode => languageCode;

        public void Bind(LanguageSelectController controller) => owner = controller;

        public void OnSelect(BaseEventData eventData) => owner?.Preview(languageCode);

        public void OnPointerEnter(PointerEventData eventData)
        {
            // Hover only moves focus; the preview follows via OnSelect.
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
