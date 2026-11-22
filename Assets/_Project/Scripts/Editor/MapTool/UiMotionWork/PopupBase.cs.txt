using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MyGame
{
    /// <summary>Shared behaviour of modal popups: focus capture/restore, Cancel to close, host bookkeeping.</summary>
    public abstract class PopupBase : MonoBehaviour
    {
        private PopupHost host;
        private GameObject previousSelection;
        private bool closed;
        private bool hostReleased;
        public const float CloseDuration = .16f;

        protected bool IsClosed => closed;

        /// <summary>Call once after instantiation.</summary>
        protected void BeginOpen(PopupHost owner, GameObject firstSelected)
        {
            GoldUiTheme.Apply(transform);
            host = owner;
            host.NotifyOpened();
            if (EventSystem.current != null)
            {
                previousSelection = EventSystem.current.currentSelectedGameObject;
                if (firstSelected != null)
                    EventSystem.current.SetSelectedGameObject(firstSelected);
            }
            else
            {
                Debug.LogWarning("[Popup] No EventSystem in the scene; keyboard/gamepad navigation will not work.");
            }
        }

        public void Close() => CloseThen(null);

        protected void CloseThen(Action afterClose)
        {
            if (closed)
                return;
            closed = true;
            if (!gameObject.activeInHierarchy) { FinishClose(afterClose); return; }
            StartCoroutine(CloseRoutine(afterClose));
        }

        private IEnumerator CloseRoutine(Action afterClose)
        {
            var group=GetComponent<CanvasGroup>();
            if(group==null)group=gameObject.AddComponent<CanvasGroup>();
            group.interactable=false;
            group.blocksRaycasts=true;
            var panels=GetComponentsInChildren<GoldUiReveal>();
            float start=group.alpha;
            for(float elapsed=0;elapsed<CloseDuration;elapsed+=Time.unscaledDeltaTime)
            {
                float t=Mathf.Clamp01(elapsed/CloseDuration);
                group.alpha=start*(1-Mathf.SmoothStep(0,1,t));
                foreach(var panel in panels)panel.SampleExit(t);
                yield return null;
            }
            group.alpha=0;
            FinishClose(afterClose);
        }

        private void FinishClose(Action afterClose)
        {
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            ReleaseHost();
            OnClosed();
            Destroy(gameObject);
            afterClose?.Invoke();
        }

        private void ReleaseHost()
        {
            if(hostReleased)return;
            hostReleased=true;
            if(host!=null)host.NotifyClosed();
        }

        protected virtual void OnClosed() { }

        /// <summary>Called when Cancel (Esc / gamepad B) is pressed. Default closes the popup.</summary>
        protected virtual void OnCancelRequested() => Close();

        private void Update()
        {
            if (closed || !GameSystems.Exists)
                return;
            if (GameSystems.Instance.InputMode.CancelPressed)
                OnCancelRequested();
        }

        private void OnDestroy()
        {
            // Destroyed externally (scene unload): keep the host counter consistent.
            closed = true;
            ReleaseHost();
        }
    }
}
