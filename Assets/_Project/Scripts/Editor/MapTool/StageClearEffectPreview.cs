using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    public static class StageClearEffectPreview
    {
        [MenuItem("Tools/MyGame/Preview Stage Clear (Play Mode)")]
        public static void PreviewInPlayMode()
        {
            var view = UnityEngine.Object.FindFirstObjectByType<InGameView>();
            if (view != null) view.ShowClearCelebration(StageClearCelebration.MinimumDuration);
        }

        [MenuItem("Tools/MyGame/Preview Stage Clear (Play Mode)", true)]
        private static bool CanPreview() => EditorApplication.isPlaying &&
            UnityEngine.Object.FindFirstObjectByType<InGameView>() != null;

        /// <summary>Isolated visual/behaviour check. Does not clear a real stage or write player saves.</summary>
        [MenuItem("Tools/MyGame/Verify Stage Clear Effect")]
        public static void VerifyAndRender()
        {
            string output = Path.Combine(Application.dataPath, "_Project/Scripts/Editor/MapTool/ClearEffectWork/Verification");
            Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewPreviewScene();
            RenderPipelineAsset graphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset qualityPipeline = QualitySettings.renderPipeline;
            RenderTexture previousTarget = RenderTexture.active;
            float originalTimeScale = Time.timeScale;
            GameObject root = null;
            RenderTexture target = null;
            try
            {
                // Use built-in UI rendering only in this isolated preview, then restore project settings.
                GraphicsSettings.defaultRenderPipeline = null;
                QualitySettings.renderPipeline = null;
                root = new GameObject("ClearEffectVerification");
                SceneManager.MoveGameObjectToScene(root, scene);
                var cameraGo = new GameObject("PreviewCamera", typeof(Camera));
                cameraGo.transform.SetParent(root.transform);
                var camera = cameraGo.GetComponent<Camera>();
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.16f, 0.24f, 0.22f);
                camera.orthographic = true;
                camera.orthographicSize = 5f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;

                var canvasGo = new GameObject("PreviewCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasGo.transform.SetParent(root.transform);
                var canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
                var scaler = canvasGo.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var effect = StageClearCelebration.Create(canvas.transform);
                Require(!effect.gameObject.activeSelf, "Created overlay must be hidden.");
                Require(Mathf.Approximately(effect.Play(1.5f), 2.6f), "Old serialized duration must not cut the effect short.");
                Require(effect.IsPlaying, "Play must start the timeline.");
                var group = effect.GetComponent<CanvasGroup>();
                effect.Sample(0f);
                Require(group.alpha == 0f, "Initial frame must be transparent.");
                effect.Sample(1f);
                Require(group.alpha > 0.99f, "Title hold must be visible.");
                effect.Sample(effect.Duration);
                Require(group.alpha > .99f, "End of timeline must retain the triangle for the result transition.");
                effect.Hide();
                Require(!effect.IsPlaying && !effect.gameObject.activeSelf, "Hide must release the overlay.");
                Require(Mathf.Approximately(effect.Play(4f), 4f), "Longer designer duration must be respected.");
                effect.Play(2.6f);
                Time.timeScale = 0f;
                effect.Sample(1f);
                Require(group.alpha > 0.99f, "Timeline sampling must work at zero game time scale.");

                target = new RenderTexture(1600, 900, 24);
                target.Create();
                camera.targetTexture = target;
                var title = effect.GetComponentInChildren<Text>(true);
                title.GetComponent<LocalizedText>().SetText("스테이지 클리어!");
                title.font = FontProvider.Get("ko");
                foreach (float seconds in new[] { 0.25f, 0.75f, 1.4f, 2.35f })
                    Render(effect, canvas, camera, target, seconds, Path.Combine(output, "clear-" + seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png"));
                foreach (string language in new[] { "en", "ko", "ja", "zh-Hans", "es" })
                {
                    Require(LocalizationTable.TryGet(language, "ingame.clear", out var text), "Missing clear translation: " + language);
                    title.GetComponent<LocalizedText>().SetText(text);
                    title.font = FontProvider.Get(language);
                    Render(effect, canvas, camera, target, 1.1f, Path.Combine(output, "language-" + language + ".png"));
                    Require(title.cachedTextGenerator.characterCountVisible > 0, "Title failed to generate: " + language);
                }
                var resultTime = UiBuilder.CreateLabel(canvas.transform,"VerificationTime",null,88,TextAnchor.MiddleCenter);
                var resultButton = UiBuilder.CreateButton(canvas.transform,"VerificationContinue","ingame.continue",new Vector2(480,90),40);
                effect.ShowResult(16.82f,resultTime.GetComponent<LocalizedText>(),resultButton,null);
                resultTime.GetComponent<LocalizedText>().SetText("16.82초");
                resultTime.font=FontProvider.Get("ko");
                var caption=effect.transform.Find("ClearTimeAndContinue/TimeCaption").GetComponent<LocalizedText>();
                caption.SetText("클리어 시간");caption.GetComponent<Text>().font=FontProvider.Get("ko");
                resultButton.GetComponentInChildren<LocalizedText>().SetText("계속");
                resultButton.GetComponentInChildren<Text>().font=FontProvider.Get("ko");
                title.GetComponent<LocalizedText>().SetText("스테이지 클리어!");title.font=FontProvider.Get("ko");
                foreach(float step in new[]{0f,.25f,.7f})
                    Render(effect,canvas,camera,target,effect.Duration,Path.Combine(output,"result-"+step.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+".png"),step);
                Require(resultTime.transform.parent.name=="ClearTimeAndContinue","Result controls must move out of legacy panel.");
                Require(resultButton.IsInteractable(),"Continue must unlock after the transition at timeScale zero.");
                Require(((RectTransform)effect.transform.Find("Composition")).anchoredPosition.y>0,"Existing triangle must move upward.");
                effect.Hide();
                effect.Play(2.6f);
                Require(effect.transform.GetSiblingIndex() == canvas.transform.childCount - 1, "Replay must be above the HUD.");
                Require(effect.GetComponentsInChildren<StageClearGraphic>(true).Length == 1, "Replay must reuse one graphic.");
                effect.Hide();
                File.WriteAllText(Path.Combine(output, "verification.txt"),
                    "PASS: Unity runtime/editor compilation; initial hidden state; minimum/extended duration; retained end frame; stop/replay; zero-timescale result slide and button unlock; five localized renders.\n" +
                    "Rendered from the actual uGUI effect in an isolated preview scene. No player saves or stage scenes modified.\n" +
                    "Full goal-to-result playthrough and real-time animation still require Play Mode verification.\n");
                Debug.Log("[StageClearEffect] Verification PASS: " + output);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                RenderTexture.active = previousTarget;
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                GraphicsSettings.defaultRenderPipeline = graphicsPipeline;
                QualitySettings.renderPipeline = qualityPipeline;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("[StageClearEffect] " + message);
        }

        private static void Render(StageClearCelebration effect, Canvas canvas, Camera camera, RenderTexture target, float seconds, string path, float resultSeconds=-1)
        {
            Canvas.ForceUpdateCanvases();
            effect.Sample(seconds);
            if(resultSeconds>=0)effect.SampleResult(resultSeconds);
            Canvas.ForceUpdateCanvases();
            var art = effect.GetComponentInChildren<StageClearGraphic>();
            Require(art != null && art.canvasRenderer != null, "Ornament needs a CanvasRenderer.");
            var mesh = art.canvasRenderer.GetMesh();
            Require(mesh != null && mesh.vertexCount > 300, "Gold ornament geometry was not submitted.");
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
