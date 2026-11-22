using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    // Explicit batch-only verification: no desktop windows, scene saves, or player-save changes.
    public static class NowCutInVerification
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly string Work = "Assets/_Project/Scripts/Editor/MapTool/CutInArtWork/Verification";
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use batch mode.");
            Directory.CreateDirectory(Work);
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            var quality = QualitySettings.renderPipeline;
            var scene = EditorSceneManager.NewPreviewScene();
            float savedScale = Time.timeScale;
            try
            {
                GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
                foreach (string name in new[] { "CounterPortrait", "NowKo" })
                {
                    string path = "Assets/_Project/Resources/CutInArt/" + name + ".png";
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Require(importer.alphaIsTransparency && !importer.mipmapEnabled, "Wrong alpha import " + name);
                    Require(Resources.Load<Texture2D>("CutInArt/" + name) != null, "Missing runtime art " + name);
                }
                var camObject = new GameObject("OffscreenCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(camObject, scene);
                var camera = camObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.orthographic = true; camera.orthographicSize = 5;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.05f, .07f, .1f);
                camera.cullingMask = 1 << 5; camera.enabled = false;
                var backdrop = new GameObject("ReferenceBackdrop", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(backdrop, scene);
                var backgroundCanvas = backdrop.GetComponent<Canvas>();
                Bind(backgroundCanvas, camera); backgroundCanvas.sortingOrder = 0;
                var bg = new GameObject("ExistingGameplayReference", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                bg.transform.SetParent(backdrop.transform, false);
                bg.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Scripts/Editor/MapTool/PresentationWork/Captures/dodge-71.9.png");
                bg.raycastTarget = false;

                foreach (string lang in new[] { "ko", "en", "ja", "zh-Hans", "es" })
                {
                    var host = new GameObject("CutInPreview");
                    SceneManager.MoveGameObjectToScene(host, scene);
                    var cue = host.AddComponent<NowCutIn>(); cue.enabled = false;
                    typeof(NowCutIn).GetMethod("BuildUi", Flags).Invoke(cue, new object[] { lang });
                    var canvas = host.GetComponent<Canvas>(); Bind(canvas, camera);
                    foreach (var r in scene.GetRootGameObjects()) foreach (var t in r.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 5;
                    Require(host.GetComponentsInChildren<Camera>().Length == 0 && host.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 0, "3D cut-in survived");
                    Require(host.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), "Cue blocks pointer input");
                    var word = host.transform.Find("SafeComposition/CutInMotion/Now");
                    Require(lang == "ko" ? word.GetComponent<RawImage>() != null : word.GetComponent<Text>() != null, "Incorrect language treatment");
                    if (lang != "ko")
                    {
                        LocalizationTable.TryGet(lang, NowCutIn.TextKey, out string expected);
                        Require(word.GetComponent<Text>().text == expected, "Missing localization " + lang);
                        Require(word.GetComponent<Text>().font.name.StartsWith("NotoSans"), "Fallback font changed");
                    }
                    var sizes = lang == "ko" ? new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(1024,768), new Vector2Int(2560,1080) } : new[] { new Vector2Int(1920,1080) };
                    foreach (var size in sizes)
                        Capture(camera, backgroundCanvas, bg, canvas, cue, size, .42f, lang + "-hold");
                    if (lang == "ko")
                        foreach (float time in new[] { 0f, .10f, .22f, .32f, .65f, .96f, 1.15f })
                            Capture(camera, backgroundCanvas, bg, canvas, cue, sizes[0], time, "ko-t" + time.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                    UnityEngine.Object.DestroyImmediate(host);
                }
                // Check actual Play() ownership, duplicate suppression and interruption cleanup without an Animator.
                var player = new GameObject("TestPlayer"); SceneManager.MoveGameObjectToScene(player, scene);
                Time.timeScale = .65f;
                NowCutIn.Play(player.transform);
                var active = (NowCutIn)typeof(NowCutIn).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Require(NowCutIn.IsPlaying && Time.timeScale == 0, "Play did not acquire time");
                NowCutIn.Play(player.transform);
                Require(ReferenceEquals(active, typeof(NowCutIn).GetField("current", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)), "Duplicate cut-in");
                active.gameObject.SetActive(false);
                // Edit-mode objects do not receive MonoBehaviour lifecycle messages automatically.
                typeof(NowCutIn).GetMethod("OnDisable", Flags).Invoke(active, null);
                Require(!NowCutIn.IsPlaying && Mathf.Approximately(Time.timeScale, .65f), "Interrupted cue did not restore saved time");
                UnityEngine.Object.DestroyImmediate(active.gameObject);
                File.WriteAllText(Work + "/verification.txt", "PASS: Unity runtime UI rendered offscreen over an existing gameplay reference (not a live boss playthrough). 4 aspect/resolution sizes, 5 languages, 8 timeline samples. Korean image lettering, Noto Sans localization fallback, uniform portrait/text scaling, fully hidden start/end, no 3D camera or model, input pass-through, duplicate suppression, and time-scale restoration through an explicitly invoked cleanup callback verified. Isolated preview uses no-op audio/time-stop service adapters; actual project integration compiled separately.\n");
            }
            catch (Exception error) { File.WriteAllText(Work + "/error.txt", error.ToString()); throw; }
            finally
            {
                Time.timeScale = savedScale;
                GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = quality;
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private static void Bind(Canvas canvas, Camera camera)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        }
        private static void Capture(Camera camera, Canvas background, RawImage bg, Canvas canvas, NowCutIn cue, Vector2Int size, float time, string name)
        {
            var rt = new RenderTexture(size.x, size.y, 24); rt.Create(); camera.targetTexture = rt;
            var previous = RenderTexture.active;
            try
            {
                camera.aspect = (float)size.x / size.y;
                // A target-texture size change reaches ScreenSpaceCamera canvases during rendering.
                // Warm up layout before measuring; otherwise the first frame uses the previous target size.
                for (int pass = 0; pass < 3; pass++)
                {
                    Canvas.ForceUpdateCanvases();
                    var b = ((RectTransform)background.transform).rect.size;
                    float scale = Mathf.Max(b.x / 1920f, b.y / 1080f);
                    bg.rectTransform.sizeDelta = new Vector2(1920, 1080) * scale;
                    typeof(NowCutIn).GetMethod("Apply", Flags).Invoke(cue, new object[] { time });
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                }
                var layout = cue.transform.Find("SafeComposition");
                var portrait = layout.Find("CutInMotion/PortraitWindow/Illustration");
                var word = layout.Find("CutInMotion/Now");
                foreach (var t in new[] { layout, portrait, word }) Require(Mathf.Approximately(t.localScale.x, t.localScale.y), "Stretched artwork " + size);
                var group = layout.Find("CutInMotion").GetComponent<CanvasGroup>();
                if (time == 0 || time >= 1.15f) Require(group.alpha == 0, "Cue visible outside timeline");
                if (time >= .4f && time <= .8f)
                {
                    foreach (var target in new[] { (RectTransform)portrait, (RectTransform)word })
                    {
                        var corners = new Vector3[4]; target.GetWorldCorners(corners);
                        foreach (var c in corners)
                        {
                            var v = camera.WorldToViewportPoint(c);
                            Require(v.x >= -.02f && v.x <= 1.02f && v.y >= -.02f && v.y <= 1.02f, "Artwork outside screen " + size);
                        }
                    }
                }
                camera.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0); tex.Apply();
                File.WriteAllBytes(Work + "/" + name + "-" + size.x + "x" + size.y + ".png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            finally { camera.targetTexture = null; RenderTexture.active = previous; rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
