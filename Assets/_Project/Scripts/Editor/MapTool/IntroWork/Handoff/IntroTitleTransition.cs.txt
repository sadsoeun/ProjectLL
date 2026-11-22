using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>One live logo survives the movie scene and lands in the real title layout.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class IntroTitleTransition : MonoBehaviour
    {
        static IntroTitleTransition current;
        public static bool IsActive => current!=null;
        public static IntroTitleTransition Current => current;
        public RectTransform LogoRect => logo.rectTransform;
        public float MotionProgress {get;private set;}
        public bool IsMoving => moving;
        Canvas canvas;
        RectTransform frame;
        RawImage logo, picture;
        Image black;
        RenderTexture snapshot;
        bool handingOff, moving;
        float began, motionBegan, initialAlpha;
        Vector2 centralSize;
        static readonly Vector2 CentralPosition=new Vector2(0,45);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()=>current=null;

        public static IntroTitleTransition Ensure()
        {
            if(current!=null)return current;
            var texture=Resources.Load<Texture2D>("TitleArt/Logo");
            if(texture==null)return null;
            var root=new GameObject("IntroTitleLogo",typeof(RectTransform));
            var transition=root.AddComponent<IntroTitleTransition>();
            current=transition;DontDestroyOnLoad(root);transition.Build(texture);return transition;
        }
        void Build(Texture texture)
        {
            canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=32765; // Above the scene fader; the logo never blinks during loading.
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var backdrop=Rect("TransitionMatte",transform);Stretch(backdrop);
            black=backdrop.gameObject.AddComponent<Image>();black.color=Color.black;black.enabled=false;
            frame=Rect("FilmFrame",transform);frame.sizeDelta=new Vector2(1920,1080);
            var image=Rect("LastFilmFrame",frame);Stretch(image);
            picture=image.gameObject.AddComponent<RawImage>();picture.raycastTarget=false;picture.enabled=false;
            var logoRect=Rect("ContinuousLogo",frame);
            logo=logoRect.gameObject.AddComponent<RawImage>();logo.texture=texture;logo.raycastTarget=false;
            centralSize=new Vector2(1350,1350f*texture.height/texture.width);
            logoRect.sizeDelta=centralSize;logoRect.anchoredPosition=CentralPosition;
            SetLogoAlpha(0);Layout();
        }
        public void SetFilmTime(double time)
        {
            if(handingOff)return;
            SetLogoAlpha(Mathf.SmoothStep(0,1,Mathf.Clamp01((float)(time-81.58)/1.5f)));
        }
        public void BeginHandoff(RenderTexture film)
        {
            if(handingOff)return;
            handingOff=true;began=Time.realtimeSinceStartup;initialAlpha=logo.color.a;
            black.enabled=true;
            if(film!=null&&film.IsCreated())
            {
                snapshot=new RenderTexture(film.width,film.height,0,RenderTextureFormat.ARGB32){name="IntroHandoffFrame"};
                snapshot.Create();Graphics.Blit(film,snapshot);picture.texture=snapshot;picture.enabled=true;
            }
        }
        void LateUpdate()
        {
            Layout();
            if(!handingOff)return;
            float now=Time.realtimeSinceStartup;
            SetLogoAlpha(Mathf.Lerp(initialAlpha,1,Mathf.SmoothStep(0,1,Mathf.Clamp01((now-began)/.35f))));
            if(now-began>18)
            {Debug.LogWarning("[Intro] Title logo handoff timed out; releasing overlay.");Finish();return;}
            if(SceneManager.GetActiveScene().name!=SceneId.Title.ToSceneName())return;
            if(GameSystems.Exists&&GameSystems.Instance.Loader.IsBusy)return;
            if(!TryDestination(out var position,out var size))return;
            if(!moving){moving=true;motionBegan=now;}
            float elapsed=now-motionBegan;
            MotionProgress=Mathf.Clamp01(elapsed/1.3f);
            float t=MotionProgress;
            float ease=t*t*t*(t*(t*6-15)+10); // Smooth start and stop, no bounce.
            logo.rectTransform.anchoredPosition=Vector2.Lerp(CentralPosition,position,ease)+Vector2.up*(Mathf.Sin(ease*Mathf.PI)*22);
            logo.rectTransform.sizeDelta=Vector2.Lerp(centralSize,size,ease);
            float cover=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.85f));
            black.color=new Color(0,0,0,cover);picture.color=new Color(1,1,1,cover);
            if(t>=1)Finish();
        }
        bool TryDestination(out Vector2 position,out Vector2 size)
        {
            position=Vector2.zero;size=Vector2.zero;
            var title=FindFirstObjectByType<TitleIllustration>();if(title==null)return false;
            var destination=title.transform.Find("GameLogo/IllustratedLogo") as RectTransform;
            if(destination==null)return false;
            var corners=new Vector3[4];destination.GetWorldCorners(corners);
            var targetCanvas=destination.GetComponentInParent<Canvas>();
            var camera=targetCanvas.renderMode==RenderMode.ScreenSpaceOverlay?null:targetCanvas.worldCamera;
            var lower=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
            var upper=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,lower,null,out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(frame,upper,null,out var b);
            position=(a+b)*.5f;size=b-a;
            return size.x>0&&size.y>0;
        }
        void Finish()
        {
            current=null;
            var title=FindFirstObjectByType<TitlePresentation>();
            if(title!=null)title.CompleteIntroEntrance();
            gameObject.SetActive(false);Destroy(gameObject);
        }
        void Layout()
        {
            if(frame==null)return;
            var bounds=((RectTransform)transform).rect.size;
            frame.localScale=Vector3.one*Mathf.Min(bounds.x/1920,bounds.y/1080);
        }
        void SetLogoAlpha(float value){var color=Color.white;color.a=value;logo.color=color;}
        void OnDestroy()
        {
            if(current==this)current=null;
            if(snapshot!=null){snapshot.Release();if(Application.isPlaying)Destroy(snapshot);else DestroyImmediate(snapshot);}
        }
        static RectTransform Rect(string name,Transform parent)
        {var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);return r;}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
