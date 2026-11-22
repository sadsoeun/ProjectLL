using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Styles existing scene UI and spawned popups without replacing their bindings or navigation.</summary>
    public static class GoldUiTheme
    {
        public static readonly Color Gold = new Color(1f,.77f,.35f);
        public static readonly Color Cream = new Color(1f,.94f,.78f);
        public static readonly Color Panel = new Color(.065f,.065f,.068f,.97f);
        public static readonly Color Dim = new Color(.012f,.012f,.014f,.82f);
        public static readonly Color Muted = new Color(.77f,.75f,.70f);
        public static readonly Color Brass = new Color(.87f,.70f,.42f);
        public static readonly Color PrimaryInk = new Color(.10f,.075f,.045f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Also covers entering Play Mode with scene reload disabled.
            for (int i=0; i<SceneManager.sceneCount; i++) OnSceneLoaded(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyToScene(scene);

        /// <summary>Styles every game canvas of the scene. Also used by the Editor scene builders to bake the theme.</summary>
        public static void ApplyToScene(Scene scene)
        {
            if (!scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                    if (IsGameCanvas(canvas.name)) Apply(canvas.transform);
        }

        private static bool IsGameCanvas(string n) => n == "TitleCanvas" || n == "LanguageCanvas" ||
            n == "LoadingCanvas" || n == "WorldMapCanvas" || n == "HUDCanvas" || n == "LogoCanvas";

        public static void Apply(Transform root)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                text.color = Cream;
                text.fontStyle = FontStyle.Normal;
                var localized=text.GetComponent<LocalizedText>();
                if(localized!=null)localized.ApplyFont();
                else text.font=FontProvider.Get(GameSystems.Exists?GameSystems.Instance.Localization.CurrentLanguage:LocalizationTable.DefaultLanguage);
                text.raycastTarget = false;
                text.resizeTextForBestFit = false;
                text.resizeTextMaxSize = text.GetComponent<LocalizedTextFit>()?.DesignSize ?? text.fontSize;
                text.resizeTextMinSize = Mathf.Min(24,text.fontSize);
                var shadow = text.GetComponent<Shadow>();
                if (shadow == null) shadow = text.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0,0,0,.7f);
                shadow.effectDistance = new Vector2(0,-2);
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                string n = image.name;
                if (n == "Window" || n == "StageInfoPanel")
                {
                    image.sprite = null; image.color = Panel; Frame(image.transform);
                    if (n == "Window" && root.GetComponent<VoiceStudioPopup>() == null) DressPanel(image.transform);
                    Reveal(image.transform);
                }
                else if (n == "PauseMenu" || n == "ResultPanel" || n == "GameOverPanel" ||
                         image.transform == root && root.GetComponent<PopupBase>() != null) image.color = Dim;
                else if (n == "ProgressFill" || n == "Fill" || n == "Checkmark" || n == "Handle") image.color = Gold;
                else if (n == "ProgressBar" || n == "Background" && (root.name == "LoadingCanvas" || image.GetComponentInParent<Selectable>() != null))
                { image.sprite = null; image.color = Panel; }
                else if (n == "LoadingIcon")
                {
                    image.color = Color.clear;
                    Frame(image.transform).diamond = false;
                }
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                // Click-outside dismiss regions intentionally have no visual target.
                if (button.targetGraphic != null || button.transition != Selectable.Transition.None)
                    StyleButton(button, button.targetGraphic is GoldUiInlay);
            }
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                if (selectable is Button) continue;
                var colors = selectable.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(1f,.9f,.65f);
                colors.selectedColor = new Color(1f,.85f,.45f);
                colors.pressedColor = new Color(.65f,.5f,.2f);
                colors.disabledColor = new Color(.35f,.35f,.35f);
                selectable.colors = colors;
            }
            if (root.name == "TitleCanvas")
            {
                TitleLayout(root);
                if (root.GetComponent<TitlePresentation>() == null) root.gameObject.AddComponent<TitlePresentation>();
                if (Application.isPlaying && root.GetComponent<TitleIllustration>() == null) root.gameObject.AddComponent<TitleIllustration>();
            }
            else if (root.name == "HUDCanvas") HudLayout(root);
            else if (root.name == "WorldMapCanvas") WorldMapPresentation.Apply(root);
            else if (root.name == "LanguageCanvas")
            {
                Background(root);
                var list = root.Find("LanguageList"); if (list != null) { Frame(list); Reveal(list); }
            }
            else if (root.name == "LoadingCanvas")
            {
                var bar = root.Find("ProgressBar"); if (bar != null) Frame(bar);
            }
            if(root.GetComponent<SettingsPopup>()!=null) SettingsPresentation.Apply(root);
            else if(root.name=="LanguageCanvas"||root.name=="HUDCanvas"||root.name=="LoadingCanvas"||root.name=="TitleCanvas"||root.GetComponent<ConfirmPopup>()!=null) UiScreenLayout.Apply(root);
        }

        private static void Reveal(Transform target)
        {
            if(target.GetComponent<GoldUiReveal>()==null)target.gameObject.AddComponent<GoldUiReveal>();
        }

        public static void DressPanel(Transform panel, bool engraving = true)
        {
            var inlay = GoldUiInlay.Add(panel, GoldUiInlay.Surface.Window);
            inlay.transform.SetAsFirstSibling();
            Frame(panel).enabled = false;
            if (!engraving) return;
            var art = UiKit.Rect("BotanicalEtching", inlay.transform);
            Place(art, Vector2.one, Vector2.one, new Vector2(-8,-7), new Vector2(344,115));
            var print = UiKit.Ensure<RawImage>(art);
            if (print.texture == null) print.texture = Resources.Load<Texture2D>("Brand/MenuLeafEtching");
            print.color = new Color(1,1,1,.53f);
            print.raycastTarget = false;
        }

        public static void StyleButton(Button button, bool primary = false)
        {
            var image = button.GetComponent<Image>();
            var plate = button.transform.Find("PrimaryInlay");
            if (primary && plate == null) plate = GoldUiInlay.Add(button.transform, GoldUiInlay.Surface.Primary).transform;
            if (plate != null)
            {
                plate.SetAsFirstSibling();
                plate.gameObject.SetActive(primary);
            }
            if (image != null)
            {
                image.sprite = null;
                image.color = primary ? Color.clear : Color.white;
                button.targetGraphic = primary ? plate.GetComponent<Graphic>() : image;
            }
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = primary ? new Color(1f,.82f,.45f) : Panel;
            colors.highlightedColor = primary ? new Color(1,.93f,.74f) : new Color(.19f,.17f,.12f,.98f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = primary ? new Color(.81f,.62f,.36f) : new Color(.30f,.25f,.15f);
            colors.disabledColor = new Color(.078f,.078f,.077f,.86f);
            colors.colorMultiplier = 1; colors.fadeDuration = .14f;
            button.colors = colors;
            Frame(button.transform).enabled = !primary;
            var motion = UiKit.Ensure<GoldUiButton>(button);
            motion.RestInk = primary ? PrimaryInk : Cream;
            motion.FocusInk = primary ? PrimaryInk : Gold;
            var text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.color = motion.RestInk;
                var shadow = text.GetComponent<Shadow>();
                if (shadow != null) shadow.enabled = !primary;
                text.fontStyle = FontStyle.Normal;
                var localized=text.GetComponent<LocalizedText>();
                if(localized!=null)localized.ApplyFont();
                else text.font=FontProvider.Get(GameSystems.Exists?GameSystems.Instance.Localization.CurrentLanguage:LocalizationTable.DefaultLanguage);
                text.resizeTextForBestFit = false;
                text.fontSize = text.resizeTextMaxSize = UiKit.ActionSize;
                text.resizeTextMinSize = UiKit.ActionSize;
                var rect = text.rectTransform;
                rect.offsetMin = new Vector2(24,4); rect.offsetMax = new Vector2(-24,-4);
                LocalizedTextFit.Attach(text, UiKit.ActionSize);
            }
        }

        public static void MarkLanguage(Button button, bool selected)
        {
            if (button.GetComponent<GoldUiButton>() == null) StyleButton(button);
            button.GetComponent<GoldUiButton>().Mark(selected);
        }

        public static GoldUiFrame Frame(Transform parent)
        {
            var existing = parent.Find("GoldLinework");
            if (existing != null) return existing.GetComponent<GoldUiFrame>();
            var rt = Rect("GoldLinework",parent);
            Stretch(rt);
            var frame = rt.gameObject.AddComponent<GoldUiFrame>();
            frame.raycastTarget = false; frame.color = Gold;
            return frame;
        }

        private static void TitleLayout(Transform root)
        {
            Background(root);
            var logo = root.Find("GameLogo") as RectTransform;
            if (logo != null)
            {
                Place(logo,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(115,330),new Vector2(780,150));
                var text = logo.GetComponent<Text>(); text.alignment = TextAnchor.MiddleLeft;
                text.fontSize = text.resizeTextMaxSize = 108; text.color = Gold;
            }
            var menu = root.Find("MainMenu") as RectTransform;
            if (menu != null)
            {
                Place(menu,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(130,-35),new Vector2(600,500));
                int i=0;
                foreach (var button in menu.GetComponentsInChildren<Button>(true))
                    Place((RectTransform)button.transform,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-i++*122),new Vector2(580,96));
            }
            var notice = root.Find("NoticeText") as RectTransform;
            if (notice != null) Place(notice,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,70),new Vector2(1650,70));
        }

        private static void HudLayout(Transform root)
        {
            HudPresentation.Apply(root);
            var message = root.Find("MessageText");
            if (message != null) { var t=message.GetComponent<Text>(); t.fontSize=t.resizeTextMaxSize=90; Frame(message); }
        }

        private static void HudLabel(Transform root,string name,Vector2 anchor,Vector2 position,Vector2 size,int font)
        {
            var rt=root.Find(name) as RectTransform; if(rt==null)return;
            Place(rt,anchor,anchor,position,size);
            var t=rt.GetComponent<Text>(); t.fontSize=t.resizeTextMaxSize=font;
        }

        private static void Background(Transform root)
        {
            if(root.Find("GoldAtmosphere")!=null)return;
            var rt=Rect("GoldAtmosphere",root); Stretch(rt); rt.SetAsFirstSibling();
            var graphic=rt.gameObject.AddComponent<GoldUiBackdrop>(); graphic.raycastTarget=false;
        }

        public static RectTransform Rect(string name,Transform parent)
        {
            var rt=(RectTransform)new GameObject(name,typeof(RectTransform)).transform;
            rt.gameObject.layer=parent.gameObject.layer; rt.SetParent(parent,false);
            rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f); return rt;
        }
        public static void Stretch(RectTransform rt) { rt.anchorMin=Vector2.zero; rt.anchorMax=Vector2.one; rt.offsetMin=rt.offsetMax=Vector2.zero; }
        public static void Place(RectTransform rt,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
        { rt.anchorMin=rt.anchorMax=anchor; rt.pivot=pivot; rt.anchoredPosition=position; rt.sizeDelta=size; }
    }
}
