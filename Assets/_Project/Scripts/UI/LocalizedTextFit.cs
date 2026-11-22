using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Shrinks overflowing translations within the existing label, never above the design size.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Text))]
    public sealed class LocalizedTextFit : MonoBehaviour
    {
        [SerializeField] private int designSize;
        private Text label;
        private readonly TextGenerator measure = new TextGenerator();
        private string lastText;
        private Font lastFont;
        private Vector2 lastSize;
        private FontStyle lastStyle;
        private float lastSpacing, lastPixels;
        private bool lastRichText, lastSingleLine, dirty = true, fitting;
        private HorizontalWrapMode lastWrap;
        private int fittedSize;
        public int DesignSize => designSize;

        public static LocalizedTextFit Attach(Text text, int baseSize = 0)
        {
            if (text == null) return null;
            var fit = text.GetComponent<LocalizedTextFit>() ?? text.gameObject.AddComponent<LocalizedTextFit>();
            fit.label = text;
            int size = baseSize > 0 ? baseSize : fit.designSize > 0 ? fit.designSize : text.fontSize;
            if (fit.designSize != size) { fit.designSize = Mathf.Max(1, size); fit.dirty = true; }
            fit.FitNow();
            return fit;
        }

        private void OnEnable() { dirty = true; Canvas.willRenderCanvases += FitNow; }
        private void OnDisable() { Canvas.willRenderCanvases -= FitNow; }
        private void LateUpdate() => FitNow();
        private void OnRectTransformDimensionsChange() { dirty = true; }

        public void FitNow()
        {
            if (fitting) return;
            if (label == null) label = GetComponent<Text>();
            if (label == null || label.font == null) return;
            if (designSize <= 0) designSize = Mathf.Max(1, label.fontSize);
            Vector2 size = label.rectTransform.rect.size;
            if (size.x <= 2 || size.y <= 2) return;
            bool singleLine = label.GetComponentInParent<Button>() != null;
            // Long action labels must shrink, rather than wrapping the final syllable onto another line.
            if (singleLine) label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.resizeTextForBestFit = false;
            if (!dirty && lastText == label.text && lastFont == label.font && lastSize == size &&
                lastStyle == label.fontStyle && lastSpacing == label.lineSpacing &&
                lastRichText == label.supportRichText && lastWrap == label.horizontalOverflow &&
                lastPixels == label.pixelsPerUnit && lastSingleLine == singleLine && label.fontSize == fittedSize) return;

            fitting = true;
            try
            {
                // Preserve existing margins; reserve two additional design units for raster rounding.
                float width = size.x - 2, height = size.y - 2;
                int low = Mathf.Min(12, designSize), high = designSize, best = low;
                while (low <= high)
                {
                    int candidate = (low + high) / 2;
                    if (Fits(candidate, width, height, singleLine)) { best = candidate; low = candidate + 1; }
                    else high = candidate - 1;
                }
                fittedSize = string.IsNullOrEmpty(label.text) ? designSize : best;
                if (label.fontSize != fittedSize) label.fontSize = fittedSize;
                lastText = label.text; lastFont = label.font; lastSize = size;
                lastStyle = label.fontStyle; lastSpacing = label.lineSpacing;
                lastRichText = label.supportRichText; lastWrap = label.horizontalOverflow;
                lastPixels = label.pixelsPerUnit; lastSingleLine = singleLine;
                dirty = false;
            }
            finally { fitting = false; }
        }

        private bool Fits(int fontSize, float width, float height, bool singleLine)
        {
            float pixels = Mathf.Max(.001f, label.pixelsPerUnit);
            var settings = label.GetGenerationSettings(new Vector2(width, height));
            settings.fontSize = fontSize;
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.horizontalOverflow = singleLine ? HorizontalWrapMode.Overflow : label.horizontalOverflow;
            if (singleLine || settings.horizontalOverflow == HorizontalWrapMode.Overflow)
                if (measure.GetPreferredWidth(label.text, settings) / pixels > width) return false;
            return measure.GetPreferredHeight(label.text, settings) / pixels <= height;
        }
    }
}
