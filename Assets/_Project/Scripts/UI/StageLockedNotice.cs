using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Non-blocking lock feedback. Never takes focus or changes stage progress.</summary>
    public sealed class StageLockedNotice : MonoBehaviour
    {
        public const float Duration=3.2f;
        RectTransform host,card,icon;
        CanvasGroup group;
        GoldUiFrame frame;
        LocalizedText heading,detail;
        AudioClip cue;
        float elapsed;
        public bool IsPlaying {get;private set;}
        public static StageLockedNotice Create(Transform parent)
        {
            var root=UiKit.Rect("StageLockedNotice",parent);GoldUiTheme.Stretch(root);
            var view=root.gameObject.AddComponent<StageLockedNotice>();view.Build();view.Hide();return view;
        }
        void Build()
        {
            host=(RectTransform)transform;
            group=gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;
            card=UiKit.Rect("LockBanner",host);
            var bg=card.gameObject.AddComponent<Image>();bg.color=GoldUiTheme.Panel;bg.raycastTarget=false;
            var border=UiKit.Rect("GoldBorder",card);GoldUiTheme.Stretch(border);
            frame=border.gameObject.AddComponent<GoldUiFrame>();frame.color=GoldUiTheme.Gold;frame.raycastTarget=false;frame.diamond=false;
            icon=UiKit.Rect("LockIcon",card);icon.gameObject.AddComponent<StageLockGraphic>().raycastTarget=false;
            heading=UiKit.Label(card,"Heading","worldmap.locked_msg",36,TextAnchor.MiddleLeft);
            detail=UiKit.Label(card,"Requirement",null,26,TextAnchor.MiddleLeft);
            cue=Resources.Load<AudioClip>("Audio/UiStageLocked");
        }
        public static string MissingStageNumbers(WorldMapNodeData node,SaveData save)
        {
            var missing=new List<string>();
            if(node!=null)foreach(var id in node.PrerequisiteStageIds)
            {
                if(string.IsNullOrEmpty(id))continue;
                var record=save?.FindStage(id);
                if(record!=null&&record.cleared)continue;
                var number=StageUnlocks.DisplayNumber(id);if(!missing.Contains(number))missing.Add(number);
            }
            return string.Join(", ",missing);
        }
        public bool Show(WorldMapNodeData node,SaveData save,bool disconnected=false)
        {
            if(IsPlaying&&elapsed<.65f)return false;
            gameObject.SetActive(true);transform.SetAsLastSibling();
            string missing=MissingStageNumbers(node,save);
            heading.SetKey(disconnected?"worldmap.route_locked":"worldmap.locked_msg");
            if(!disconnected&&!string.IsNullOrEmpty(missing))detail.SetKey("worldmap.locked_requires",missing);
            else detail.SetKey("worldmap.locked_route_hint");
            elapsed=0;IsPlaying=true;Sample(0);
            Sfx.Play(cue,.52f,SfxId.UiClick);
            return true;
        }
        void Update()=>Tick(Time.unscaledDeltaTime);
        public void Tick(float delta)
        {
            if(!IsPlaying)return;
            elapsed+=Mathf.Max(0,delta);
            if(elapsed>=Duration){Hide();return;}
            Sample(elapsed);
        }
        public void Sample(float seconds)
        {
            float fit=Mathf.Clamp(Mathf.Min((host.rect.width-64)/940f,(host.rect.height-64)/400f),.1f,1f);
            float enter=1-Mathf.Pow(1-Mathf.Clamp01(seconds/.28f),3);
            float exit=Mathf.SmoothStep(0,1,(seconds-(Duration-.4f))/.4f);
            UiKit.Place(card,.5f,1,0,(-132+18*(1-enter))*fit,940,160);card.localScale=Vector3.one*fit;
            group.alpha=enter*(1-exit);
            UiKit.Place(icon,.5f,.5f,-396,0,76,76);
            icon.anchoredPosition+=Vector2.right*(Mathf.Sin(seconds*55)*5*Mathf.Clamp01(1-seconds/.38f));
            UiKit.Place(heading.transform,.5f,.5f,48,27,740,56);
            UiKit.Place(detail.transform,.5f,.5f,48,-32,740,42);
            float accent=Mathf.Clamp01(1-seconds/.65f);
            heading.transform.localScale=Vector3.one*(1+.025f*accent);
            heading.GetComponent<Text>().color=Color.Lerp(GoldUiTheme.Cream,Color.white,accent*.65f);
            detail.GetComponent<Text>().color=new Color(.76f,.72f,.62f);
            frame.SetMotion(Mathf.Sin(Mathf.Clamp01(seconds/.8f)*Mathf.PI)*.7f,Mathf.Clamp01(seconds/.8f),0);
        }
        public void Hide(){IsPlaying=false;gameObject.SetActive(false);}
        void OnDisable(){IsPlaying=false;}
    }
}
