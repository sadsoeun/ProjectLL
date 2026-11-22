using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>One explanation page as shown: title, body, control hints, page counter.</summary>
    public struct TutorialExplainContent
    {
        public string titleKey;
        public string bodyKey;
        public TutorialGlyph[] glyphs;
        public int page;
        public int pageCount;
    }

    /// <summary>Small vector shapes of the bubble: its tail, the "next" arrow and the hold-to-skip ring.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TutorialShape : MaskableGraphic
    {
        public enum Kind { Triangle, TailTriangle, HoldRing }
        public Kind kind;
        public Color edge = Color.clear;
        private float progress;

        public void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(value, progress)) return;
            progress = value; SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (kind == Kind.HoldRing)
            {
                float radius = Mathf.Min(r.width, r.height) * .5f - 2f;
                Arc(vh, r.center, radius, 3f, 1f, new Color(color.r, color.g, color.b, color.a * .28f));
                if (progress > 0f) Arc(vh, r.center, radius, 3.5f, progress, color);
                return;
            }
            // Triangle pointing up (+y); rotate the transform for other directions.
            Vector2 a = new Vector2(r.xMin, r.yMin), b = new Vector2(r.xMax, r.yMin), tip = new Vector2(r.center.x, r.yMax);
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(tip, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            if (kind == Kind.TailTriangle && edge.a > 0f)
            {
                Line(vh, a, tip, 1.5f, edge); Line(vh, tip, b, 1.5f, edge);
            }
        }

        private static void Arc(VertexHelper vh, Vector2 c, float radius, float width, float fraction, Color col)
        {
            const int seg = 40;
            int count = Mathf.Max(1, Mathf.RoundToInt(seg * fraction));
            for (int i = 0; i < count; i++)
            {
                // clockwise from the top
                float a0 = Mathf.PI * .5f - i * Mathf.PI * 2f / seg, a1 = Mathf.PI * .5f - (i + 1) * Mathf.PI * 2f / seg;
                Line(vh, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, width, col);
            }
        }

        private static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = (b - a).normalized, n = new Vector2(-d.y, d.x) * width * .5f;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero); vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero); vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }

    /// <summary>
    /// Explanation phase view (no game rules): the whole screen is dimmed except ONE highlight (SpotlightOverlay), and one
    /// bubble beside it explains it, with a tail toward the highlight, a "next" indicator and a hold-to-skip hint.
    /// Black / gold styling, Noto Sans, regular weight. Animates on unscaled time (the game is frozen meanwhile).
    /// </summary>
    public sealed class TutorialExplainView : MonoBehaviour
    {
        private enum Phase { Hidden, In, Shown, Out }
        private enum Side { Below, Above, Right, Left, None }

        private TutorialExplainSettings settings;
        private bool preferAbove;
        private RectTransform root, bubble, tail, glyphRow, skipRow, nextRow, divider;
        private CanvasGroup group, bubbleGroup, nextGroup;
        private Canvas sortCanvas;
        public const int SortingOrder = 35;
        private SpotlightOverlay overlay;
        private Text title, body, pageText, skipText, nextText;
        private TutorialShape tailShape, holdRing, nextArrow;
        private readonly List<RectTransform> glyphObjects = new List<RectTransform>(), skipObjects = new List<RectTransform>(), nextObjects = new List<RectTransform>();

        private TutorialExplainContent content;
        private SpotShape target, current;
        private Phase phase = Phase.Hidden;
        private float phaseTime, pageTime, skipProgress, nextRowWidth;
        private string shownLanguage; private InputDeviceKind shownDevice;
        private Vector2 bubbleSize; private Vector2 laidOutFor = new Vector2(float.NaN, 0f);

        public bool IsOpen => phase != Phase.Hidden;
        public bool IsClosing => phase == Phase.Out;
        /// <summary>The page has been readable for the minimum time; Confirm may advance.</summary>
        public bool CanAdvance => phase == Phase.Shown && pageTime >= settings.minReadSeconds;
        public string TitleText => title != null ? title.text : null;
        public Rect CanvasRect => root.rect;

        /// <summary>preferAbove: put the bubble above the highlight when both sides fit (world map, whose info panel is at the bottom).</summary>
        public static TutorialExplainView Create(Transform canvas, TutorialExplainSettings settings, bool preferAbove)
        {
            var rt = GoldUiTheme.Rect("TutorialExplain", canvas);
            GoldUiTheme.Stretch(rt);
            var view = rt.gameObject.AddComponent<TutorialExplainView>();
            view.settings = settings ?? new TutorialExplainSettings();
            view.preferAbove = preferAbove;
            view.Build(rt);
            return view;
        }

        private void Build(RectTransform rt)
        {
            root = rt;
            group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = true;
            // Own sorting layer above the other overlay canvases of the stage (boss HP bar = 30) and below the "Now!" cut-in (40),
            // so the dim layer also covers them and a hole can light one of them up.
            sortCanvas = rt.gameObject.AddComponent<Canvas>();
            rt.gameObject.AddComponent<GraphicRaycaster>();

            var overlayRt = GoldUiTheme.Rect("Dim", rt); GoldUiTheme.Stretch(overlayRt);
            overlay = overlayRt.gameObject.AddComponent<SpotlightOverlay>();
            overlay.raycastTarget = true;

            bubble = GoldUiTheme.Rect("Bubble", rt);
            bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
            var back = bubble.gameObject.AddComponent<Image>(); back.color = settings.bubbleColor; back.raycastTarget = false;
            var frame = GoldUiTheme.Frame(bubble); frame.diamond = false; frame.inset = 2f; frame.color = new Color(.86f, .70f, .42f, .9f);

            tail = GoldUiTheme.Rect("Tail", rt);
            tailShape = tail.gameObject.AddComponent<TutorialShape>();
            tailShape.kind = TutorialShape.Kind.TailTriangle; tailShape.color = settings.bubbleColor;
            tailShape.edge = new Color(.86f, .70f, .42f, .9f); tailShape.raycastTarget = false;

            title = TutorialUi.MakeText("Title", bubble, settings.titleFontSize, settings.titleColor);
            pageText = TutorialUi.MakeText("Page", bubble, settings.hintFontSize, TutorialUi.Muted);
            pageText.alignment = TextAnchor.MiddleRight; pageText.horizontalOverflow = HorizontalWrapMode.Overflow;
            body = TutorialUi.MakeText("Body", bubble, settings.bodyFontSize, GoldUiTheme.Cream);
            body.alignment = TextAnchor.UpperLeft; body.lineSpacing = 1.18f;
            glyphRow = GoldUiTheme.Rect("Keys", bubble);
            divider = GoldUiTheme.Rect("Divider", bubble);
            var line = divider.gameObject.AddComponent<Image>(); line.color = new Color(.86f, .70f, .42f, .28f); line.raycastTarget = false;

            skipRow = GoldUiTheme.Rect("Skip", bubble);
            var ringRt = GoldUiTheme.Rect("HoldRing", skipRow);
            holdRing = ringRt.gameObject.AddComponent<TutorialShape>();
            holdRing.kind = TutorialShape.Kind.HoldRing; holdRing.color = settings.ringColor; holdRing.raycastTarget = false;
            skipText = TutorialUi.MakeText("SkipText", skipRow, settings.hintFontSize, TutorialUi.Muted);
            skipText.horizontalOverflow = HorizontalWrapMode.Overflow;

            nextRow = GoldUiTheme.Rect("Next", bubble);
            nextGroup = nextRow.gameObject.AddComponent<CanvasGroup>();
            nextText = TutorialUi.MakeText("NextText", nextRow, settings.hintFontSize + 2, GoldUiTheme.Cream);
            nextText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var arrowRt = GoldUiTheme.Rect("Arrow", nextRow);
            nextArrow = arrowRt.gameObject.AddComponent<TutorialShape>();
            nextArrow.kind = TutorialShape.Kind.Triangle; nextArrow.color = settings.ringColor; nextArrow.raycastTarget = false;
            arrowRt.localRotation = Quaternion.Euler(0f, 0f, -90f); // pointing right

            rt.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ API

        /// <summary>Opens on a page: the dim layer fades in and the ring shrinks onto the highlight.</summary>
        public void Open(TutorialExplainContent page, SpotShape spot)
        {
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            sortCanvas.overrideSorting = true; // only sticks while the object is active
            sortCanvas.sortingOrder = SortingOrder;
            target = current = spot;
            skipProgress = 0f;
            SetPhase(Phase.In);
            SetPage(page);
            Apply(0f);
        }

        /// <summary>Next page of the same explanation: the highlight glides to the new target and the ring shrinks again.</summary>
        public void ShowPage(TutorialExplainContent page, SpotShape spot)
        {
            target = spot;
            SetPage(page);
        }

        /// <summary>Per-frame target of the highlight (screen position of a world object can change).</summary>
        public void SetSpot(SpotShape spot) => target = spot;

        public void SetSkipProgress(float value)
        {
            skipProgress = Mathf.Clamp01(value);
            if (holdRing != null) holdRing.SetProgress(skipProgress);
        }

        public void Close()
        {
            if (phase != Phase.Hidden && phase != Phase.Out)
                SetPhase(Phase.Out);
        }

        public void HideImmediate()
        {
            phase = Phase.Hidden;
            if (group != null) group.alpha = 0f;
            if (root != null) root.gameObject.SetActive(false);
        }

        /// <summary>Editor captures / tests: jump to the settled pose (ring at rest, bubble fully shown).</summary>
        public void SnapVisible()
        {
            if (phase == Phase.Hidden) return;
            phase = Phase.Shown; phaseTime = 10f; pageTime = 10f - 0.25f / Mathf.Max(0.01f, settings.ringPulseSpeed);
            current = target;
            Apply(0f);
        }

        // ------------------------------------------------------------------ animation

        private void SetPhase(Phase next) { phase = next; phaseTime = 0f; }

        private void SetPage(TutorialExplainContent page)
        {
            content = page;
            pageTime = 0f;
            Relayout();
        }

        private void Update() => Apply(Time.unscaledDeltaTime);

        private void Apply(float dt)
        {
            if (phase == Phase.Hidden)
                return;
            phaseTime += dt; pageTime += dt;
            float alpha = 1f;
            if (phase == Phase.In)
            {
                alpha = Mathf.Clamp01(phaseTime / settings.fadeInSeconds);
                if (alpha >= 1f) phase = Phase.Shown;
            }
            else if (phase == Phase.Out)
            {
                alpha = 1f - Mathf.Clamp01(phaseTime / settings.fadeOutSeconds);
                if (alpha <= 0f) { HideImmediate(); return; }
            }
            group.alpha = alpha;

            if (TutorialUi.Language != shownLanguage || Device != shownDevice)
                Relayout();

            current = dt <= 0f ? target : SpotShape.Lerp(current, target, 1f - Mathf.Exp(-settings.spotFollow * dt));
            float k = Mathf.Clamp01(pageTime / settings.ringShrinkSeconds);
            float ease = 1f - Mathf.Pow(1f - k, 3f);
            float offset = Mathf.Lerp(settings.ringStartOffset, settings.ringGap, ease);
            if (k >= 1f)
                offset += Mathf.Sin((pageTime - settings.ringShrinkSeconds) * settings.ringPulseSpeed * Mathf.PI * 2f) * settings.ringPulsePixels;
            overlay.Set(current, settings.overlayAlpha, settings.featherPixels, settings.ringColor, settings.ringWidth, offset, Mathf.Clamp01(k * 2.5f));

            float pop = Mathf.Clamp01((pageTime - 0.12f) / 0.18f);
            bubbleGroup.alpha = pop;
            bubble.localScale = Vector3.one * Mathf.Lerp(.97f, 1f, pop);
            tailShape.color = new Color(settings.bubbleColor.r, settings.bubbleColor.g, settings.bubbleColor.b, settings.bubbleColor.a * pop);
            tailShape.edge = new Color(.86f, .70f, .42f, .9f * pop);

            bool ready = pageTime >= settings.minReadSeconds;
            nextGroup.alpha = ready ? 1f : .3f;
            float bob = ready ? Mathf.Abs(Mathf.Sin(pageTime * 4.2f)) * 6f : 0f;
            ((RectTransform)nextArrow.transform).anchoredPosition = new Vector2(nextRowWidth - 9f + bob, 0f);

            if ((target.center - laidOutFor).sqrMagnitude > 4f)
                PlaceBubble();
        }

        private static InputDeviceKind Device => TutorialUi.Input != null ? TutorialUi.Input.ActiveDevice : InputDeviceKind.Keyboard;

        // ------------------------------------------------------------------ layout

        private void Relayout()
        {
            if (root == null)
                return;
            shownLanguage = TutorialUi.Language; shownDevice = Device;
            var font = TutorialUi.Font;
            title.font = body.font = pageText.font = skipText.font = nextText.font = font;
            title.text = TutorialUi.Lookup(content.titleKey);
            body.text = TutorialUi.Lookup(content.bodyKey);
            bool paged = content.pageCount > 1;
            pageText.text = paged ? (content.page + 1) + " / " + content.pageCount : "";
            skipText.text = TutorialUi.Lookup("tutorial.skip");
            nextText.text = TutorialUi.Lookup(content.page + 1 < content.pageCount ? "tutorial.next" : "tutorial.continue");

            const float padX = 30f, padTop = 24f, padBottom = 18f, footerH = 40f;
            float width = Mathf.Min(settings.bubbleWidth, root.rect.width - 48f);
            float inner = width - padX * 2f;
            float titleH = TutorialUi.PreferredHeight(title, inner - (paged ? 90f : 0f));
            float bodyH = TutorialUi.PreferredHeight(body, inner);
            TutorialUi.DestroyAll(glyphObjects);
            float glyphW = TutorialUi.BuildGlyphRow(glyphRow, content.glyphs, font, settings.glyphHeight, glyphObjects);
            float glyphH = glyphW > 0f ? settings.glyphHeight + 18f : 0f;

            Vector2 tl = new Vector2(0f, 1f);
            float y = -padTop;
            GoldUiTheme.Place(title.rectTransform, tl, tl, new Vector2(padX, y), new Vector2(inner - (paged ? 90f : 0f), titleH));
            GoldUiTheme.Place(pageText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-padX, y), new Vector2(90f, titleH));
            y -= titleH + 10f;
            GoldUiTheme.Place(body.rectTransform, tl, tl, new Vector2(padX, y), new Vector2(inner, bodyH));
            y -= bodyH;
            if (glyphW > 0f)
            {
                y -= 18f;
                GoldUiTheme.Place(glyphRow, tl, tl, new Vector2(padX, y), new Vector2(glyphW, settings.glyphHeight));
                y -= settings.glyphHeight;
            }
            float bodyTop = -y - padTop; // height of title + body + keys
            y -= 18f;
            GoldUiTheme.Place(divider, tl, tl, new Vector2(padX, y), new Vector2(inner, 1f));

            // Footer, left: hold ring + cancel key + "hold to skip". Right: "continue" + confirm key + arrow.
            const float small = 32f;
            TutorialUi.DestroyAll(skipObjects);
            GoldUiTheme.Place((RectTransform)holdRing.transform, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(30f, 30f));
            var skipKeys = GoldUiTheme.Rect("Keys", skipRow); skipObjects.Add(skipKeys);
            float skipKeyW = TutorialUi.BuildGlyphRow(skipKeys, new[] { TutorialGlyph.Cancel }, font, small, skipObjects);
            GoldUiTheme.Place(skipKeys, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(38f, 0f), new Vector2(skipKeyW, small));
            float skipTextW = TutorialUi.PreferredWidth(skipText) + 4f;
            float skipRowWidth = 38f + skipKeyW + 10f + skipTextW;
            GoldUiTheme.Place(skipText.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(38f + skipKeyW + 10f, 0f), new Vector2(skipTextW, footerH));

            TutorialUi.DestroyAll(nextObjects);
            var nextKeys = GoldUiTheme.Rect("Keys", nextRow); nextObjects.Add(nextKeys);
            float nextKeyW = TutorialUi.BuildGlyphRow(nextKeys, new[] { TutorialGlyph.Confirm }, font, small + 4f, nextObjects);
            float nextTextW = TutorialUi.PreferredWidth(nextText) + 4f;
            nextRowWidth = nextTextW + 10f + nextKeyW + 14f + 18f + 8f;
            // Long languages / narrow bubbles: the two hints do not fit side by side, so "next" goes on its own row above.
            bool stacked = skipRowWidth + 28f + nextRowWidth > inner;
            float footerTotal = stacked ? footerH * 2f + 4f : footerH;
            GoldUiTheme.Place(skipRow, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(padX, padBottom), new Vector2(skipRowWidth, footerH));
            GoldUiTheme.Place(nextRow, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-padX, padBottom + (stacked ? footerH + 4f : 0f)), new Vector2(nextRowWidth, footerH));
            GoldUiTheme.Place(nextText.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), Vector2.zero, new Vector2(nextTextW, footerH));
            GoldUiTheme.Place(nextKeys, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(nextTextW + 10f, 0f), new Vector2(nextKeyW, small + 4f));
            GoldUiTheme.Place((RectTransform)nextArrow.transform, new Vector2(0f, .5f), new Vector2(.5f, .5f), new Vector2(nextRowWidth - 9f, 0f), new Vector2(20f, 18f));

            float height = padTop + bodyTop + 18f + 1f + 10f + footerTotal + padBottom;
            bubbleSize = new Vector2(width, Mathf.Ceil(height));
            bubble.sizeDelta = bubbleSize;

            PlaceBubble();
        }

        /// <summary>Puts the bubble beside the highlight (never over it) with its tail toward it.</summary>
        private void PlaceBubble()
        {
            laidOutFor = target.center;
            Rect r = root.rect;
            const float margin = 24f, tailH = 16f, tailW = 30f;
            float w = bubbleSize.x, h = bubbleSize.y;
            float ext = settings.ringGap + settings.ringWidth + settings.ringPulsePixels + 6f;
            float left = target.center.x - target.half.x - ext, right = target.center.x + target.half.x + ext;
            float bottom = target.center.y - target.half.y - ext, top = target.center.y + target.half.y + ext;
            float gap = settings.bubbleGap + tailH;
            float cxClamped = Mathf.Clamp(target.center.x, r.xMin + margin + w * .5f, r.xMax - margin - w * .5f);
            float cyClamped = Mathf.Clamp(target.center.y, r.yMin + margin + h * .5f, r.yMax - margin - h * .5f);

            bool fitsBelow = bottom - gap - h >= r.yMin + margin;
            bool fitsAbove = top + gap + h <= r.yMax - margin;
            bool fitsRight = right + gap + w <= r.xMax - margin;
            bool fitsLeft = left - gap - w >= r.xMin + margin;
            Side side = preferAbove
                ? (fitsAbove ? Side.Above : fitsBelow ? Side.Below : fitsRight ? Side.Right : fitsLeft ? Side.Left : Side.None)
                : (fitsBelow ? Side.Below : fitsAbove ? Side.Above : fitsRight ? Side.Right : fitsLeft ? Side.Left : Side.None);

            Vector2 pos, tailPos = Vector2.zero; float tailRot = 0f;
            switch (side)
            {
                case Side.Below:
                    pos = new Vector2(cxClamped, bottom - gap - h * .5f);
                    tailPos = new Vector2(Mathf.Clamp(target.center.x, pos.x - w * .5f + 44f, pos.x + w * .5f - 44f), pos.y + h * .5f - 2f); tailRot = 0f;
                    break;
                case Side.Above:
                    pos = new Vector2(cxClamped, top + gap + h * .5f);
                    tailPos = new Vector2(Mathf.Clamp(target.center.x, pos.x - w * .5f + 44f, pos.x + w * .5f - 44f), pos.y - h * .5f + 2f); tailRot = 180f;
                    break;
                case Side.Right:
                    pos = new Vector2(right + gap + w * .5f, cyClamped);
                    tailPos = new Vector2(pos.x - w * .5f + 2f, Mathf.Clamp(target.center.y, pos.y - h * .5f + 44f, pos.y + h * .5f - 44f)); tailRot = 90f;
                    break;
                case Side.Left:
                    pos = new Vector2(left - gap - w * .5f, cyClamped);
                    tailPos = new Vector2(pos.x + w * .5f - 2f, Mathf.Clamp(target.center.y, pos.y - h * .5f + 44f, pos.y + h * .5f - 44f)); tailRot = -90f;
                    break;
                default:
                    // No free side (huge highlight): bottom centre, no tail.
                    pos = new Vector2(0f, r.yMin + margin + h * .5f);
                    break;
            }
            bubble.anchoredPosition = pos;
            tail.gameObject.SetActive(side != Side.None);
            tail.pivot = new Vector2(.5f, 0f);
            tail.sizeDelta = new Vector2(tailW, tailH);
            tail.anchoredPosition = tailPos;
            tail.localRotation = Quaternion.Euler(0f, 0f, tailRot);
        }
    }
}
