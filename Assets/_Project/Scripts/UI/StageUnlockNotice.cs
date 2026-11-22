using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace MyGame
{
    /// <summary>Unlock celebration followed by an screen click or confirm input; dismissing never enters a stage.</summary>
    public sealed class StageUnlockNotice:MonoBehaviour,IPointerClickHandler
    {
        public const float Duration=3.6f; // Kept for preview callers; no longer an auto-close timer.
        public const float InputDelay=.8f, ExitDuration=.35f;
        readonly Queue<WorldMapNodeData> queue=new Queue<WorldMapNodeData>();
        CanvasGroup group,numberGroup,copyGroup;
        RectTransform content,host;
        LocalizedText number,title,stage,hint;
        StageUnlockGraphic art;
        float elapsed,exitElapsed;
        bool closing;
        public bool IsPlaying {get;private set;}
        public bool Suspended {get;set;}
        public bool CanDismiss=>IsPlaying&&!closing&&!Suspended&&elapsed>=InputDelay;
        public static StageUnlockNotice Create(Transform parent)
        {
            var rect=UiKit.Rect("StageUnlockNotice",parent);GoldUiTheme.Stretch(rect);
            var view=UiKit.Ensure<StageUnlockNotice>(rect);view.Build();view.gameObject.SetActive(false);return view;
        }
        void Build()
        {
            if(group!=null)return;
            host=(RectTransform)transform;
            group=gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=true;
            var scrim=gameObject.AddComponent<Image>();scrim.color=new Color(.019f,.019f,.021f,.97f);scrim.raycastTarget=true;
            content=UiKit.Rect("UnlockComposition",transform);
            var graphic=UiKit.Rect("GoldSeal",content);GoldUiTheme.Stretch(graphic);art=UiKit.Ensure<StageUnlockGraphic>(graphic);art.raycastTarget=false;
            number=UiKit.Label(content,"StageNumber",null,74,TextAnchor.MiddleCenter);numberGroup=number.gameObject.AddComponent<CanvasGroup>();
            var copy=UiKit.Rect("Copy",content);GoldUiTheme.Stretch(copy);copyGroup=copy.gameObject.AddComponent<CanvasGroup>();
            title=UiKit.Label(copy,"Heading","worldmap.unlocked_msg",38,TextAnchor.MiddleCenter);
            stage=UiKit.Label(copy,"StageName",null,30,TextAnchor.MiddleCenter);
            hint=UiKit.Label(copy,"ContinueHint","worldmap.unlock_continue",22,TextAnchor.MiddleCenter);
            Layout();
        }
        public void Show(IEnumerable<WorldMapNodeData> nodes)
        {
            Build();queue.Clear();var ids=new HashSet<string>();
            foreach(var node in nodes)if(node!=null&&node.Stage!=null&&ids.Add(node.NodeId))queue.Enqueue(node);
            if(queue.Count==0){Hide();return;}
            gameObject.SetActive(true);transform.SetAsLastSibling();BeginNext();
        }
        void BeginNext()
        {
            var node=queue.Dequeue();number.SetText(StageUnlocks.DisplayNumber(node.Stage.StageId));
            stage.SetKey(node.Stage.DisplayNameKey);elapsed=0;exitElapsed=0;closing=false;IsPlaying=true;Sample(0);Sfx.Play(SfxId.Special);
        }
        void Layout()
        {
            UiKit.Fit(content,host,1040,670);
            UiKit.Place(number.transform,.5f,.5f,0,66,230,120);
            UiKit.Place(title.transform,.5f,.5f,0,-112,940,60);
            UiKit.Place(stage.transform,.5f,.5f,0,-171,900,50);
            UiKit.Place(hint.transform,.5f,.5f,0,-255,940,40);
            number.GetComponent<Text>().color=GoldUiTheme.Cream;title.GetComponent<Text>().color=GoldUiTheme.Cream;
            stage.GetComponent<Text>().color=new Color(.93f,.78f,.49f);hint.GetComponent<Text>().color=new Color(.60f,.62f,.62f);
        }
        void Update()=>Tick(Time.unscaledDeltaTime);
        public void Tick(float delta)
        {
            if(!IsPlaying)return;
            if(Suspended)return;
            delta=Mathf.Max(0,delta);elapsed+=delta;
            if(closing)
            {
                exitElapsed+=delta;
                if(exitElapsed>=ExitDuration){if(queue.Count>0)BeginNext();else Hide();return;}
            }
            Sample(elapsed);
        }
        public void Sample(float seconds)
        {
            Layout();float fade=Mathf.SmoothStep(0,1,seconds/.20f)*(closing?1-Mathf.SmoothStep(0,1,exitElapsed/ExitDuration):1);
            group.alpha=fade;art.Sample(seconds);
            numberGroup.alpha=Mathf.SmoothStep(0,1,(seconds-.33f)/.40f);
            float reveal=Mathf.SmoothStep(0,1,(seconds-.55f)/.40f);copyGroup.alpha=reveal;
            number.transform.localScale=Vector3.one*(1+.12f*(1-Mathf.SmoothStep(0,1,(seconds-.30f)/.55f)));
        }
        public void Dismiss(){if(!CanDismiss)return;closing=true;exitElapsed=0;}
        public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)Dismiss();}
        void Hide(){IsPlaying=false;queue.Clear();gameObject.SetActive(false);}
        void OnDisable(){IsPlaying=false;queue.Clear();}
    }
}
