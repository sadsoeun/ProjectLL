using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace MyGame.EditorTools
{
    /// <summary>Helpers that build uGUI hierarchies in code. All text is legacy uGUI Text + LocalizedText.</summary>
    internal static class UiBuilder
    {
        public static readonly Color DimColor = new Color(0f, 0f, 0f, 0.65f);
        public static readonly Color WindowColor = new Color(0.10f, 0.13f, 0.20f, 0.98f);
        // Button tints are applied to a white image: neutral translucent black, lighter when focused.
        public static readonly Color ButtonColor = new Color(0f, 0f, 0f, 0.6f);
        public static readonly Color ButtonHighlightColor = new Color(0.22f, 0.22f, 0.22f, 0.8f);
        public static readonly Color ButtonSelectedColor = new Color(0.32f, 0.32f, 0.32f, 0.85f);
        public static readonly Color ButtonPressedColor = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        public static readonly Color ButtonDisabledColor = new Color(0f, 0f, 0f, 0.35f);
        public static readonly Color BarBackColor = new Color(0.05f, 0.07f, 0.12f, 1f);
        public static readonly Color AccentColor = new Color(0.95f, 0.75f, 0.2f, 1f);
        public static readonly Color TextColor = Color.white;
        public static readonly Color SubTextColor = new Color(0.8f, 0.85f, 0.95f, 1f);

        private const int UiLayer = 5;

        private static Sprite UiSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        private static Sprite BackgroundSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        private static Sprite KnobSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        private static Sprite CheckmarkSprite => AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");

        private static Font EditorFont => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ------------------------------------------------------------------ basics

        public static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = UiLayer;
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform Rt(GameObject go) => (RectTransform)go.transform;

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = NewUI(name, parent);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static Image CreatePanel(Transform parent, string name, Color color, bool rounded = false)
        {
            var go = NewUI(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            if (rounded)
            {
                img.sprite = BackgroundSprite;
                img.type = Image.Type.Sliced;
            }
            return img;
        }

        /// <summary>Full-screen dim image that also blocks clicks behind it.</summary>
        public static Image CreateDim(Transform parent, string name)
        {
            var img = CreatePanel(parent, name, DimColor);
            Stretch(Rt(img.gameObject));
            img.raycastTarget = true;
            return img;
        }

        public static Text CreateLabel(Transform parent, string name, string key, int size, TextAnchor align,
            Color? color = null, string literal = null, string fontLanguage = null, FontStyle style = FontStyle.Normal)
        {
            var go = NewUI(name, parent);
            var text = go.AddComponent<Text>();
            text.font = EditorFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = align;
            text.color = color ?? TextColor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = false;

            string preview = literal;
            if (preview == null && !string.IsNullOrEmpty(key) && LocalizationTable.TryGet("en", key, out var en))
                preview = en;
            text.text = preview ?? string.Empty;

            var localized = go.AddComponent<LocalizedText>();
            EditorWire.Set(localized, "key", key ?? string.Empty);
            EditorWire.Set(localized, "fixedFontLanguage", fontLanguage ?? string.Empty);
            return text;
        }

        public static LocalizedText Loc(Text text) => text.GetComponent<LocalizedText>();

        public static Button CreateButton(Transform parent, string name, string key, Vector2 size, int fontSize = 40,
            string literal = null, string fontLanguage = null)
        {
            var go = NewUI(name, parent);
            Rt(go).sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.sprite = UiSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;

            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = ButtonHighlightColor;
            colors.selectedColor = ButtonSelectedColor;
            colors.pressedColor = ButtonPressedColor;
            colors.disabledColor = ButtonDisabledColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            var label = CreateLabel(go.transform, "Label", key, fontSize, TextAnchor.MiddleCenter, Color.white, literal, fontLanguage);
            Stretch(Rt(label.gameObject));
            return button;
        }

        public static Slider CreateSlider(Transform parent, string name, Vector2 size)
        {
            var root = NewUI(name, parent);
            Rt(root).sizeDelta = size;

            var back = NewUI("Background", root.transform);
            var backImg = back.AddComponent<Image>();
            backImg.sprite = BackgroundSprite;
            backImg.type = Image.Type.Sliced;
            backImg.color = BarBackColor;
            var backRt = Rt(back);
            backRt.anchorMin = new Vector2(0f, 0.25f);
            backRt.anchorMax = new Vector2(1f, 0.75f);
            backRt.sizeDelta = Vector2.zero;

            var fillArea = NewUI("Fill Area", root.transform);
            var fillAreaRt = Rt(fillArea);
            fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRt.anchoredPosition = new Vector2(-5f, 0f);
            fillAreaRt.sizeDelta = new Vector2(-20f, 0f);

            var fill = NewUI("Fill", fillArea.transform);
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = UiSprite;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = AccentColor;
            Rt(fill).sizeDelta = new Vector2(10f, 0f);

            var handleArea = NewUI("Handle Slide Area", root.transform);
            var handleAreaRt = Rt(handleArea);
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.sizeDelta = new Vector2(-20f, 0f);

            var handle = NewUI("Handle", handleArea.transform);
            var handleImg = handle.AddComponent<Image>();
            handleImg.sprite = KnobSprite;
            handleImg.color = Color.white;
            Rt(handle).sizeDelta = new Vector2(32f, 0f);

            var slider = root.AddComponent<Slider>();
            slider.fillRect = Rt(fill);
            slider.handleRect = Rt(handle);
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.8f;

            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1f, 0.92f, 0.6f, 1f);
            colors.selectedColor = new Color(1f, 0.85f, 0.4f, 1f);
            slider.colors = colors;
            return slider;
        }

        public static Toggle CreateToggle(Transform parent, string name, string key, Vector2 size, int fontSize = 36)
        {
            var root = NewUI(name, parent);
            Rt(root).sizeDelta = size;

            var box = NewUI("Background", root.transform);
            var boxImg = box.AddComponent<Image>();
            boxImg.sprite = UiSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = Color.white;
            Place(Rt(box), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(size.y, size.y));

            var check = NewUI("Checkmark", box.transform);
            var checkImg = check.AddComponent<Image>();
            checkImg.sprite = CheckmarkSprite;
            checkImg.color = new Color(0.1f, 0.4f, 0.8f, 1f);
            Place(Rt(check), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.y * 0.8f, size.y * 0.8f));

            var label = CreateLabel(root.transform, "Label", key, fontSize, TextAnchor.MiddleLeft);
            var labelRt = Rt(label.gameObject);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(size.y + 20f, 0f);
            labelRt.offsetMax = Vector2.zero;

            var toggle = root.AddComponent<Toggle>();
            toggle.targetGraphic = boxImg;
            toggle.graphic = checkImg;
            toggle.isOn = true;
            var colors = ColorBlock.defaultColorBlock;
            colors.highlightedColor = new Color(1f, 0.92f, 0.6f, 1f);
            colors.selectedColor = new Color(1f, 0.85f, 0.4f, 1f);
            toggle.colors = colors;
            return toggle;
        }

        // ------------------------------------------------------------------ navigation

        public static void SetNav(Selectable s, Selectable up, Selectable down, Selectable left = null, Selectable right = null)
        {
            s.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = up,
                selectOnDown = down,
                selectOnLeft = left,
                selectOnRight = right,
            };
        }

        public static void LinkVertical(IList<Selectable> items, bool wrap)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Selectable up = i > 0 ? items[i - 1] : (wrap ? items[items.Count - 1] : null);
                Selectable down = i < items.Count - 1 ? items[i + 1] : (wrap ? items[0] : null);
                SetNav(items[i], up, down);
            }
        }

        // ------------------------------------------------------------------ event system

        public static UiFocusKeeper CreateEventSystem(GameObject defaultSelected = null, bool restoreFocus = true)
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            // Default UI actions are assigned by the module itself at runtime (OnEnable).
            go.AddComponent<InputSystemUIInputModule>();
            var keeper = go.AddComponent<UiFocusKeeper>();
            EditorWire.Set(keeper, "defaultSelected", defaultSelected);
            EditorWire.Set(keeper, "restoreFocus", restoreFocus);
            return keeper;
        }
    }
}
