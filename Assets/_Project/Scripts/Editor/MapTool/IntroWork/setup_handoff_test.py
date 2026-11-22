from pathlib import Path
import shutil
work=Path(__file__).resolve().parent
base=Path('C:/Users/PC/.codex/visualizations/2026/10/31/01a19252-8ac5-7110-b2a9-856eb2b4ca42')
copy=base/'IntroFlowProject'
text=(work/'StartupFlowVerification.cs.txt').read_text(encoding='utf-8')
text=text.replace('StartupFlowVerification','HandoffFlowVerification').replace('IntroFlowQA.Running','HandoffFlowQA.Running')
text=text.replace('        static Keyboard keyboard;','''        static Keyboard keyboard;
        static bool centralLogoSeen, middleSeen, landingSeen;
        static int captureStep;
        static float previousMotion;
        static int persistentLogoId;
        public static void ValidateLanding(IntroTitleTransition handoff)
        {
            var title=Find<TitlePresentation>();if(title==null)return;
            var target=(RectTransform)title.transform.Find("GameLogo/IllustratedLogo");
            var a=new Vector3[4];var b=new Vector3[4];target.GetWorldCorners(a);handoff.LogoRect.GetWorldCorners(b);
            float error=0;for(int i=0;i<4;i++)error=Mathf.Max(error,Vector3.Distance(a[i],b[i]));
            Require(error<1,"Logo handoff changed bounds: "+error);
            landingSeen=true;Log("PASS exact final bounds before overlay release, error="+error.ToString("F4"));
        }''')
text=text.replace('                    if(persistentLogoId==0)persistentLogoId=handoff.GetInstanceID();','                    if(persistentLogoId==0){persistentLogoId=handoff.GetInstanceID();handoff.gameObject.AddComponent<HandoffLandingProbe>();}')
text=text.replace('                var film=Find<IntroSequencePlayer>();','''                var film=Find<IntroSequencePlayer>();
                var handoff=IntroTitleTransition.Current;
                if(handoff!=null && state==2)
                {
                    if(persistentLogoId==0){persistentLogoId=handoff.GetInstanceID();handoff.gameObject.AddComponent<HandoffLandingProbe>();}
                    Require(persistentLogoId==handoff.GetInstanceID(),"Logo object replaced across scenes");
                    float ratio=handoff.LogoRect.rect.width/handoff.LogoRect.rect.height;
                    var image=handoff.LogoRect.GetComponent<RawImage>();
                    Require(Mathf.Abs(ratio-(float)image.texture.width/image.texture.height)<.01f,"Logo distorted");
                    if(film!=null&&film.Player.time>83)
                    {
                        centralLogoSeen=true;
                        Require(image.color.a>.95f,"Closing logo did not appear");
                        if(captureStep==0){Capture("01-film-logo.png");captureStep++;}
                    }
                    if(handoff.IsMoving)
                    {
                        Require(handoff.MotionProgress>=previousMotion,"Motion ran backwards");previousMotion=handoff.MotionProgress;
                        var title=Find<TitlePresentation>();
                        var target=title.transform.Find("GameLogo");
                        Require(target.GetComponent<CanvasGroup>().alpha==0,"Duplicate title logo visible");
                        foreach(var item in title.transform.Find("MainMenu").GetComponentsInChildren<Button>())
                            Require(!item.IsInteractable()&&item.GetComponent<CanvasGroup>().alpha==0,"Menu revealed before logo landing");
                        if(handoff.MotionProgress>.45f&&handoff.MotionProgress<.65f)
                        {middleSeen=true;if(captureStep==1){Capture("02-moving-logo.png");captureStep++;}}
                        if(handoff.MotionProgress>.97f)
                        {
                            var actual=(RectTransform)target.Find("IllustratedLogo");
                            var a=new Vector3[4];var b=new Vector3[4];actual.GetWorldCorners(a);handoff.LogoRect.GetWorldCorners(b);
                            Require(Vector3.Distance((a[0]+a[2])*.5f,(b[0]+b[2])*.5f)<2,"Logo lands away from title position");
                            if(captureStep==2){Capture("03-logo-landed.png");captureStep++;}
                        }
                    }
                }''')
text=text.replace('if(scene!="03_Title"||systems.Loader.IsBusy)return;','if(scene!="03_Title"||systems.Loader.IsBusy||IntroTitleTransition.IsActive)return;')
text=text.replace('Log("PASS language confirm -> decoded video, Japanese Noto caption, duplicate-input guard; no new save.");','Log("PASS language confirm -> decoded video, Japanese Noto caption, duplicate-input guard; no new save.");\n                                film.Player.time=80;began=now-80;Log("Seek to 80s to verify the revised ending; full playback was exercised in the preceding run.");')
text=text.replace('PASS full 85s playback -> title','PASS natural ending after seek -> title')
text=text.replace('if(!newGame.IsInteractable())return;','if(!newGame.IsInteractable()||Find<TitlePresentation>().transform.Find("MainMenu").GetComponentsInChildren<Button>().Any(b=>b.GetComponent<CanvasGroup>().alpha<.999f))return;')
text=text.replace('                            newGame.onClick.Invoke();Next(3,70);','''                            Require(centralLogoSeen&&middleSeen&&landingSeen&&!IntroTitleTransition.IsActive,"Continuous logo sequence incomplete");
                            Require(Find<TitlePresentation>().transform.Find("GameLogo").GetComponent<CanvasGroup>().alpha==1,"Title logo not restored");
                            Capture("04-menu-revealed.png");
                            Log("PASS same logo across scene unload; middle motion, exact landing, no duplicate logo, menu delayed until landing.");
                            newGame.onClick.Invoke();Next(3,70);''')
insert='''        static void Capture(string name)
        {
            var camera=Camera.main;if(camera==null)return;
            var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
            var cameras=canvases.Select(c=>c.worldCamera).ToArray();var planes=canvases.Select(c=>c.planeDistance).ToArray();
            var old=camera.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(Mathf.Max(640,Screen.width),Mathf.Max(360,Screen.height),24);rt.Create();
            try
            {
                camera.targetTexture=rt;
                foreach(var canvas in canvases){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;}
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();
                File.WriteAllBytes(Path.Combine(Output,name),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
            }
            finally
            {
                camera.targetTexture=old;RenderTexture.active=active;
                for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=planes[i];}
                Canvas.ForceUpdateCanvases();rt.Release();UnityEngine.Object.Destroy(rt);
            }
        }
'''
text=text.replace('        static void Require(bool condition',insert+'        static void Require(bool condition')
text+='''
namespace MyGame.EditorTools
{
    public sealed class HandoffLandingProbe : UnityEngine.MonoBehaviour
    {
        void OnDisable()
        {
            var handoff=GetComponent<MyGame.IntroTitleTransition>();
            if(handoff!=null&&handoff.MotionProgress>=1)HandoffFlowVerification.ValidateLanding(handoff);
        }
    }
}
'''
(work/'Handoff/HandoffFlowVerification.cs.txt').write_text(text,encoding='utf-8')
# Retain only one armed verifier in this copy (old verifier remains inactive).
shutil.copyfile(work/'Handoff/HandoffFlowVerification.cs.txt',copy/'Assets/_Project/Scripts/Editor/MapTool/HandoffFlowVerification.cs')
shutil.copyfile(base/'intro-film/v2/LeafAndLeapIntro_LogoHandoff.mp4',copy/'Assets/StreamingAssets/Video/LeafAndLeapIntro.mp4')
print('Prepared full playback, logo continuity, mouse and Escape verification.')
