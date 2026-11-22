using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>What one prompt shows: a main line, an optional smaller line and control hints.</summary>
    public struct TutorialPromptContent
    {
        public string textKey;
        public string subKey;
        public TutorialGlyph[] glyphs;
    }

    /// <summary>
    /// The single tutorial hint plate (view only: no game rules). Dark translucent backing with the fine gold frame of
    /// the HUD, Noto Sans via FontProvider, regular weight; hierarchy comes from size and brightness. Control hints follow
    /// the active device and the language live. Uses unscaled time so pauses / time stops do not freeze its fades.
    /// </summary>
    public sealed class TutorialPromptView : MonoBehaviour
    {
        private enum Phase { Hidden, In, Shown, Hold, Out }

        private TutorialViewSettings settings;
        private RectTransform root, row, textColumn;
        private CanvasGroup group;
        private Image backing;
        private GoldUiFrame frame;
        private Text mainText, subText;
        private readonly List<RectTransform> glyphObjects = new List<RectTransform>();

        private RectTransform anchorRect;
        private bool anchorAbove;
        private float fallbackY;

        private TutorialPromptContent content;
        private Phase phase = Phase.Hidden;
        private float phaseTime, holdFor, suppressAlpha = 1f, accent;
        private bool suppressed, subscribed;

        /// <summary>Editor previews / captures only (no GameSystems): input service and language used for the hints.</summary>
        public static InputModeService PreviewInput;
        public static string PreviewLanguage;

        public bool IsIdle => phase == Phase.Hidden;
        /// <summary>The prompt is up and not yet completed (fading in or shown).</summary>
        public bool IsOpen => phase == Phase.In || phase == Phase.Shown;
        public string MainText => mainText != null ? mainText.text : null;

        /// <summary>Creates the plate as the last child of the canvas. anchorAbove: sit above anchor (world map panel);
        /// otherwise below it (stage HUD). fallbackY is used when the anchor is missing (top offset / bottom offset).</summary>
        public static TutorialPromptView Create(Transform canvas, TutorialViewSettings settings, RectTransform anchor, bool anchorAbove, float fallbackY)
        {
            var rt = GoldUiTheme.Rect("TutorialPrompt", canvas);
            var view = rt.gameObject.AddComponent<TutorialPromptView>();
            view.settings = settings ?? new TutorialViewSettings();
            view.anchorRect = anchor;
            view.anchorAbove = anchorAbove;
            view.fallbackY = fallbackY;
            view.Build(rt);
            return view;
        }

        private void Build(RectTransform rt)
        {
            root = rt;
            group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            backing = rt.gameObject.AddComponent<Image>();
            backing.color = settings.background; backing.raycastTarget = false;
            frame = GoldUiTheme.Frame(rt); frame.diamond = false; frame.color = settings.frame; frame.inset = 2f;

            row = GoldUiTheme.Rect("Row", rt);
            textColumn = GoldUiTheme.Rect("Text", rt);
            mainText = TutorialUi.MakeText("Main", textColumn, settings.mainFontSize, GoldUiTheme.Cream);
            subText = TutorialUi.MakeText("Sub", textColumn, settings.subFontSize, new Color(.93f, .90f, .83f));
            rt.gameObject.SetActive(false);
        }

        private void OnEnable() => Subscribe(true);
        private void OnDisable() => Subscribe(false);
        private void OnDestroy() => Subscribe(false);

        private void Subscribe(bool on)
        {
            if (!GameSystems.Exists || on == subscribed)
                return;
            subscribed = on;
            var systems = GameSystems.Instance;
            if (on)
            {
                systems.Localization.LanguageChanged += Relayout;
                systems.InputMode.ActiveDeviceChanged += OnDeviceChanged;
            }
            else
            {
                systems.Localization.LanguageChanged -= Relayout;
                systems.InputMode.ActiveDeviceChanged -= OnDeviceChanged;
            }
        }

        private void OnDeviceChanged(InputDeviceKind _) => Relayout();

        /// <summary>Changes what the plate hangs under (e.g. the boss HP bar while a boss fight is on). Null = fallback offset.</summary>
        public void SetAnchor(RectTransform anchor) => anchorRect = anchor;

        // ------------------------------------------------------------------ API

        public void Show(TutorialPromptContent value)
        {
            content = value;
            root.gameObject.SetActive(true);
            accent = 0f;
            SetPhase(Phase.In);
            Relayout();
            Tick(0f);
        }

        /// <summary>Swaps the text of the open prompt (e.g. the helper line) without a new entrance.</summary>
        public void ChangeText(string textKey, string subKey)
        {
            content.textKey = textKey;
            content.subKey = subKey;
            Relayout();
        }

        /// <summary>Done: gold accent, optional success line with a tick (successKey), stays holdSeconds, then fades out.</summary>
        public void Complete(float holdSeconds, string successKey = null)
        {
            if (phase == Phase.Hidden || phase == Phase.Out || phase == Phase.Hold)
                return;
            if (!string.IsNullOrEmpty(successKey))
            {
                content = new TutorialPromptContent { textKey = successKey, glyphs = new[] { TutorialGlyph.Check } };
                Relayout();
            }
            holdFor = Mathf.Max(0f, holdSeconds);
            accent = 1f;
            SetPhase(Phase.Hold);
        }

        /// <summary>Fades out now (passed by, replaced by the next prompt, stage left).</summary>
        public void Dismiss()
        {
            if (phase != Phase.Hidden && phase != Phase.Out)
                SetPhase(Phase.Out);
        }

        /// <summary>Editor captures / tests: jump to the fully shown pose.</summary>
        public void SnapVisible()
        {
            if (phase == Phase.Hidden) return;
            SetPhase(Phase.Shown);
            suppressAlpha = suppressed ? 0f : 1f;
            group.alpha = suppressAlpha;
            Place(0f);
        }

        public void HideImmediate()
        {
            SetPhase(Phase.Hidden);
            group.alpha = 0f;
            root.gameObject.SetActive(false);
        }

        /// <summary>Hidden while pause / settings / game over / clear / dead; the prompt state is kept.</summary>
        public void SetSuppressed(bool value) => suppressed = value;

        // ------------------------------------------------------------------ animation

        private void SetPhase(Phase next) { phase = next; phaseTime = 0f; }

        private void Update() => Tick(Time.unscaledDeltaTime);

        private void Tick(float dt)
        {
            if (phase == Phase.Hidden)
                return;
            phaseTime += dt;
            suppressAlpha = Mathf.MoveTowards(suppressAlpha, suppressed ? 0f : 1f, dt / 0.12f);
            float visible = 1f, slide = 0f;
            switch (phase)
            {
                case Phase.In:
                {
                    float k = Mathf.Clamp01(phaseTime / settings.fadeInSeconds);
                    float e = 1f - (1f - k) * (1f - k);
                    visible = e; slide = (1f - e) * settings.slidePixels;
                    if (k >= 1f) SetPhase(Phase.Shown);
                    break;
                }
                case Phase.Hold:
                    if (phaseTime >= holdFor) SetPhase(Phase.Out);
                    break;
                case Phase.Out:
                {
                    float k = Mathf.Clamp01(phaseTime / settings.fadeOutSeconds);
                    visible = 1f - k; slide = -k * settings.slidePixels * .5f;
                    if (k >= 1f) { HideImmediate(); return; }
                    break;
                }
            }
            accent = Mathf.MoveTowards(accent, phase == Phase.Hold || phase == Phase.Out ? .55f : 0f, dt * 2.2f);
            frame.color = Color.Lerp(settings.frame, settings.doneAccent, accent);
            group.alpha = visible * suppressAlpha;
            Place(slide);
        }

        private void LateUpdate()
        {
            if (phase != Phase.Hidden)
                Place(phase == Phase.In ? (1f - Mathf.Clamp01(phaseTime / settings.fadeInSeconds)) * settings.slidePixels : 0f);
        }

        /// <summary>Positions the plate relative to the anchor rect, in the parent canvas space.</summary>
        private void Place(float slide)
        {
            var parent = (RectTransform)root.parent;
            float y;
            if (anchorRect != null)
            {
                var corners = new Vector3[4];
                anchorRect.GetWorldCorners(corners);
                Vector3 local = parent.InverseTransformPoint(anchorAbove ? corners[1] : corners[0]);
                // Parent-local y (pivot-centred) -> offset from the parent's top (below) or bottom (above).
                y = anchorAbove
                    ? local.y - parent.rect.yMin + settings.worldMapGap
                    : local.y - parent.rect.yMax - settings.stageGapBelowHud;
            }
            else
                y = anchorAbove ? fallbackY : -fallbackY;
            if (anchorAbove)
                GoldUiTheme.Place(root, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, y - slide), root.sizeDelta);
            else
                GoldUiTheme.Place(root, new Vector2(.5f, 1f), new Vector2(.5f, 1f), new Vector2(0f, y + slide), root.sizeDelta);
        }

        // ------------------------------------------------------------------ layout

        private void Relayout()
        {
            if (root == null || string.IsNullOrEmpty(content.textKey))
                return;
            var font = TutorialUi.Font;
            mainText.font = subText.font = font;
            mainText.text = TutorialUi.Lookup(content.textKey);
            bool hasSub = !string.IsNullOrEmpty(content.subKey);
            subText.text = hasSub ? TutorialUi.Lookup(content.subKey) : "";
            subText.gameObject.SetActive(hasSub);

            float h = settings.glyphHeight;
            const float padX = 26f, padY = 16f, gap = 18f;
            TutorialUi.DestroyAll(glyphObjects);
            float glyphWidth = TutorialUi.BuildGlyphRow(row, content.glyphs, font, h, glyphObjects);

            float maxText = settings.maxWidth - padX * 2f - (glyphWidth > 0f ? glyphWidth + gap : 0f);
            float textWidth = Mathf.Min(maxText, Mathf.Max(TutorialUi.PreferredWidth(mainText), hasSub ? TutorialUi.PreferredWidth(subText) : 0f) + 2f);
            float mainH = TutorialUi.PreferredHeight(mainText, textWidth), subH = hasSub ? TutorialUi.PreferredHeight(subText, textWidth) : 0f;
            float textHeight = mainH + (hasSub ? 4f + subH : 0f);
            float innerH = Mathf.Max(textHeight, glyphWidth > 0f ? h : 0f);
            float width = padX * 2f + (glyphWidth > 0f ? glyphWidth + gap : 0f) + textWidth;
            float height = innerH + padY * 2f;
            root.sizeDelta = new Vector2(width, height);

            GoldUiTheme.Place(row, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(padX, 0f), new Vector2(glyphWidth, h));
            GoldUiTheme.Place(textColumn, new Vector2(0f, .5f), new Vector2(0f, .5f),
                new Vector2(padX + (glyphWidth > 0f ? glyphWidth + gap : 0f), 0f), new Vector2(textWidth, textHeight));
            GoldUiTheme.Place(mainText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(textWidth, mainH));
            if (hasSub)
                GoldUiTheme.Place(subText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -mainH - 4f), new Vector2(textWidth, subH));
            Place(0f);
        }

    }
}
