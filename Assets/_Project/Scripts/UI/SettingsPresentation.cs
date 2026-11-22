using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyGame
{
    [DisallowMultipleComponent]
    public sealed class SettingsPresentation:MonoBehaviour
    {
        private RectTransform window;
        private Slider master,bgm,sfx;
        private Toggle fullscreen,vsync;
        private Button language,close,resolution;
        private RectTransform resolutionList;
        private GameObject resolutionBlocker;
        private readonly System.Collections.Generic.List<Button> resolutionOptions=new System.Collections.Generic.List<Button>();
        public bool ResolutionListOpen => resolutionList!=null&&resolutionList.gameObject.activeSelf;
        private Button[] languages;
        private Transform[] mainRows;
        private bool initialized;
        public bool LanguagePage { get; private set; }
        public static SettingsPresentation Apply(Transform root)
        {
            var view=UiKit.Ensure<SettingsPresentation>(root);view.Initialize();view.Layout();view.RefreshValues();return view;
        }
        private void Initialize()
        {
            if(initialized)return;window=transform.Find("Window") as RectTransform;if(window==null)return;initialized=true;
            master=window.Find("MasterSlider").GetComponent<Slider>();bgm=window.Find("BgmSlider").GetComponent<Slider>();sfx=window.Find("SfxSlider").GetComponent<Slider>();
            fullscreen=window.Find("FullscreenToggle").GetComponent<Toggle>();vsync=window.Find("VsyncToggle").GetComponent<Toggle>();close=window.Find("CloseButton").GetComponent<Button>();
            language=UiKit.Button(window,"CurrentLanguage","settings.language");language.onClick.AddListener(OpenLanguages);
            // Resolution: a drop-down of the options that fit the display (applied on pick, saved when the popup closes).
            UiKit.Label(window,"ResolutionLabel","settings.resolution",28);
            resolution=UiKit.Button(window,"ResolutionButton",null);resolution.onClick.AddListener(ToggleResolutions);
            var blocker=GoldUiTheme.Rect("ResolutionBlocker",window);GoldUiTheme.Stretch(blocker);resolutionBlocker=blocker.gameObject;
            blocker.gameObject.AddComponent<Image>().color=new Color(0,0,0,.35f); // a click outside the list closes it
            blocker.gameObject.AddComponent<Button>().onClick.AddListener(()=>CloseResolutions());
            blocker.GetComponent<Button>().targetGraphic=null;blocker.GetComponent<Button>().transition=Selectable.Transition.None;blocker.GetComponent<Button>().navigation=new Navigation{mode=Navigation.Mode.None};
            resolutionList=GoldUiTheme.Rect("ResolutionList",window);resolutionList.gameObject.AddComponent<Image>().color=new Color(.05f,.05f,.06f,1f);
            GoldUiTheme.Frame(resolutionList).diamond=false;
            resolutionBlocker.SetActive(false);resolutionList.gameObject.SetActive(false);
            languages=new Button[LocalizationTable.LanguageCodes.Length];
            for(int i=0;i<languages.Length;i++)languages[i]=window.Find("Lang_"+LocalizationTable.LanguageCodes[i]).GetComponent<Button>();
            UiKit.Label(window,"AudioHeading","ui.audio",24);UiKit.Label(window,"DisplayHeading","ui.display",24);
            UiKit.Label(window,"SettingsNote","ui.settings_note",UiKit.CaptionSize,TextAnchor.MiddleCenter);
            foreach(string n in new[]{"Master","Bgm","Sfx"})UiKit.Label(window,n+"Value",null,UiKit.BodySize,TextAnchor.MiddleRight);
            UiKit.Label(window,"FullscreenState",null,UiKit.BodySize,TextAnchor.MiddleRight);UiKit.Label(window,"VsyncState",null,UiKit.BodySize,TextAnchor.MiddleRight);
            mainRows=new[]{window.Find("AudioHeading"),window.Find("DisplayHeading"),window.Find("LanguageLabel"),language.transform,
                window.Find("MasterLabel"),master.transform,window.Find("MasterValue"),window.Find("BgmLabel"),bgm.transform,window.Find("BgmValue"),
                window.Find("SfxLabel"),sfx.transform,window.Find("SfxValue"),fullscreen.transform,vsync.transform,window.Find("FullscreenState"),window.Find("VsyncState"),
                window.Find("ResolutionLabel"),resolution.transform};
            master.onValueChanged.AddListener(_=>RefreshValues());bgm.onValueChanged.AddListener(_=>RefreshValues());sfx.onValueChanged.AddListener(_=>RefreshValues());
            fullscreen.onValueChanged.AddListener(_=>RefreshValues());vsync.onValueChanged.AddListener(_=>RefreshValues());
            SetPage(false);
        }
        public void RefreshValues()
        {
            if(!initialized)return;
            Literal("MasterValue",Mathf.RoundToInt(master.value*100)+"%");Literal("BgmValue",Mathf.RoundToInt(bgm.value*100)+"%");Literal("SfxValue",Mathf.RoundToInt(sfx.value*100)+"%");
            UiKit.Key(window.Find("FullscreenState"),fullscreen.isOn?"ui.on":"ui.off");UiKit.Key(window.Find("VsyncState"),vsync.isOn?"ui.on":"ui.off");
            string res=GameSystems.Exists?GameSystems.Instance.Settings.Data.resolution:"1920x1080";
            resolution.GetComponentInChildren<LocalizedText>(true).SetText(ResolutionText(res)+"   ▼");
            string code=GameSystems.Exists?GameSystems.Instance.Localization.CurrentLanguage:"ko";
            int index=Mathf.Max(0,LocalizationTable.IndexOf(code));
            language.GetComponentInChildren<LocalizedText>(true).SetText(LocalizationTable.NativeNames[index]+"  ›");
            for(int i=0;i<languages.Length;i++)
            {
                languages[i].GetComponentInChildren<LocalizedText>(true).SetText((i==index?"✓  ":"")+LocalizationTable.NativeNames[i]);
                GoldUiTheme.MarkLanguage(languages[i],i==index);
            }
        }
        private static string ResolutionText(string res)=>SettingsService.TryParseResolution(res,out int w,out int h)?w+" × "+h:res;
        private void ToggleResolutions(){if(ResolutionListOpen)CloseResolutions();else OpenResolutions();}
        private void OpenResolutions()
        {
            if(!GameSystems.Exists)return;
            string current=GameSystems.Instance.Settings.Data.resolution;
            var options=SettingsService.ResolutionOptions(current);
            const float row=58,pad=10;
            while(resolutionOptions.Count<options.Count)resolutionOptions.Add(UiKit.Button(resolutionList,"Option"+resolutionOptions.Count,null));
            var order=new Selectable[options.Count];
            for(int i=0;i<resolutionOptions.Count;i++)
            {
                var b=resolutionOptions[i];b.gameObject.SetActive(i<options.Count);if(i>=options.Count)continue;
                string value=options[i];
                b.onClick.RemoveAllListeners();b.onClick.AddListener(()=>PickResolution(value));
                UiKit.Place(b.transform,.5f,1,0,-pad-i*row,540,row-6);UiKit.ButtonStyle(b);UiKit.TextStyle(b.transform.Find("Label"),26,TextAnchor.MiddleCenter);
                b.GetComponentInChildren<LocalizedText>(true).SetText((value==current?"✓  ":"")+ResolutionText(value));
                GoldUiTheme.MarkLanguage(b,value==current);order[i]=b;
            }
            // Below the button when it fits inside the window, else above it.
            float height=options.Count*row+pad*2-6,top=-558-64-6;
            if(top-height<-(window.rect.height-12))top=-558+height+6;
            UiKit.Place(resolutionList,1,1,-52,top,560,height);
            resolutionBlocker.transform.SetAsLastSibling();resolutionList.SetAsLastSibling();
            resolutionBlocker.SetActive(true);resolutionList.gameObject.SetActive(true);
            UiKit.Link(order);
            EventSystem.current?.SetSelectedGameObject(order[Mathf.Max(0,options.IndexOf(current))].gameObject);
        }
        /// <summary>Closes the resolution list. False when it was not open.</summary>
        public bool CloseResolutions()
        {
            if(!ResolutionListOpen)return false;
            resolutionBlocker.SetActive(false);resolutionList.gameObject.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(resolution.gameObject);return true;
        }
        private void PickResolution(string value)
        {
            var settings=GameSystems.Instance.Settings;
            if(settings.Data.resolution!=value){settings.Data.resolution=value;settings.NotifyChanged();}
            CloseResolutions();RefreshValues();
        }
        private void Literal(string n,string value)=>window.Find(n).GetComponent<LocalizedText>().SetText(value);
        public void OpenLanguages()
        {
            SetPage(true);
            int index=GameSystems.Exists?Mathf.Max(0,LocalizationTable.IndexOf(GameSystems.Instance.Localization.CurrentLanguage)):0;
            EventSystem.current?.SetSelectedGameObject(languages[index].gameObject);
        }
        public bool CloseLanguages()
        {
            if(!LanguagePage)return false;SetPage(false);EventSystem.current?.SetSelectedGameObject(language.gameObject);return true;
        }
        private void SetPage(bool page)
        {
            CloseResolutions();
            LanguagePage=page;foreach(var row in mainRows)row.gameObject.SetActive(!page);
            foreach(var b in languages)b.gameObject.SetActive(page);
            UiKit.Key(window.Find("Title"),page?"settings.language":"settings.title");
            UiKit.Key(close.transform,page?"worldmap.back":"common.close");
            UiKit.Key(window.Find("SettingsNote"),page?"ui.language_note":"ui.settings_note");
            if(page)
            {
                var order=new Selectable[languages.Length+1];for(int i=0;i<languages.Length;i++)order[i]=languages[i];order[languages.Length]=close;UiKit.Link(order);
            }
            else UiKit.Link(master,bgm,sfx,fullscreen,vsync,resolution,language,close);
        }
        private void OnRectTransformDimensionsChange(){if(initialized)Layout();}
        public void Layout()
        {
            if(!initialized)return;UiKit.Fit(window,(RectTransform)transform,1080,890);
            UiKit.Place(window.Find("Title"),.5f,1,0,-26,920,64);UiKit.TextStyle(window.Find("Title"),UiKit.HeadingSize,TextAnchor.MiddleCenter);
            UiKit.Place(window.Find("AudioHeading"),0,1,52,-112,940,40);
            string[] rows={"Master","Bgm","Sfx"};
            for(int i=0;i<3;i++)
            {
                float y=-164-i*70;UiKit.Place(window.Find(rows[i]+"Label"),0,1,56,y,330,50);UiKit.TextStyle(window.Find(rows[i]+"Label"),28);
                UiKit.Place(window.Find(rows[i]+"Slider"),0,1,414,y-6,470,40);UiKit.Place(window.Find(rows[i]+"Value"),1,1,-52,y,110,50);
            }
            UiKit.Place(window.Find("DisplayHeading"),0,1,52,-384,940,40);
            UiKit.Place(fullscreen.transform,0,1,56,-438,710,48);UiKit.Place(vsync.transform,0,1,56,-498,710,48);
            UiKit.TextStyle(fullscreen.GetComponentInChildren<Text>(true).transform,28);UiKit.TextStyle(vsync.GetComponentInChildren<Text>(true).transform,28);
            UiKit.Place(window.Find("FullscreenState"),1,1,-52,-438,180,48);UiKit.Place(window.Find("VsyncState"),1,1,-52,-498,180,48);
            UiKit.Place(window.Find("ResolutionLabel"),0,1,56,-562,300,58);UiKit.TextStyle(window.Find("ResolutionLabel"),28);
            UiKit.Place(resolution.transform,1,1,-52,-558,560,64);UiKit.ButtonStyle(resolution);
            UiKit.TextStyle(resolution.transform.Find("Label"),28,TextAnchor.MiddleCenter);
            UiKit.Place(window.Find("LanguageLabel"),0,1,56,-640,300,58);UiKit.TextStyle(window.Find("LanguageLabel"),28);
            UiKit.Place(language.transform,1,1,-52,-636,560,72);UiKit.ButtonStyle(language);
            UiKit.Place(window.Find("SettingsNote"),.5f,1,0,-718,950,42);window.Find("SettingsNote").GetComponent<Text>().color=UiKit.Muted;
            UiKit.Place(close.transform,.5f,0,0,38,440,72);UiKit.ButtonStyle(close,true);
            for(int i=0;i<languages.Length;i++){UiKit.Place(languages[i].transform,.5f,1,0,-142-i*92,760,72);UiKit.ButtonStyle(languages[i]);}
            window.Find("AudioHeading").GetComponent<Text>().color=GoldUiTheme.Gold;window.Find("DisplayHeading").GetComponent<Text>().color=GoldUiTheme.Gold;
        }
    }
}
