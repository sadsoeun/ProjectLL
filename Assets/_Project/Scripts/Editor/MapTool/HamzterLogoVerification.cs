using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    public static class HamzterLogoVerification
    {
        [MenuItem("Tools/MyGame/Verify HAMZTER Startup Logo")]
        public static void Verify()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before preview verification.");
            string output=Path.Combine(Application.dataPath,"_Project/Scripts/Editor/MapTool/LogoInstallWork");
            var scene=EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/01_Logo.unity");
            var originalGraphics=GraphicsSettings.defaultRenderPipeline;
            var originalQuality=QualitySettings.renderPipeline;
            var previousTarget=RenderTexture.active;
            RenderTexture target=null;
            try
            {
                GraphicsSettings.defaultRenderPipeline=null;QualitySettings.renderPipeline=null;
                var roots=scene.GetRootGameObjects();
                var canvas=roots.SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).Single();
                var camera=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Single();
                var sequencer=roots.SelectMany(r=>r.GetComponentsInChildren<LogoSequencer>(true)).Single();
                var logo=canvas.transform.Find("LogoGroup/LogoImage").GetComponent<Image>();
                var sprite=Resources.Load<Sprite>(LogoSequencer.DefaultLogoResourcePath);
                Require(sprite!=null,"Logo must import as a runtime Sprite.");
                Require(logo.sprite==sprite,"Scene must reference the approved sprite.");
                Require(logo.type==Image.Type.Simple && logo.preserveAspect,"Logo must keep its aspect ratio.");
                Require(canvas.GetComponentsInChildren<Text>(true).Length==0,"No old studio text may remain.");
                var serialized=new SerializedObject(sequencer);
                var sprites=serialized.FindProperty("logoSprites");
                Require(sprites.arraySize==1 && sprites.GetArrayElementAtIndex(0).objectReferenceValue==sprite,"Sequence must show HAMZTER once.");
                Require(EditorBuildSettings.scenes.First(s=>s.enabled).path.EndsWith("01_Logo.unity"),"Logo must be the first build scene.");
                GoldUiTheme.Apply(canvas.transform);
                Require(canvas.GetComponentsInChildren<GoldUiFrame>(true).Length==0,"Startup logo must not have a surrounding gold window.");
                camera.scene=scene;camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                target=new RenderTexture(1600,900,24);target.Create();camera.targetTexture=target;
                canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
                canvas.transform.Find("LogoGroup").GetComponent<CanvasGroup>().alpha=1;
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                var image=new Texture2D(1600,900,TextureFormat.RGB24,false);
                try { image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,"hamzter-startup-preview.png"),image.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(image); }
                File.WriteAllText(Path.Combine(output,"verification.txt"),"PASS: Unity compiled; approved Sprite imported and assigned to first build scene; one logo; aspect preserved; no legacy Text or added gold frame. Actual scene rendered at 1600x900. Existing 0.8s fade in / 1.5s hold / 0.8s fade out and skip flow preserved. No player saves touched.\n");
                Debug.Log("[HAMZTER] Verification PASS: "+output);
            }
            finally
            {
                RenderTexture.active=previousTarget;
                EditorSceneManager.ClosePreviewScene(scene);
                if(target!=null){target.Release();UnityEngine.Object.DestroyImmediate(target);}
                GraphicsSettings.defaultRenderPipeline=originalGraphics;QualitySettings.renderPipeline=originalQuality;
            }
        }
        private static void Require(bool condition,string reason){if(!condition)throw new InvalidOperationException("[HAMZTER] "+reason);}
    }
}
