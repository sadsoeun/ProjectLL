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
    public static class HudReadabilityVerification
    {
        private static string Output => Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/HudReadabilityWork/Verification");
        [InitializeOnLoadMethod]
        private static void CheckRequest()
        {
            EditorApplication.delayCall+=()=>
            {
                string request=Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/HudReadabilityWork/verify.request");
                if(!File.Exists(request)||EditorApplication.isPlayingOrWillChangePlaymode)return;
                File.Delete(request); Directory.CreateDirectory(Output);
                try { Verify(); } catch(Exception e) {File.WriteAllText(Path.Combine(Output,"error.txt"),e.ToString());Debug.LogException(e);}
            };
        }
        [MenuItem("Tools/MyGame/Verify HUD Readability")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            var scene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/06_InGame.unity");
            var pipeline=GraphicsSettings.defaultRenderPipeline;var quality=QualitySettings.renderPipeline;
            var previous=RenderTexture.active;
            try
            {
                GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
                var roots=scene.GetRootGameObjects();
                var canvas=roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).First(c=>c.name=="HUDCanvas");
                var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).First();
                camera.scene=scene;camera.enabled=false;camera.cullingMask=1<<5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.21f,.34f,.46f);
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                foreach(string n in new[]{"PauseMenu","ResultPanel","GameOverPanel","MessageText"})canvas.transform.Find(n).gameObject.SetActive(false);
                GoldUiTheme.Apply(canvas.transform);
                var hud=canvas.GetComponent<HudPresentation>();
                int count=canvas.GetComponentsInChildren<Image>(true).Length;
                GoldUiTheme.Apply(canvas.transform);
                Require(count==canvas.GetComponentsInChildren<Image>(true).Length,"Duplicate HUD elements");
                Require(Resources.Load<Sprite>("HudArt/Portrait")!=null && Resources.Load<Sprite>("HudArt/Coin")!=null,"HUD sprites missing");
                var view=canvas.GetComponent<InGameView>();
                view.SetLives(3);view.SetCoins(128);view.SetTime(42);
                canvas.transform.Find("StageNameText").GetComponent<LocalizedText>().SetText("1-3 초원");
                view.SetSpecials(new[]{true,true,false});
                int clicks=0;view.PauseClicked+=()=>clicks++;
                hud.PauseButton.onClick.Invoke();Require(clicks==1,"Pause click event not delivered exactly once");
                view.SetPauseAvailable(false);Require(!hud.PauseButton.interactable,"Pause input gate missing");view.SetPauseAvailable(true);
                view.SetLives(2);Require(canvas.transform.Find("LivesText").GetComponent<Text>().text=="× 2","Lives failed to update");view.SetLives(3);
                Require(canvas.transform.Find("CoinsText").GetComponent<Text>().text=="128","Coins failed to update");
                Require(hud.PauseButton.navigation.mode==Navigation.Mode.None,"HUD button steals gameplay navigation");
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(2560,1080),new Vector2Int(1024,768)})
                    Capture(camera,canvas,hud,size,"hud-"+size.x+"x"+size.y);
                var setVisible=typeof(InGameView).GetMethod("SetHudVisible",BindingFlags.Instance|BindingFlags.NonPublic);
                setVisible.Invoke(view,new object[]{false});
                Require(!hud.PauseButton.gameObject.activeSelf&&!canvas.transform.Find("HudStats").gameObject.activeSelf,"HUD decorations remain over modal");
                setVisible.Invoke(view,new object[]{true});
                Require(hud.PauseButton.gameObject.activeSelf,"HUD not restored");
                // Requesting pause before controller initialization must be harmless.
                var ctrl=roots.SelectMany(r=>r.GetComponentsInChildren<InGameController>(true)).First();
                typeof(InGameController).GetMethod("RequestHudPause",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ctrl,null);
                File.WriteAllText(Path.Combine(Output,"verification.txt"),"PASS: Unity compilation; sprite imports; idempotent HUD creation; live count updates; pause event once; unavailable-state gate; HUD modal hide/restore; gameplay navigation isolation; pre-initialization pause guard; 1920x1080,1280x720,2560x1080,1024x768 renders.\nIsolated actual scene preview. Full gameplay pause/resume still requires Play Mode validation.\n");
                Debug.Log("[HudReadability] PASS "+Output);
            }
            finally {RenderTexture.active=previous;EditorSceneManager.ClosePreviewScene(scene);GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=quality;}
        }
        private static void Capture(Camera camera,Canvas canvas,HudPresentation hud,Vector2Int size,string name)
        {
            var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;
            try
            {
                Canvas.ForceUpdateCanvases();hud.Layout();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                var tex=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);
                try {tex.ReadPixels(new Rect(0,0,size.x,size.y),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(Output,name+".png"),tex.EncodeToPNG());}
                finally {UnityEngine.Object.DestroyImmediate(tex);}
            }
            finally {camera.targetTexture=null;RenderTexture.active=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException(reason);}
    }
}
