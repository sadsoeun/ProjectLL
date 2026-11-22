using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// Loads the target scene with activation held, shows progress, then hands over to
    /// SceneLoader.CompleteLoading (spec 5.4). Progress weights: 10 / 50 / 30 / 10 %.
    /// </summary>
    public sealed class LoadingController : MonoBehaviour
    {
        [SerializeField] private RectTransform progressFill;
        [SerializeField] private RectTransform loadingIcon;
        [SerializeField] private LocalizedText tipText;
        [SerializeField, Min(0f)] private float minimumDisplayTime = 1.0f;
        private LoadingPresentation presentation;

        private static readonly string[] TipKeys = { "loading.tip1", "loading.tip2", "loading.tip3" };

        private void Awake()
        {
            Assert.IsNotNull(progressFill, "LoadingController.progressFill is not assigned.");
            Assert.IsNotNull(loadingIcon, "LoadingController.loadingIcon is not assigned.");
            Assert.IsNotNull(tipText, "LoadingController.tipText is not assigned.");
            presentation=LoadingPresentation.Apply(tipText.GetComponentInParent<Canvas>().transform);
            SetProgress(0f);
        }

        private void Start()
        {
            var systems = GameSystems.Instance;
            systems.InputMode.SetMode(InputMode.UI);
            tipText.SetKey(TipKeys[Random.Range(0, TipKeys.Length)]);
            StartCoroutine(Run(systems));
        }

        private void Update()
        {
            presentation?.Tick(Time.unscaledDeltaTime);
        }

        private IEnumerator Run(GameSystems systems)
        {
            float startTime = Time.unscaledTime;
            var request = systems.Loader.TakePendingRequest();
            if (request == null)
            {
                // Loading scene was played directly: continue to the world map.
                Debug.LogWarning("[Loading] No pending LoadRequest (scene played directly). Using WorldMap.");
                request = new LoadRequest(SceneId.WorldMap);
            }

            var canvas=tipText.GetComponentInParent<Canvas>();
            var destination=canvas!=null?canvas.transform.Find("LoadingContent/Destination"):null;
            if(destination!=null)
            {
                string key="worldmap.heading";
                if(request.TargetScene==SceneId.InGame&&systems.StageDb.TryGetStage(request.StageId,out var nextStage))key=nextStage.DisplayNameKey;
                destination.GetComponent<LocalizedText>().SetKey(key);
            }
            // Step 1 (10%): release unused assets from the previous scene.
            yield return Resources.UnloadUnusedAssets();
            SetProgress(0.10f);

            // Step 2 (30%): validate the stage before loading the heavy scene.
            if (request.TargetScene == SceneId.InGame && !ValidateStage(systems, request.StageId, out string error))
            {
                Debug.LogError($"[Loading] {error} Returning to the world map.");
                request = new LoadRequest(SceneId.WorldMap, null, LoadReason.Failed);
                UiFeedback.Notice("ui.loading_failed");
                if(destination!=null)destination.GetComponent<LocalizedText>().SetKey("worldmap.heading");
            }

            // Step 3 (50%): load the target scene, activation held.
            AsyncOperation op = SceneManager.LoadSceneAsync(request.TargetScene.ToSceneName(), LoadSceneMode.Single);
            if (op == null)
            {
                systems.Loader.AbortLoading($"Cannot load scene '{request.TargetScene.ToSceneName()}'. Is it in Build Settings?");
                yield break;
            }
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                // progress stops at 0.9 while activation is held: map 0..0.9 to 0..1
                SetProgress(0.10f + 0.50f * Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
            SetProgress(0.60f);

            // Step 4 (30% + 10%): data check done above; wait for the minimum display time.
            SetProgress(0.90f);
            while (Time.unscaledTime - startTime < minimumDisplayTime)
                yield return null;
            SetProgress(1f);
            yield return null;

            // The Loading scene is destroyed during activation, so the loader finishes the job.
            systems.Loader.CompleteLoading(op, request);
        }

        private static bool ValidateStage(GameSystems systems, string stageId, out string error)
        {
            error = null;
            if (!systems.StageDb.TryGetStage(stageId, out StageData stage))
            {
                error = $"Unknown StageId '{stageId}'.";
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(stage.SceneName))
            {
                error = $"Stage scene '{stage.SceneName}' is not in Build Settings.";
                return false;
            }
            return true;
        }

        private void SetProgress(float value)
        {
            var max = progressFill.anchorMax;
            max.x = Mathf.Clamp01(value);
            progressFill.anchorMax = max;
            presentation?.SetProgress(value);
        }
    }
}
