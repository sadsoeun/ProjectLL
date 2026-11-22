using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    public static class TextFitVerification
    {
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
            string output = "Assets/TextFitVerification"; Directory.CreateDirectory(output);
            var scene = EditorSceneManager.NewPreviewScene();
            var cameraObject = new GameObject("Offscreen",typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera = cameraObject.GetComponent<Camera>();camera.scene=scene;camera.orthographic=true;camera.orthographicSize=5;camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.06f,.08f);camera.cullingMask=1<<5;
            var host = new GameObject("TextFitPreview",typeof(RectTransform),typeof(Canvas));SceneManager.MoveGameObjectToScene(host,scene);
            var canvas=host.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var root=new GameObject("Design",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(host.transform,false);root.sizeDelta=new Vector2(1280,720);
            Label(root,"다국어 메뉴 — 넘치는 글자만 축소",new Vector2(0,300),new Vector2(1200,60),32,"ko");
            Label(root,"Noto Sans · 기본 크기 40 · 버튼 200 × 72",new Vector2(0,247),new Vector2(1200,40),24,"ko");
            Label(root,"언어",new Vector2(-470,190),new Vector2(180,40),24,"ko");
            Label(root,"기존",new Vector2(-175,190),new Vector2(200,40),24,"ko");
            Label(root,"수정",new Vector2(165,190),new Vector2(200,40),24,"ko");
            Label(root,"적용 크기",new Vector2(470,190),new Vector2(200,40),24,"ko");
            string[] codes={"ko","en","ja","zh-Hans","es"};
            var fixedLabels=new Text[codes.Length];var sizes=new Text[codes.Length];
            for(int i=0;i<codes.Length;i++)
            {
                float y=115-i*90;
                LocalizationTable.TryGet(codes[i],"worldmap.menu",out string caption);
                Label(root,codes[i],new Vector2(-470,y),new Vector2(180,60),28,"en");
                MakeButton(root,new Vector2(-175,y),caption,codes[i],false);
                fixedLabels[i]=MakeButton(root,new Vector2(165,y),caption,codes[i],true);
                sizes[i]=Label(root,"",new Vector2(470,y),new Vector2(230,60),24,"en");
            }
            foreach(var t in host.GetComponentsInChildren<Transform>(true))t.gameObject.layer=5;
            string report="";
            try
            {
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(2560,1080)})
                {
                    var rt=new RenderTexture(size.x,size.y,24);rt.Create();camera.targetTexture=rt;camera.aspect=(float)size.x/size.y;
                    for(int pass=0;pass<3;pass++)
                    {
                        Canvas.ForceUpdateCanvases();var bounds=((RectTransform)host.transform).rect.size;
                        root.localScale=Vector3.one*Mathf.Min(bounds.x/1280,bounds.y/720);
                        for(int i=0;i<codes.Length;i++)
                        {
                            var label=fixedLabels[i];label.GetComponent<LocalizedTextFit>().FitNow();
                            sizes[i].text="40 → "+label.fontSize;
                        }
                        Canvas.ForceUpdateCanvases();camera.Render();
                    }
                    for(int i=0;i<codes.Length;i++)
                    {
                        var label=fixedLabels[i];var fitter=label.GetComponent<LocalizedTextFit>();
                        Require(label.fontSize<=40 && fitter.DesignSize==40,"Exceeded design size");
                        Require(label.preferredWidth<=label.rectTransform.rect.width && label.preferredHeight<=label.rectTransform.rect.height,"Overflow "+codes[i]);
                        var generator=new TextGenerator();generator.Populate(label.text,label.GetGenerationSettings(label.rectTransform.rect.size));
                        Require(generator.lineCount==1,"Action wrapped "+codes[i]);
                        if(codes[i]=="ja")Require(label.fontSize<40,"Japanese did not shrink");
                        report+=size+" "+codes[i]+" font="+label.fontSize+" lines="+generator.lineCount+"\n";
                    }
                    RenderTexture.active=rt;var image=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,size.x,size.y),0,0);image.Apply();File.WriteAllBytes(output+"/menu-fit-"+size.x+"x"+size.y+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);RenderTexture.active=null;camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);
                }
                var changing=fixedLabels[2];var fit=changing.GetComponent<LocalizedTextFit>();
                changing.font=FontProvider.Get("ko");changing.text="메뉴";fit.FitNow();Require(changing.fontSize==40,"Short translation did not restore base size");
                var original=changing.rectTransform.sizeDelta;changing.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,72);fit.FitNow();Require(changing.fontSize<40,"Narrow resize not fitted");changing.rectTransform.sizeDelta=original;fit.FitNow();Require(changing.fontSize==40,"Resize did not restore base size");
                // Repeated styling/localization must not progressively reduce the base size.
                for(int i=0;i<20;i++){changing.fontSize=40;LocalizedTextFit.Attach(changing,40);}
                Require(changing.fontSize==40&&fit.DesignSize==40,"Progressive shrink");
                var body=Label(root,"Elige un nivel y continúa la aventura.",Vector2.zero,new Vector2(180,65),28,"es");
                LocalizedTextFit.Attach(body,28).FitNow();Require(body.preferredHeight<=65,"Wrapped body overflows");
                File.WriteAllText(output+"/verification.txt","PASS: 5 menu translations x 4 render sizes, single-line width/height fit, Japanese shrink, language/size restoration, no cumulative shrink, wrapped body height. Exact runtime fitting component with bundled Noto fonts; isolated UI preview, not live world-map navigation.\n"+report);
            }
            finally{RenderTexture.active=null;EditorSceneManager.ClosePreviewScene(scene);}
        }
        private static Text MakeButton(Transform parent,Vector2 position,string text,string lang,bool fit)
        {
            var r=new GameObject("MenuButton",typeof(RectTransform),typeof(Image),typeof(Button)).GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=new Vector2(200,72);r.anchoredPosition=position;r.GetComponent<Image>().color=new Color(.035f,.038f,.045f,.96f);
            foreach(float y in new[]{-35f,35f}){var edge=new GameObject("GoldEdge",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();edge.SetParent(r,false);edge.sizeDelta=new Vector2(200,2);edge.anchoredPosition=new Vector2(0,y);edge.GetComponent<Image>().color=new Color(1,.77f,.35f);}
            var label=Label(r,text,Vector2.zero,Vector2.zero,40,lang);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(24,4);label.rectTransform.offsetMax=new Vector2(-24,-4);label.verticalOverflow=VerticalWrapMode.Truncate;
            if(fit)LocalizedTextFit.Attach(label,40);
            return label;
        }
        private static Text Label(Transform parent,string text,Vector2 position,Vector2 size,int fontSize,string lang)
        {
            var r=new GameObject("Label",typeof(RectTransform),typeof(Text)).GetComponent<RectTransform>();r.SetParent(parent,false);r.sizeDelta=size;r.anchoredPosition=position;var label=r.GetComponent<Text>();label.font=FontProvider.Get(lang);label.text=text;label.fontSize=fontSize;label.fontStyle=FontStyle.Normal;label.color=new Color(1,.94f,.78f);label.alignment=TextAnchor.MiddleCenter;label.raycastTarget=false;return label;
        }
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    }
}
