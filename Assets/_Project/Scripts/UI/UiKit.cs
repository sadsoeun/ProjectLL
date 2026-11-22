using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    public static class UiKit
    {
        public const int HeadingSize=40, ActionSize=40, BodySize=28, CaptionSize=24, HudSize=32;
        public static readonly Color Muted = GoldUiTheme.Muted;
        public static readonly Color Warning = new Color(1f,.64f,.47f);
        public static RectTransform Rect(string name, Transform parent) => parent.Find(name) as RectTransform ?? GoldUiTheme.Rect(name,parent);
        public static T Ensure<T>(Component c) where T:Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
        public static void Place(Transform t,float ax,float ay,float x,float y,float w,float h)
        { if(t!=null) GoldUiTheme.Place((RectTransform)t,new Vector2(ax,ay),new Vector2(ax,ay),new Vector2(x,y),new Vector2(w,h)); }
        public static void TextStyle(Transform t,int size,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            if(t==null)return; var text=t.GetComponent<Text>(); if(text==null)return;
            text.fontSize=size; text.resizeTextForBestFit=false;text.fontStyle=FontStyle.Normal; text.resizeTextMinSize=Mathf.Min(22,size);text.resizeTextMaxSize=size;
            text.alignment=anchor;text.color=GoldUiTheme.Cream;text.raycastTarget=false;
            LocalizedTextFit.Attach(text,size);
        }
        public static LocalizedText Label(Transform parent,string name,string key,int size=26,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            var rt=Rect(name,parent);Ensure<Text>(rt);var loc=Ensure<LocalizedText>(rt);
            if(key!=null)loc.SetKey(key);TextStyle(rt,size,anchor);return loc;
        }
        public static Button Button(Transform parent,string name,string key)
        {
            var rt=Rect(name,parent);var image=Ensure<Image>(rt);var button=Ensure<Button>(rt);button.targetGraphic=image;
            var label=Label(rt,"Label",key,30,TextAnchor.MiddleCenter);GoldUiTheme.Stretch((RectTransform)label.transform);
            ButtonStyle(button);return button;
        }
        public static void ButtonStyle(Button button,bool primary=false)
        {
            if(button==null)return;GoldUiTheme.StyleButton(button,primary);
            var label=button.GetComponentInChildren<Text>(true);if(label!=null)TextStyle(label.transform,ActionSize,TextAnchor.MiddleCenter);
            if(primary && label!=null)label.color=GoldUiTheme.PrimaryInk;
        }
        public static void Fit(RectTransform panel,RectTransform host,float width,float height)
        {
            Place(panel,.5f,.5f,0,0,width,height);
            var bounds=host.rect;
            float fit=bounds.width>1&&bounds.height>1?Mathf.Min(1,(bounds.width-64)/width,(bounds.height-64)/height):1;
            panel.localScale=Vector3.one*Mathf.Max(.1f,fit);
            panel.GetComponent<GoldUiReveal>()?.RefreshRestPose();
        }
        public static void Link(params Selectable[] items)
        {
            for(int i=0;i<items.Length;i++)items[i].navigation=new Navigation{mode=Navigation.Mode.Explicit,
                selectOnUp=items[(i+items.Length-1)%items.Length],selectOnDown=items[(i+1)%items.Length]};
        }
        public static void Key(Transform target,string key)
        { if(target!=null)target.GetComponentInChildren<LocalizedText>(true)?.SetKey(key); }
    }
}
