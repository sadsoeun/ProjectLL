using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    /// <summary>Centered loading composition. Scene loading and progress values remain controller-owned.</summary>
    [DisallowMultipleComponent]
    public sealed class LoadingPresentation:MonoBehaviour
    {
        RectTransform host,content,icon,bar,logo;
        RawImage landscape,logoArt;
        LoadingTrailGraphic trail;
        LoadingEmblemGraphic emblem;
        Text percent;
        bool initialized;
        float elapsed,target,display;
        public static LoadingPresentation Apply(Transform root)
        {
            var view=UiKit.Ensure<LoadingPresentation>(root);view.Initialize();view.Layout();return view;
        }
        void Initialize()
        {
            if(initialized)return;initialized=true;host=(RectTransform)transform;
            content=UiKit.Rect("LoadingContent",transform);
            foreach(string name in new[]{"LoadingIcon","LoadingLabel","TipText","ProgressBar","Destination"})
            {var child=transform.Find(name);if(child!=null)child.SetParent(content,false);}
            transform.Find("Background")?.gameObject.SetActive(false);
            var back=UiKit.Rect("LoadingLandscape",transform);GoldUiTheme.Stretch(back);back.SetAsFirstSibling();
            landscape=UiKit.Ensure<RawImage>(back);landscape.texture=Resources.Load<Texture2D>("Brand/LoadingMeadowPrint");
            landscape.color=Color.white;landscape.raycastTarget=false;
            var shade=UiKit.Rect("LoadingShade",back);GoldUiTheme.Stretch(shade);UiKit.Ensure<LoadingShadeGraphic>(shade).raycastTarget=false;
            logo=UiKit.Rect("LoadingBrand",content);logoArt=UiKit.Ensure<RawImage>(logo);logoArt.texture=Resources.Load<Texture2D>("TitleArt/Logo");logoArt.raycastTarget=false;
            UiKit.Label(content,"Destination",null,42,TextAnchor.MiddleCenter);
            bar=(RectTransform)content.Find("ProgressBar");icon=(RectTransform)content.Find("LoadingIcon");
            // Keep the serialized objects/bindings, while replacing the old square frame and thick bar.
            foreach(var image in bar.GetComponentsInChildren<Image>(true))image.enabled=false;
            foreach(var image in icon.GetComponentsInChildren<Image>(true))image.enabled=false;
            bar.Find("GoldLinework")?.gameObject.SetActive(false);icon.Find("GoldLinework")?.gameObject.SetActive(false);
            var line=UiKit.Rect("GoldenTrail",bar);GoldUiTheme.Stretch(line);trail=UiKit.Ensure<LoadingTrailGraphic>(line);trail.raycastTarget=false;
            var badge=UiKit.Rect("LeafEmblem",icon);GoldUiTheme.Stretch(badge);emblem=UiKit.Ensure<LoadingEmblemGraphic>(badge);emblem.raycastTarget=false;
            percent=UiKit.Label(content,"ProgressPercent",null,24,TextAnchor.MiddleRight).GetComponent<Text>();
            icon.SetAsLastSibling();
        }
        public void Layout()
        {
            if(!initialized)return;
            // Both anchor and pivot are centered; no inherited top/bottom origin survives reparenting.
            UiKit.Fit(content,host,1160,560);
            Put(logo,0,162,450,logoArt.texture!=null?450f*logoArt.texture.height/logoArt.texture.width:130);
            Put(content.Find("Destination"),0,32,1020,72);Style("Destination",42,GoldUiTheme.Cream);
            Put(content.Find("LoadingLabel"),0,-28,1000,44);Style("LoadingLabel",24,new Color(.80f,.78f,.70f));
            Put(bar,0,-100,960,76);
            Put(percent.transform,450,-154,120,36);percent.color=new Color(.94f,.83f,.58f);
            Put(content.Find("TipText"),0,-210,1000,72);Style("TipText",24,GoldUiTheme.Muted);
            FitBackground();Sample(elapsed,display);
        }
        void FitBackground()
        {
            if(landscape.texture==null||host.rect.height<=0)return;
            float frame=host.rect.width/host.rect.height,source=(float)landscape.texture.width/landscape.texture.height;
            if(frame>source){float h=source/frame;landscape.uvRect=new Rect(0,(1-h)*.5f,1,h);}
            else{float w=frame/source;landscape.uvRect=new Rect((1-w)*.5f,0,w,1);}
        }
        void Style(string name,int size,Color color)
        {var t=content.Find(name);UiKit.TextStyle(t,size,TextAnchor.MiddleCenter);t.GetComponent<Text>().color=color;}
        static void Put(Transform t,float x,float y,float w,float h)=>UiKit.Place(t,.5f,.5f,x,y,w,h);
        public void SetProgress(float value)
        {
            target=float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Clamp01(value);
            if(target==0||target==1)display=target;
            Sample(elapsed,display);
        }
        public void Tick(float delta)
        {elapsed+=Mathf.Max(0,delta);display=Mathf.MoveTowards(display,target,Mathf.Max(0,delta)*1.5f);Sample(elapsed,display);}
        public void Sample(float seconds,float progress)
        {
            if(!initialized)return;float p=Mathf.Clamp01(progress);
            trail.Sample(seconds,p);emblem.Sample(seconds);
            Put(icon,Mathf.Lerp(-480,480,p),-100,74,74);icon.localRotation=Quaternion.identity;
            percent.text=Mathf.RoundToInt(p*100)+"%";
        }
    }
}
