using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Common responsive panel sizes; preserves scene bindings and navigation.</summary>
    [DisallowMultipleComponent]
    public sealed class UiScreenLayout:MonoBehaviour
    {
        private bool ready;
        public static void Apply(Transform root)
        {
            var view=UiKit.Ensure<UiScreenLayout>(root);view.ready=true;view.Layout();
        }
        private void OnRectTransformDimensionsChange(){if(ready)Layout();}
        private void LateUpdate()
        {
            if(name!="HUDCanvas")return;
            var plate=transform.Find("StatusBanner");var label=plate!=null?plate.Find("MessageText"):null;
            if(label!=null&&plate.gameObject.activeSelf!=label.gameObject.activeSelf)plate.gameObject.SetActive(label.gameObject.activeSelf);
        }
        public void Layout()
        {
            if(!ready)return;
            if(name=="LanguageCanvas")Language();
            else if(name=="HUDCanvas")Gameplay();
            else if(GetComponent<ConfirmPopup>()!=null)Confirm();
            else if(name=="LoadingCanvas")Loading();
            else if(name=="TitleCanvas")Title();
        }
        private void Language()
        {
            var group=UiKit.Rect("LanguageContent",transform);
            foreach(string n in new[]{"Header","LanguageList","ConfirmButton","ErrorText"})
            {var child=transform.Find(n);if(child!=null)child.SetParent(group,false);}
            UiKit.Fit(group,(RectTransform)transform,820,820);
            GoldUiTheme.DressPanel(group);
            UiKit.Place(group.Find("Header"),.5f,1,0,-24,760,90);UiKit.TextStyle(group.Find("Header"),UiKit.HeadingSize,TextAnchor.MiddleCenter);
            var list=group.Find("LanguageList");UiKit.Place(list,.5f,1,0,-145,740,440);
            list.Find("GoldLinework")?.gameObject.SetActive(false);
            int i=0;foreach(var button in list.GetComponentsInChildren<Button>(true))
            {UiKit.Place(button.transform,.5f,1,0,-i++*82,680,68);UiKit.ButtonStyle(button);}
            list.GetComponent<GoldUiReveal>()?.RefreshRestPose();
            UiKit.Place(group.Find("ErrorText"),.5f,1,0,-600,740,60);UiKit.TextStyle(group.Find("ErrorText"),24,TextAnchor.MiddleCenter);
            group.Find("ErrorText").GetComponent<Text>().color=UiKit.Warning;
            UiKit.Place(group.Find("ConfirmButton"),.5f,1,0,-696,460,76);UiKit.ButtonStyle(group.Find("ConfirmButton").GetComponent<Button>(),true);
        }
        private void Gameplay()
        {
            var pause=transform.Find("PauseMenu/Window") as RectTransform;
            if(pause!=null)
            {
                UiKit.Fit(pause,(RectTransform)transform,660,610);
                UiKit.Place(pause.Find("Title"),.5f,1,0,-32,540,64);UiKit.TextStyle(pause.Find("Title"),UiKit.HeadingSize,TextAnchor.MiddleCenter);
                string[] names={"Button_resume","Button_retry","Button_settings","Button_exit_map"};
                for(int i=0;i<names.Length;i++){var b=pause.Find(names[i]);UiKit.Place(b,.5f,1,0,-126-i*88,520,72);UiKit.ButtonStyle(b?.GetComponent<Button>(),i==0);}
                var note=UiKit.Label(pause,"PauseNote","ui.pause_note",UiKit.CaptionSize,TextAnchor.MiddleCenter);
                UiKit.Place(note.transform,.5f,0,0,20,550,76);note.GetComponent<Text>().color=UiKit.Muted;
            }
            var over=transform.Find("GameOverPanel/Window") as RectTransform;
            if(over!=null)
            {
                UiKit.Fit(over,(RectTransform)transform,720,510);
                UiKit.Place(over.Find("Title"),.5f,1,0,-42,620,76);UiKit.TextStyle(over.Find("Title"),UiKit.HeadingSize,TextAnchor.MiddleCenter);
                var note=UiKit.Label(over,"RecoveryNote","ui.recovery_note",UiKit.BodySize,TextAnchor.MiddleCenter);
                UiKit.Place(note.transform,.5f,1,0,-130,620,60);
                // Retry / World map / Title, top to bottom; Retry is the primary choice.
                string[] names={"RetryButton","ContinueButton","TitleButton"};
                string[] keys={"pause.retry","pause.exit_map","pause.to_title"};
                var buttons=new System.Collections.Generic.List<Selectable>();
                for(int i=0;i<names.Length;i++)
                {
                    var b=over.Find(names[i]);if(b==null)continue;
                    UiKit.Key(b,keys[i]);UiKit.Place(b,.5f,1,0,-212-i*94,520,76);UiKit.ButtonStyle(b.GetComponent<Button>(),i==0);
                    buttons.Add(b.GetComponent<Button>());
                }
                if(buttons.Count>1)UiKit.Link(buttons.ToArray());
            }
            var message=transform.Find("MessageText")??transform.Find("StatusBanner/MessageText");
            if(message!=null)
            {
                var plate=UiKit.Rect("StatusBanner",transform);UiKit.Place(plate,.5f,.5f,0,90,720,110);
                var image=UiKit.Ensure<Image>(plate);image.color=GoldUiTheme.Panel;image.raycastTarget=false;
                GoldUiTheme.DressPanel(plate,false);
                if(message.parent!=plate)message.SetParent(plate,false);
                GoldUiTheme.Stretch((RectTransform)message);UiKit.TextStyle(message,52,TextAnchor.MiddleCenter);
                plate.gameObject.SetActive(message.gameObject.activeSelf);
            }
        }
        private void Confirm()
        {
            var window=transform.Find("Window") as RectTransform;if(window==null)return;
            UiKit.Fit(window,(RectTransform)transform,1040,420);
            UiKit.Place(window.Find("Message"),.5f,1,0,-36,940,210);UiKit.TextStyle(window.Find("Message"),32,TextAnchor.MiddleCenter);
            UiKit.Place(window.Find("YesButton"),.5f,0,-240,42,460,90);
            UiKit.Place(window.Find("NoButton"),.5f,0,240,42,460,90);
            UiKit.ButtonStyle(window.Find("YesButton").GetComponent<Button>());
            UiKit.ButtonStyle(window.Find("NoButton").GetComponent<Button>(),true);
        }
        private void Loading()
        {
            LoadingPresentation.Apply(transform);
        }
        private void Title()
        {
            var menu=transform.Find("MainMenu");if(menu==null)return;
            foreach(var b in menu.GetComponentsInChildren<Button>(true))
            {
                var label=b.transform.Find("Label")?.GetComponent<Text>();
                if(label==null)continue;
                // Peer actions share the same design size; only overflowing translations shrink.
                UiKit.TextStyle(label.transform,UiKit.ActionSize,TextAnchor.MiddleCenter);
                if(b.targetGraphic is GoldUiInlay)label.color=GoldUiTheme.PrimaryInk;
                label.resizeTextForBestFit=false;
                label.fontStyle=FontStyle.Normal;
                label.horizontalOverflow=HorizontalWrapMode.Overflow;
                label.verticalOverflow=VerticalWrapMode.Truncate;
                var rect=label.rectTransform;
                GoldUiTheme.Stretch(rect);
                rect.offsetMin=new Vector2(24,8);rect.offsetMax=new Vector2(-24,-8);
                rect.localScale=Vector3.one;
                LocalizedTextFit.Attach(label,UiKit.ActionSize);
                b.transform.Find("ResumeStage")?.gameObject.SetActive(false);
            }
            var notice=transform.Find("NoticeText");if(notice!=null){UiKit.TextStyle(notice,28,TextAnchor.MiddleCenter);notice.GetComponent<Text>().color=UiKit.Warning;}
        }
    }
}
