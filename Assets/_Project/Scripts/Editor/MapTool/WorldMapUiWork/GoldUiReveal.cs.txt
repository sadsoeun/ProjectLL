using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    /// <summary>Panel settle and staggered controls in unscaled time, including while paused.</summary>
    [DisallowMultipleComponent]
    public sealed class GoldUiReveal : MonoBehaviour
    {
        public const float Duration=.42f;
        private RectTransform rect;
        private CanvasGroup group;
        private Vector2 position;
        private Vector3 scale;
        private readonly List<CanvasGroup> rows=new List<CanvasGroup>();
        private float elapsed;
        private bool initialized,exiting;
        public void Initialize()
        {
            if(initialized)return;initialized=true;
            rect=(RectTransform)transform;position=rect.anchoredPosition;scale=rect.localScale;
            group=GetComponent<CanvasGroup>();if(group==null)group=gameObject.AddComponent<CanvasGroup>();
            foreach(Transform child in transform)
            {
                if(child.GetComponent<Selectable>()==null)continue;
                var row=child.GetComponent<CanvasGroup>();if(row==null)row=child.gameObject.AddComponent<CanvasGroup>();rows.Add(row);
            }
        }
        public void RefreshRestPose()
        {
            Initialize(); position=rect.anchoredPosition; scale=rect.localScale;
        }
        private void OnEnable(){Initialize();elapsed=0;exiting=false;Sample(0);}
        private void Update(){if(exiting||elapsed>=Duration)return;elapsed+=Time.unscaledDeltaTime;Sample(elapsed);}
        private void OnDisable()
        {
            if(!initialized)return;
            rect.anchoredPosition=position;rect.localScale=scale;group.alpha=1;group.interactable=true;
            foreach(var row in rows)if(row!=null)row.alpha=1;
        }
        public void Sample(float seconds)
        {
            Initialize();float t=Mathf.Clamp01(seconds/.3f),ease=1-Mathf.Pow(1-t,3);
            group.alpha=Mathf.SmoothStep(0,1,t);group.interactable=seconds>=Duration;
            rect.anchoredPosition=position+new Vector2(0,-18*(1-ease));rect.localScale=scale*Mathf.Lerp(.975f,1,ease);
            for(int i=0;i<rows.Count;i++)rows[i].alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((seconds-Mathf.Min(i,8)*.022f)/.24f));
        }
        public void SampleExit(float progress)
        {
            Initialize();exiting=true;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(progress));
            rect.anchoredPosition=position+new Vector2(0,-10*t);rect.localScale=scale*(1-.015f*t);group.interactable=false;
        }
    }
}
