using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Unscaled focus glint and click feedback; never changes hitboxes or navigation.</summary>
    [DisallowMultipleComponent]
    public sealed class GoldUiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler, ISubmitHandler, IPointerClickHandler
    {
        private Button button;
        private GoldUiFrame frame;
        private Text label;
        private Vector3 labelScale;
        private bool hovered, selected, marked, pressed, wasFocused, initialized;
        private float focus, press, sweep=1, submitPulse;
        public Color RestInk { get; set; } = GoldUiTheme.Cream;
        public Color FocusInk { get; set; } = GoldUiTheme.Gold;
        private void Bind()
        {
            if(button==null)button=GetComponent<Button>();
            if(frame==null)frame=GetComponentInChildren<GoldUiFrame>(true);
            if(label==null)label=GetComponentInChildren<Text>(true);
            if(!initialized&&label!=null){labelScale=label.rectTransform.localScale;initialized=true;}
        }
        public void Mark(bool value){marked=value;Tick(0);}
        private void OnEnable(){Bind();Tick(0);}
        private void OnDisable()
        {
            hovered=selected=pressed=wasFocused=false;focus=press=submitPulse=0;sweep=1;
            if(label!=null)label.rectTransform.localScale=labelScale;
            if(frame!=null)frame.SetMotion(0,1,0);
        }
        public void OnPointerEnter(PointerEventData e){hovered=true;}
        public void OnPointerExit(PointerEventData e){hovered=false;pressed=false;}
        public void OnSelect(BaseEventData e){selected=true;}
        public void OnDeselect(BaseEventData e){selected=false;pressed=false;}
        public void OnPointerDown(PointerEventData e){if(e.button==PointerEventData.InputButton.Left)pressed=true;}
        public void OnPointerUp(PointerEventData e){pressed=false;}
        public void OnSubmit(BaseEventData e){if(button!=null&&button.IsInteractable()){submitPulse=.12f;Sfx.Play(SfxId.UiClick);}}
        public void OnPointerClick(PointerEventData e){if(e.button==PointerEventData.InputButton.Left&&button!=null&&button.IsInteractable())Sfx.Play(SfxId.UiClick);}
        private void Update()=>Tick(Time.unscaledDeltaTime);
        public void Tick(float delta)
        {
            Bind();if(button==null)return;
            bool usable=button.IsInteractable(),focused=usable&&(hovered||selected||marked);
            if(focused&&!wasFocused)sweep=0;wasFocused=focused;sweep=Mathf.Min(1,sweep+delta/.42f);
            submitPulse=Mathf.Max(0,submitPulse-delta);
            focus=Mathf.MoveTowards(focus,focused?1:0,delta/.16f);
            press=Mathf.MoveTowards(press,usable&&(pressed||submitPulse>0)?1:0,delta/.075f);
            if(frame!=null)
            {
                frame.color=new Color(1,.77f,.35f,usable?Mathf.Lerp(.5f,1,focus):.18f);
                frame.SetMotion(focus,sweep,press);
            }
            if(label!=null)
            {
                label.color=usable?Color.Lerp(RestInk,FocusInk,marked?1:focus*.25f):new Color(.53f,.52f,.47f);
                label.rectTransform.localScale=labelScale*(1-.035f*press);
            }
        }
    }
}
