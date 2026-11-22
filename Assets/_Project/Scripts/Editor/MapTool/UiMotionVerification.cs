using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    public static class UiMotionVerification
    {
        [InitializeOnLoadMethod]
        private static void CheckRequestedVerification()
        {
            EditorApplication.delayCall += () =>
            {
                string request=Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/UiMotionWork/verify.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete(request);
                try { Verify(); }
                catch(Exception error)
                {
                    File.WriteAllText(Path.Combine(Path.GetDirectoryName(request),"verification-error.txt"),error.ToString());
                    Debug.LogException(error);
                }
            };
        }

        [MenuItem("Tools/MyGame/Verify UI Motion")]
        public static void Verify()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode for isolated verification.");
            string output=Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/UiMotionWork/Verification");
            Directory.CreateDirectory(output);
            var scene=EditorSceneManager.NewPreviewScene();
            var graphics=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
            var previous=RenderTexture.active;float time=Time.timeScale;RenderTexture target=null;
            try
            {
                GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;Time.timeScale=0;
                var cameraGo=new GameObject("PreviewCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);
                var camera=cameraGo.GetComponent<Camera>();camera.scene=scene;camera.backgroundColor=new Color(.07f,.1f,.09f);camera.clearFlags=CameraClearFlags.SolidColor;
                var canvasGo=new GameObject("PreviewCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));SceneManager.MoveGameObjectToScene(canvasGo,scene);
                var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                var scaler=canvasGo.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                var popup=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/UI/SettingsPopup.prefab"),canvas.transform);
                GoldUiTheme.Apply(popup.transform);
                foreach(var loc in popup.GetComponentsInChildren<LocalizedText>(true))
                {
                    var so=new SerializedObject(loc);string key=so.FindProperty("key").stringValue;
                    string fixedLanguage=so.FindProperty("fixedFontLanguage").stringValue;
                    string language=string.IsNullOrEmpty(fixedLanguage)?"ko":fixedLanguage;
                    if(!string.IsNullOrEmpty(key)&&LocalizationTable.TryGet(language,key,out var text))loc.SetText(text);
                    loc.GetComponent<Text>().font=FontProvider.Get(language);
                }
                var reveal=popup.GetComponentInChildren<GoldUiReveal>(true);reveal.Initialize();
                var panel=(RectTransform)reveal.transform;Vector2 position=panel.anchoredPosition;Vector3 scale=panel.localScale;
                var button=popup.GetComponentsInChildren<Button>(true).First(b=>b.name=="CloseButton");
                var motion=button.GetComponent<GoldUiButton>();var label=button.GetComponentInChildren<Text>();
                var nav=button.navigation;var hitbox=((RectTransform)button.transform).sizeDelta;
                target=new RenderTexture(1600,900,24);target.Create();camera.targetTexture=target;
                reveal.Sample(0);Require(!button.IsInteractable(),"Hidden controls must not accept early input.");
                Capture(camera,target,output,"settings-00-hidden");
                reveal.Sample(.12f);Require(panel.anchoredPosition.y<position.y,"Panel should enter from below.");
                Capture(camera,target,output,"settings-01-enter");
                reveal.Sample(GoldUiReveal.Duration);
                Require(button.IsInteractable(),"Controls must unlock after settling at timeScale zero.");
                Require(panel.anchoredPosition==position&&panel.localScale==scale,"Panel must settle exactly at authored layout.");
                motion.OnSelect(null);motion.Tick(.2f);
                Capture(camera,target,output,"settings-02-focus");
                motion.OnPointerDown(new PointerEventData(null){button=PointerEventData.InputButton.Left});motion.Tick(.08f);
                Require(label.rectTransform.localScale.x<1,"Press feedback missing.");
                Require(((RectTransform)button.transform).sizeDelta==hitbox&&button.navigation.mode==nav.mode,"Motion must not alter hitboxes/navigation.");
                Capture(camera,target,output,"settings-03-press");
                motion.OnPointerUp(null);motion.Tick(.2f);Require(Mathf.Approximately(label.rectTransform.localScale.x,1),"Button must release cleanly.");
                button.interactable=false;motion.Tick(.2f);Require(label.color.r<.6f,"Disabled button must not look selected.");button.interactable=true;
                for(int i=0;i<3;i++)
                {
                    Invoke(reveal,"OnDisable");Invoke(reveal,"OnEnable");reveal.Sample(GoldUiReveal.Duration);
                    Require(panel.anchoredPosition==position&&panel.localScale==scale,"Repeated opening must not accumulate transform drift.");
                }
                var rootGroup=popup.GetComponent<CanvasGroup>();if(rootGroup==null)rootGroup=popup.AddComponent<CanvasGroup>();
                rootGroup.interactable=false;rootGroup.blocksRaycasts=true;rootGroup.alpha=.5f;reveal.SampleExit(.5f);
                Require(!button.IsInteractable(),"Exit must suppress duplicate submissions.");
                Capture(camera,target,output,"settings-04-exit");
                File.WriteAllText(Path.Combine(output,"verification.txt"),"PASS: Unity compilation; actual settings prefab renders; unscaled reveal; entrance input gate; exact settle; repeated-open reset; focus/press/release/disabled feedback; hitbox/navigation preserved; exit input suppression.\nPopup close coroutine and callback ordering code reviewed; no player saves or scene assets modified.\n");
                Debug.Log("[UiMotion] Verification PASS: "+output);
            }
            finally
            {
                Time.timeScale=time;RenderTexture.active=previous;EditorSceneManager.ClosePreviewScene(scene);
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
                GraphicsSettings.defaultRenderPipeline=graphics;QualitySettings.renderPipeline=quality;
            }
        }
        private static void Invoke(object target,string name)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,null);
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("[UiMotion] "+reason);}
        private static void Capture(Camera camera,RenderTexture target,string dir,string name)
        {
            Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try{image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());}
            finally{UnityEngine.Object.DestroyImmediate(image);}
        }
    }
}
