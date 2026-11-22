using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace MyGame
{
    /// <summary>
    /// Single entry point for main-scene transitions (spec 3.3 / 4.2).
    /// Rejects requests while a transition is running, locks input, and resets Time.timeScale.
    /// </summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeDuration = 0.4f;
        [SerializeField, Min(1f)] private float sceneReadyTimeout = 15f;

        private ScreenFader fader;
        private InputModeService input;
        private AudioService bgmAudio;
        private int readyGateCount;
        private bool lockHeld;

        public bool IsBusy { get; private set; }
        /// <summary>Request waiting to be consumed by the Loading scene.</summary>
        public LoadRequest PendingRequest { get; private set; }
        /// <summary>Request that produced the currently active main scene (null when the scene was played directly).</summary>
        public LoadRequest LastRequest { get; private set; }
        public float FadeDuration => fadeDuration;

        public void Initialize(ScreenFader screenFader, InputModeService inputMode, AudioService audioService)
        {
            Assert.IsNotNull(screenFader);
            Assert.IsNotNull(inputMode);
            Assert.IsNotNull(audioService);
            fader = screenFader;
            input = inputMode;
            bgmAudio = audioService;
        }

        /// <summary>Fade-only transition. Returns false if ignored because a transition is already running.</summary>
        public bool Load(SceneId target)
        {
            Assert.IsTrue(target != SceneId.Loading, "Use LoadWithLoading for the Loading scene.");
            if (IsBusy)
            {
                Debug.Log($"[SceneLoader] Ignored Load({target}): transition already in progress.");
                return false;
            }
            BeginTransition();
            StartCoroutine(FadeLoadRoutine(target));
            return true;
        }

        /// <summary>Transition through the Loading scene. Returns false if ignored.</summary>
        public bool LoadWithLoading(LoadRequest request)
        {
            if (request == null)
            {
                Debug.LogError("[SceneLoader] LoadWithLoading called with null request.");
                return false;
            }
            if (IsBusy)
            {
                Debug.Log($"[SceneLoader] Ignored {request}: transition already in progress.");
                return false;
            }
            BeginTransition();
            PendingRequest = request;
            StartCoroutine(EnterLoadingRoutine());
            return true;
        }

        /// <summary>Called by the Loading scene. Consumes the pending request (null if none).</summary>
        public LoadRequest TakePendingRequest()
        {
            var r = PendingRequest;
            PendingRequest = null;
            return r;
        }

        /// <summary>
        /// Called by the Loading scene once the target scene is loaded (activation held).
        /// Runs on this persistent object because the Loading scene is destroyed by the swap.
        /// </summary>
        public void CompleteLoading(AsyncOperation targetOperation, LoadRequest request)
        {
            Assert.IsNotNull(targetOperation);
            Assert.IsNotNull(request);
            if (!IsBusy)
                BeginTransition(); // Loading scene played directly in the Editor
            StartCoroutine(ActivateRoutine(targetOperation, request));
        }

        /// <summary>Called by the Loading scene when it cannot continue; returns to the title with a fade.</summary>
        public void AbortLoading(string reason)
        {
            Debug.LogError($"[SceneLoader] Loading aborted: {reason}");
            if (!IsBusy)
                BeginTransition();
            StartCoroutine(FadeLoadAfterAbort());
        }

        /// <summary>Scenes that need extra initialisation time hold the fade-in with a gate (call from Awake).</summary>
        public void AcquireReadyGate() => readyGateCount++;

        public void ReleaseReadyGate()
        {
            Assert.IsTrue(readyGateCount > 0, "ReleaseReadyGate without matching AcquireReadyGate.");
            if (readyGateCount > 0)
                readyGateCount--;
        }

        private void BeginTransition()
        {
            Assert.IsFalse(IsBusy);
            IsBusy = true;
            Time.timeScale = 1f;
            if (!lockHeld)
            {
                input.PushLock();
                lockHeld = true;
            }
            bgmAudio.DuckBgm(0f, fadeDuration);
        }

        private void EndTransition()
        {
            IsBusy = false;
            if (lockHeld)
            {
                input.PopLock();
                lockHeld = false;
            }
        }

        private IEnumerator FadeLoadRoutine(SceneId target)
        {
            yield return fader.FadeTo(1f, fadeDuration);
            Time.timeScale = 1f;

            // LastRequest must be set before activation: the new scene reads it in Awake/Start.
            var previousRequest = LastRequest;
            LastRequest = new LoadRequest(target);
            var op = SceneManager.LoadSceneAsync(target.ToSceneName(), LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError($"[SceneLoader] Could not load scene '{target.ToSceneName()}'. Is it in Build Settings?");
                LastRequest = previousRequest;
                bgmAudio.DuckBgm(1f, fadeDuration);
                yield return fader.FadeTo(0f, fadeDuration);
                EndTransition();
                yield break;
            }
            yield return op;

            yield return WaitSceneReady();
            bgmAudio.DuckBgm(1f, fadeDuration);
            yield return fader.FadeTo(0f, fadeDuration);
            EndTransition();
        }

        private IEnumerator EnterLoadingRoutine()
        {
            yield return fader.FadeTo(1f, fadeDuration);
            Time.timeScale = 1f;

            var op = SceneManager.LoadSceneAsync(SceneId.Loading.ToSceneName(), LoadSceneMode.Single);
            if (op == null)
            {
                Debug.LogError("[SceneLoader] Could not load 04_Loading. Is it in Build Settings?");
                PendingRequest = null;
                bgmAudio.DuckBgm(1f, fadeDuration);
                yield return fader.FadeTo(0f, fadeDuration);
                EndTransition();
                yield break;
            }
            yield return op;
            yield return null;
            // Loading screen is visible now; IsBusy stays true until CompleteLoading finishes.
            yield return fader.FadeTo(0f, fadeDuration);
        }

        private IEnumerator ActivateRoutine(AsyncOperation targetOperation, LoadRequest request)
        {
            yield return fader.FadeTo(1f, fadeDuration);
            Time.timeScale = 1f;
            // LastRequest must be set before activation: the new scene reads it in Awake/Start.
            LastRequest = request;
            targetOperation.allowSceneActivation = true;
            while (!targetOperation.isDone)
                yield return null;

            yield return WaitSceneReady();
            bgmAudio.DuckBgm(1f, fadeDuration);
            yield return fader.FadeTo(0f, fadeDuration);
            EndTransition();
        }

        private IEnumerator FadeLoadAfterAbort()
        {
            yield return fader.FadeTo(1f, fadeDuration);
            LastRequest = new LoadRequest(SceneId.Title);
            var op = SceneManager.LoadSceneAsync(SceneId.Title.ToSceneName(), LoadSceneMode.Single);
            if (op != null)
                yield return op;
            yield return WaitSceneReady();
            bgmAudio.DuckBgm(1f, fadeDuration);
            yield return fader.FadeTo(0f, fadeDuration);
            EndTransition();
        }

        private IEnumerator WaitSceneReady()
        {
            yield return null; // let Start() of the new scene run
            float waited = 0f;
            while (readyGateCount > 0 && waited < sceneReadyTimeout)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            if (readyGateCount > 0)
            {
                Debug.LogError($"[SceneLoader] Scene did not become ready within {sceneReadyTimeout}s. Forcing fade-in.");
                readyGateCount = 0;
            }
        }
    }
}
