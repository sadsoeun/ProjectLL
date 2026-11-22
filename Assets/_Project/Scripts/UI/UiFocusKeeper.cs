using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// Keeps keyboard/gamepad focus on a menu button: restores the last selection when the mouse
    /// click on empty space cleared it. Disable restoring while no menu is shown (world map, gameplay).
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    public sealed class UiFocusKeeper : MonoBehaviour
    {
        [SerializeField] private GameObject defaultSelected;
        [SerializeField] private bool restoreFocus = true;

        private EventSystem eventSystem;
        private GameObject last;

        private void Awake()
        {
            eventSystem = GetComponent<EventSystem>();
        }

        public void SetRestore(bool value) => restoreFocus = value;

        public void Select(GameObject target)
        {
            if (target == null)
                return;
            last = target;
            eventSystem.SetSelectedGameObject(null);
            eventSystem.SetSelectedGameObject(target);
        }

        public void ClearSelection()
        {
            last = null;
            eventSystem.SetSelectedGameObject(null);
        }

        private void Start()
        {
            if (restoreFocus && defaultSelected != null)
                Select(defaultSelected);
        }

        private void Update()
        {
            var current = eventSystem.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy)
            {
                last = current;
                return;
            }
            if (!restoreFocus)
                return;

            if (IsUsable(last))
                eventSystem.SetSelectedGameObject(last);
            else if (IsUsable(defaultSelected))
                Select(defaultSelected);
        }

        private static bool IsUsable(GameObject go)
        {
            if (go == null || !go.activeInHierarchy)
                return false;
            var s = go.GetComponent<Selectable>();
            return s == null || s.IsInteractable();
        }
    }
}
