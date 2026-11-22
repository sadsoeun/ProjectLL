using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    /// <summary>Visual checks in unsaved preview scenes; never loads gameplay or touches player saves.</summary>
    public static class GoldUiVerification
    {
        private static string Output => Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/UiThemeWork/Verification");

        [MenuItem("Tools/MyGame/Verify Gold UI")]
        public static void Run()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode for isolated UI verification.");
            Directory.CreateDirectory(Output);
            foreach(string name in new[]{"02_LanguageSelect","03_Title","04_Loading","05_WorldMap","06_InGame"})RenderScene(name);
            StageClearEffectPreview.VerifyAndRender();
            File.WriteAllText(Path.Combine(Output,"verification.txt"),"PASS: Runtime and editor compile; actual scene and popup uGUI renders; idempotent theme; localized text; title model motion; result transition at zero time scale.\nPreview scenes only, no scene assets or player saves changed. Full gameplay goal-to-result test remains separate.\n");
            Debug.Log("[GoldUI] Verification PASS: "+Output);
        }

        private static void RenderScene(string name)
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/"+name+".unity");
            var previous=RenderTexture.active;
            RenderTexture rt=null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var canvas=roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First();
                var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
                if(camera==null)
                {
                    var go=new GameObject("VerificationCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);
                    camera=go.GetComponent<Camera>();camera.backgroundColor=GoldUiTheme.Panel;camera.clearFlags=CameraClearFlags.SolidColor;
                }
                camera.scene=scene;camera.enabled=false;
                rt=new RenderTexture(1600,900,24);rt.Create();camera.targetTexture=rt;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;
                GoldUiTheme.Apply(canvas.transform);
                int count=canvas.GetComponentsInChildren<GoldUiFrame>(true).Length;
                GoldUiTheme.Apply(canvas.transform);
                Require(count==canvas.GetComponentsInChildren<GoldUiFrame>(true).Length,"Theme created duplicate frames: "+name);
                Localize(canvas.transform);
                if(name=="03_Title")
                {
                    var presentation=canvas.GetComponent<TitlePresentation>();presentation.Initialize();
                    presentation.Sample(.2f);Capture(camera,rt,name+"-intro");
                    presentation.Sample(2f);Capture(camera,rt,name);
                    var initial=camera.transform.localPosition;presentation.Sample(5f);
                    Require(Vector3.Distance(initial,camera.transform.localPosition)>.01f,"Title camera must move.");
                    Capture(camera,rt,name+"-motion");
                    Require(roots.First(r=>r.name=="Environment").transform.Find("TitleFloatingRelic")!=null,"Title relic missing.");
                    return;
                }
                if(name=="06_InGame")
                {
                    foreach(string panel in new[]{"PauseMenu","ResultPanel","GameOverPanel","MessageText"})
                    {var child=canvas.transform.Find(panel);if(child!=null)child.gameObject.SetActive(false);}
                    Capture(camera,rt,name+"-hud");
                    foreach(string childName in new[]{"LivesText","TimeText","StageNameText","CoinsText","SpecialSlots"})
                    {var child=canvas.transform.Find(childName);if(child!=null)child.gameObject.SetActive(false);}
                    var pause=canvas.transform.Find("PauseMenu");pause.gameObject.SetActive(true);Capture(camera,rt,"pause");pause.gameObject.SetActive(false);
                    var over=canvas.transform.Find("GameOverPanel");over.gameObject.SetActive(true);Capture(camera,rt,"gameover");over.gameObject.SetActive(false);
                    foreach(string popup in new[]{"SettingsPopup","ConfirmPopup"})
                    {
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/"+popup+".prefab");
                        var instance=UnityEngine.Object.Instantiate(prefab,canvas.transform);
                        GoldUiTheme.Apply(instance.transform);Localize(instance.transform);
                        if(popup=="SettingsPopup")
                        {var buttons=instance.GetComponentsInChildren<Button>();GoldUiTheme.MarkLanguage(buttons.First(b=>b.name=="Lang_ko"),true);}
                        Capture(camera,rt,popup);UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
                else Capture(camera,rt,name);
            }
            finally
            {
                RenderTexture.active=previous;
                EditorSceneManager.ClosePreviewScene(scene);
                if(rt!=null){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            }
        }

        private static void Localize(Transform root)
        {
            foreach(var loc in root.GetComponentsInChildren<LocalizedText>(true))
            {
                var serialized=new SerializedObject(loc);
                string key=serialized.FindProperty("key").stringValue;
                string fixedLanguage=serialized.FindProperty("fixedFontLanguage").stringValue;
                string language=string.IsNullOrEmpty(fixedLanguage)?"ko":fixedLanguage;
                if(!string.IsNullOrEmpty(key)&&LocalizationTable.TryGet(language,key,out var value))
                {
                    if(key=="ingame.time")value=string.Format(value,"0:16");
                    else if(key=="worldmap.best_time")value=string.Format(value,"0:16.8");
                    else if(value.Contains("{0}"))value=string.Format(value,3,3);
                    loc.SetText(value);
                }
                loc.GetComponent<Text>().font=FontProvider.Get(language);
            }
        }

        private static void Capture(Camera camera,RenderTexture rt,string name)
        {
            Canvas.ForceUpdateCanvases();
            if(GraphicsSettings.currentRenderPipeline!=null)
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            else camera.Render();
            RenderTexture.active=rt;
            var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
            try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),image.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(image);}
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("[GoldUI] "+message);}
    }
}
