using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>World map information hierarchy and a single-page, keyboard-accessible menu.</summary>
    [DisallowMultipleComponent]
    public sealed class WorldMapPresentation : MonoBehaviour
    {
        private RectTransform info, window, hint, heading, menu, hintBack, headingBack;
        private WorldMapOrbitCarousel carousel;
        private RectTransform plotViewport;
        private RectTransform stageTitle, stageState;
        private CanvasRenderer titleInk, stateInk;
        private Vector2 titleRest, stateRest;
        private float identityTime = 1f, slideDirection;
        private int selectedStop = -1;
        private Button[] primary;
        private Button studioMenu, studioHud;
        /// <summary>Voice Studio entries: an always-visible HUD button (mouse) and a pause-menu row (keyboard / gamepad). Null until initialized.</summary>
        public Button StudioHudButton => studioHud;
        public Button StudioMenuButton => studioMenu;
        private Button rankingMenu, rankingHud;
        /// <summary>Ranking entries, same pair as the Voice Studio ones. Null until initialized.</summary>
        public Button RankingHudButton => rankingHud;
        public Button RankingMenuButton => rankingMenu;
        private LocalizedText menuNote;
        private UiFocusKeeper focus;
        private bool initialized;
        private static readonly Color Back = new Color32(15,16,19,248);
        private static readonly Color Paper = GoldUiTheme.Cream;
        private static readonly Color Silver = GoldUiTheme.Muted;
        private static readonly string[] RetiredRows = { "MoreButton", "BackButton", "SaveButton", "LoadButton", "QuitButton" };

        public static WorldMapPresentation Apply(Transform root)
        {
            var view = root.GetComponent<WorldMapPresentation>() ?? root.gameObject.AddComponent<WorldMapPresentation>();
            view.Initialize(); view.Layout(); return view;
        }

        public void Configure(UiFocusKeeper keeper) => focus = keeper;

        private void Initialize()
        {
            if (initialized) return;
            info = transform.Find("StageInfoPanel") as RectTransform;
            window = transform.Find("PauseMenu/Window") as RectTransform;
            hint = transform.Find("HintText") as RectTransform;
            menu = transform.Find("MenuButton") as RectTransform;
            if (info == null || window == null || menu == null) return;
            initialized = true;
            heading = Label(transform,"MapHeading","worldmap.heading",UiKit.BodySize,TextAnchor.MiddleLeft).GetComponent<RectTransform>();
            headingBack = Existing("MapHeadingBackdrop", transform);
            headingBack.SetAsFirstSibling();
            var headingImage = Ensure<Image>(headingBack);
            headingImage.color = Back;
            headingImage.raycastTarget = false;
            var marker = Ensure<Image>(Existing("IndexMark", headingBack));
            marker.raycastTarget = false;
            marker.color = GoldUiTheme.Gold;
            Place(marker.transform, 0, .5f, 0, 0, 3, 32);
            hint.GetComponent<LocalizedText>().SetKey("worldmap.hint_short");
            Style(hint,UiKit.CaptionSize,TextAnchor.MiddleCenter);
            hintBack=Existing("MapHintBackdrop",transform); hintBack.SetAsFirstSibling();
            var hintImage=Ensure<Image>(hintBack); hintImage.color=Back; hintImage.raycastTarget=false;
            menuNote = Label(window,"MenuNote","worldmap.menu_note",UiKit.CaptionSize,TextAnchor.MiddleCenter);
            studioMenu = NewButton("VoiceStudioButton","voice.title");
            studioHud = UiKit.Button(transform,"VoiceStudioHudButton","voice.title");
            studioHud.navigation = new Navigation { mode = Navigation.Mode.None };
            rankingMenu = NewButton("RankingButton","ranking.title");
            rankingHud = UiKit.Button(transform,"RankingHudButton","ranking.title");
            rankingHud.navigation = new Navigation { mode = Navigation.Mode.None };
            primary = VoiceRecorder.Supported
                ? new[] { Button("ResumeButton"), studioMenu, rankingMenu, Button("SettingsButton"), Button("TitleButton") }
                : new[] { Button("ResumeButton"), rankingMenu, Button("SettingsButton"), Button("TitleButton") };
            studioMenu.gameObject.SetActive(VoiceRecorder.Supported);
            foreach (var button in primary) StyleButton(button);
            // The "more options" page is gone: its rows (and the save / load / quit buttons it held) stay in the scene, hidden.
            foreach (var name in RetiredRows) { var row = window.Find(name); if (row != null) row.gameObject.SetActive(false); }
            info.GetComponent<Image>().color = Back;
            window.GetComponent<Image>().color = Back;
            info.GetComponent<Image>().raycastTarget = false;
            GoldUiTheme.Frame(info).enabled = false;
            var chartRect = Existing("RouteChart", info);
            chartRect.SetAsFirstSibling();
            GoldUiTheme.Stretch(chartRect);
            var chart = Ensure<WorldMapChartGraphic>(chartRect);
            chart.raycastTarget = false;
            plotViewport = Existing("PlateViewport", info);
            Ensure<RectMask2D>(plotViewport);
            var plates = Existing("OrbitPlates", plotViewport);
            GoldUiTheme.Stretch(plates);
            carousel = Ensure<WorldMapOrbitCarousel>(plates);
            carousel.raycastTarget = false;
            carousel.Bind(chart);
            stageTitle = (RectTransform)info.Find("StageNameText");
            stageState = (RectTransform)info.Find("StateText");
            titleInk = stageTitle.GetComponent<Text>().canvasRenderer;
            stateInk = stageState.GetComponent<Text>().canvasRenderer;
            GoldUiTheme.Frame(window).diamond = false;
            Style(info.Find("StageNameText"),UiKit.HudSize,TextAnchor.MiddleLeft);
            Style(info.Find("StateText"),UiKit.BodySize,TextAnchor.MiddleLeft);
            Style(info.Find("BestTimeText"),UiKit.BodySize,TextAnchor.MiddleLeft);
            Style(info.Find("SpecialsText"),UiKit.BodySize,TextAnchor.MiddleLeft);
            GoldUiTheme.DressPanel(window);
            var menuButton = menu.GetComponent<Button>();
            StyleButton(menuButton);
            menuButton.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(info,"BestTimeLabel","worldmap.best_time_label",22,TextAnchor.MiddleLeft);
            Label(info,"SpecialsLabel","worldmap.leaves_label",22,TextAnchor.MiddleLeft);
            foreach(var name in new[]{"IdentityDivider","StatsDivider"})
            {
                var line=Ensure<Image>(Existing(name,info));line.color=new Color(1f,.77f,.35f,.22f);line.raycastTarget=false;
            }
            ResetMenu();
        }

        private Button Button(string name) => window.Find(name).GetComponent<Button>();
        private Button NewButton(string name,string key)
        {
            var rt = Existing(name,window);
            var image = Ensure<Image>(rt);
            var button = Ensure<Button>(rt); button.targetGraphic = image;
            var label = Label(rt,"Label",key,30,TextAnchor.MiddleCenter);
            GoldUiTheme.Stretch(label.GetComponent<RectTransform>());
            return button;
        }
        private static LocalizedText Label(Transform parent,string name,string key,int size,TextAnchor alignment)
        {
            var rt = Existing(name,parent); Ensure<Text>(rt);
            var loc = Ensure<LocalizedText>(rt); loc.SetKey(key);
            // Editor previews have no service singleton.
            if (!Application.isPlaying && LocalizationTable.TryGet("ko",key,out string value)) loc.GetComponent<Text>().text=value;
            Style(rt,size,alignment); return loc;
        }
        private static RectTransform Existing(string name,Transform parent) => parent.Find(name) as RectTransform ?? GoldUiTheme.Rect(name,parent);
        private static T Ensure<T>(Component c) where T : Component => c.GetComponent<T>() ?? c.gameObject.AddComponent<T>();
        private static void Style(Transform target,int size,TextAnchor alignment)
        {
            var text = target.GetComponent<Text>(); if (text == null) return;
            UiKit.TextStyle(target,size,alignment);
        }
        private static void StyleButton(Button button)
        {
            GoldUiTheme.StyleButton(button);
            Style(button.GetComponentInChildren<Text>(true).transform,UiKit.ActionSize,TextAnchor.MiddleCenter);
            var colors=button.colors; colors.normalColor=Back; button.colors=colors;
        }

        public void ResetMenu()
        {
            if (!initialized) return;
            foreach (var name in RetiredRows) { var row = window.Find(name); if (row != null) row.gameObject.SetActive(false); }
            for(int i=0;i<primary.Length;i++)
            {
                primary[i].gameObject.SetActive(true);
                primary[i].navigation=new Navigation { mode=Navigation.Mode.Explicit,
                    selectOnUp=primary[(i+primary.Length-1)%primary.Length], selectOnDown=primary[(i+1)%primary.Length] };
            }
        }

        public void SetMenuVisible(bool visible)
        {
            info.gameObject.SetActive(!visible); hint.gameObject.SetActive(!visible);
            menu.gameObject.SetActive(!visible); heading.gameObject.SetActive(!visible);
            hintBack.gameObject.SetActive(!visible);
            headingBack.gameObject.SetActive(!visible);
            studioHud.gameObject.SetActive(!visible && VoiceRecorder.Supported);
            rankingHud.gameObject.SetActive(!visible);
            if (visible) ResetMenu();
        }

        private void OnRectTransformDimensionsChange() { if(initialized) Layout(); }

        public void SetEncounterStages(System.Collections.Generic.IReadOnlyList<string> stageIds) =>
            carousel.SetEncounterStages(stageIds);

        public void SetRoutePosition(int index, int count)
        {
            carousel.SelectStop(index, count);
            if (Application.isPlaying && selectedStop >= 0 && index != selectedStop)
            {
                slideDirection = Mathf.Sign(index - selectedStop);
                identityTime = 0;
                PositionIdentity();
            }
            selectedStop = index;
        }

        private void Update()
        {
            if (identityTime >= 1f) return;
            identityTime = Mathf.Min(1f, identityTime + Time.unscaledDeltaTime / .42f);
            PositionIdentity();
        }

        private void PositionIdentity()
        {
            float title = Mathf.SmoothStep(0, 1, identityTime);
            float state = Mathf.SmoothStep(0, 1, (identityTime - .12f) / .88f);
            stageTitle.anchoredPosition = titleRest + Vector2.right * (22 * slideDirection * (1 - title));
            stageState.anchoredPosition = stateRest + Vector2.right * (14 * slideDirection * (1 - state));
            titleInk.SetAlpha(Mathf.Lerp(.2f, 1, title));
            stateInk.SetAlpha(Mathf.Lerp(.2f, 1, state));
        }

        public void Layout()
        {
            if(!initialized) return;
            float width=((RectTransform)transform).rect.width;
            if(width<1) width=1920;
            info.GetComponent<Image>().color=Back; window.GetComponent<Image>().color=Back;
            foreach(var button in primary) StyleButton(button);
            if(primary.Length>0) UiKit.ButtonStyle(primary[0],true);
            var enterButton=info.Find("EnterButton").GetComponent<Button>();
            UiKit.ButtonStyle(enterButton,true);
            float w = Mathf.Min(1640, width - 64);
            bool compact = w < 1320;
            float plotWidth = Mathf.Clamp(w * .25f, 146, 396);
            Place(info, .5f, 0, 0, 68, w, compact ? 234 : 218);
            Place(plotViewport, 0, 1, 14, -10, plotWidth - 16, info.rect.height - 52);
            float tabWidth = Mathf.Min(268, width * .26f);
            Place(headingBack, 0, 1, 32, -24, tabWidth, 62);
            Place(heading, 0, 1, 56, -24, tabWidth - 40, 62);
            HierarchyStyle(heading, 30, false, Paper);
            float menuWidth = Mathf.Min(156, width * .19f);
            Place(menu,1,1,-32,-24,menuWidth,62);
            StyleButton(menu.GetComponent<Button>());
            StyleButton(studioHud); studioHud.navigation = new Navigation { mode = Navigation.Mode.None };
            float studioWidth = VoiceRecorder.Supported ? Mathf.Min(248,width*.32f) : -12; // no studio button: the ranking button takes its place
            if (VoiceRecorder.Supported) Place(studioHud.transform,1,1,-44-menuWidth,-24,studioWidth,62);
            else studioHud.gameObject.SetActive(false);
            StyleButton(rankingHud); rankingHud.navigation = new Navigation { mode = Navigation.Mode.None };
            Place(rankingHud.transform,1,1,-56-menuWidth-studioWidth,-24,Mathf.Min(228,width*.24f),62);
            Place(hint,.5f,0,0,22,w-32,30);
            Place(hintBack,.5f,0,0,14,w,46);
            Place(transform.Find("MessageText"),.5f,1,0,-105,Mathf.Min(1100,width-80),64);
            float contentX = plotWidth + 22;
            float usable = w - contentX - 28;
            if(!compact)
            {
                float bestX = contentX + usable * .34f;
                float leavesX = contentX + usable * .54f;
                Place(info.Find("StateText"),0,1,contentX,-32,usable*.32f,32);
                Place(info.Find("StageNameText"),0,1,contentX,-69,usable*.32f,60);
                Place(info.Find("BestTimeLabel"),0,1,bestX,-32,usable*.18f,32);
                Place(info.Find("BestTimeText"),0,1,bestX,-70,usable*.18f,56);
                Place(info.Find("SpecialsLabel"),0,1,leavesX,-32,usable*.19f,32);
                Place(info.Find("SpecialsText"),0,1,leavesX,-70,84,56);
                Place(info.Find("SpecialSlots"),0,1,leavesX+86,-86,86,24);
                Place(info.Find("EnterButton"),1,1,-28,-50,usable*.235f,76);
                Place(info.Find("IdentityDivider"),0,1,bestX-18,-32,1,92);
                Place(info.Find("StatsDivider"),0,1,leavesX-18,-32,1,92);
            }
            else
            {
                float leavesX = contentX + usable * .49f;
                Place(info.Find("StateText"),0,1,contentX,-12,usable*.59f,28);
                Place(info.Find("StageNameText"),0,1,contentX,-40,usable*.59f,54);
                Place(info.Find("EnterButton"),1,1,-28,-24,usable*.36f,64);
                Place(info.Find("IdentityDivider"),0,1,contentX,-104,usable,1);
                Place(info.Find("StatsDivider"),0,1,leavesX-14,-116,1,70);
                Place(info.Find("BestTimeLabel"),0,1,contentX,-111,usable*.45f,30);
                Place(info.Find("BestTimeText"),0,1,contentX,-141,usable*.45f,48);
                Place(info.Find("SpecialsLabel"),0,1,leavesX,-111,usable*.49f,30);
                Place(info.Find("SpecialsText"),0,1,leavesX,-141,84,48);
                Place(info.Find("SpecialSlots"),0,1,leavesX+86,-153,86,24);
            }
            titleRest = stageTitle.anchoredPosition;
            stateRest = stageState.anchoredPosition;
            PositionIdentity();
            var slots=info.Find("SpecialSlots");
            for(int i=0;i<slots.childCount;i++) { var slot=slots.GetChild(i); slot.localRotation=Quaternion.identity; var shape=slot.Find("SlotShape"); if(shape!=null)shape.localRotation=Quaternion.identity; Place(slot,0,1,i*29,0,24,24); }
            ApplyInfoTypography();
            Place(window,.5f,.5f,0,0,Mathf.Min(660,width-64),258+primary.Length*88);
            Place(window.Find("Title"),.5f,1,0,-32,500,64);
            Style(window.Find("Title"),UiKit.HeadingSize,TextAnchor.MiddleCenter);
            Place(menuNote.transform,.5f,0,0,30,Mathf.Min(560,width-110),58);
            for(int i=0;i<primary.Length;i++) Place(primary[i].transform,.5f,1,0,-126-i*88,Mathf.Min(520,width-120),72);
            info.localScale=Vector3.one; window.localScale=Vector3.one;
            info.GetComponent<GoldUiReveal>()?.RefreshRestPose();
            window.GetComponent<GoldUiReveal>()?.RefreshRestPose();
        }
        private void ApplyInfoTypography()
        {
            HierarchyStyle(info.Find("StageNameText"),42,false,Paper);
            HierarchyStyle(info.Find("StateText"),22,false,GoldUiTheme.Gold);
            HierarchyStyle(info.Find("BestTimeLabel"),21,false,Silver);
            HierarchyStyle(info.Find("SpecialsLabel"),21,false,Silver);
            HierarchyStyle(info.Find("BestTimeText"),34,false,Paper);
            HierarchyStyle(info.Find("SpecialsText"),32,false,Paper);
            var action=info.Find("EnterButton").GetComponentInChildren<Text>(true);
            HierarchyStyle(action.transform,32,false,GoldUiTheme.PrimaryInk,TextAnchor.MiddleCenter);
            HierarchyStyle(hint,22,false,new Color(.65f,.67f,.69f),TextAnchor.MiddleCenter);
        }
        private static void HierarchyStyle(Transform target,int size,bool bold,Color color,TextAnchor anchor=TextAnchor.MiddleLeft)
        {
            UiKit.TextStyle(target,size,anchor);
            var text=target.GetComponent<Text>();text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;text.color=color;
            // On a dark panel, a thick shadow compounds simulated font weight. Keep the strokes clean.
            var shadow=text.GetComponent<Shadow>();if(shadow!=null)shadow.enabled=false;
            text.horizontalOverflow=HorizontalWrapMode.Overflow;
            text.GetComponent<LocalizedTextFit>()?.FitNow();
        }
        private static void Place(Transform t,float ax,float ay,float x,float y,float w,float h)
        {
            if(t!=null) GoldUiTheme.Place((RectTransform)t,new Vector2(ax,ay),new Vector2(ax,ay),new Vector2(x,y),new Vector2(w,h));
        }
    }
}
