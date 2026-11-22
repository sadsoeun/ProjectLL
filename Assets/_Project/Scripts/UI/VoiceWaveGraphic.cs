using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// Voice Studio waveform: vertical bars of a recording (SetSamples) or of the live microphone level (Push), with the
    /// part before <see cref="SetProgress"/> drawn in the accent colour (playback / recording position).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoiceWaveGraphic : MaskableGraphic
    {
        private const int Bars = 96;
        private readonly float[] heights = new float[Bars];
        private int liveCount;
        private float progress;
        public Color accent = new Color(1f, .77f, .35f, 1f);

        /// <summary>Shows a whole recording (peak per bar). Null / empty clears the display.</summary>
        public void SetSamples(float[] samples)
        {
            liveCount = 0;
            for (int b = 0; b < Bars; b++)
            {
                float peak = 0f;
                if (samples != null && samples.Length > 0)
                {
                    int from = (int)((long)b * samples.Length / Bars), to = Mathf.Max(from + 1, (int)((long)(b + 1) * samples.Length / Bars));
                    for (int i = from; i < to && i < samples.Length; i++)
                        peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
                }
                heights[b] = peak;
            }
            progress = 0f;
            SetVerticesDirty();
        }

        /// <summary>Live recording: the bar at <paramref name="fraction"/> (0..1 of the take) takes the current level.</summary>
        public void Push(float fraction, float level)
        {
            int bar = Mathf.Clamp(Mathf.FloorToInt(fraction * Bars), 0, Bars - 1);
            for (int b = liveCount; b <= bar; b++)
                heights[b] = 0f;
            heights[bar] = Mathf.Max(heights[bar], Mathf.Clamp01(level));
            liveCount = Mathf.Max(liveCount, bar + 1);
            for (int b = liveCount; b < Bars; b++)
                heights[b] = 0f;
            progress = fraction;
            SetVerticesDirty();
        }

        public void SetProgress(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Approximately(fraction, progress))
                return;
            progress = fraction;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            Color guide = new Color(.38f, .40f, .39f, .30f);
            for (int i = 0; i <= 8; i++)
            {
                float x = r.xMin + (r.width - 1) * i / 8;
                float tick = i % 2 == 0 ? 9 : 5;
                GoldUiFrame.Quad(vh, x, r.yMin, 1, tick, guide);
                GoldUiFrame.Quad(vh, x, r.yMax - tick, 1, tick, guide);
            }
            float step = r.width / Bars, width = Mathf.Max(1f, step * .62f), mid = r.center.y, half = r.height * .5f - 4f;
            GoldUiFrame.Quad(vh, r.xMin, mid, r.width, 1, new Color(.64f,.58f,.45f,.28f));
            for (int b = 0; b < Bars; b++)
            {
                float h = Mathf.Max(2f, Mathf.Sqrt(Mathf.Clamp01(heights[b])) * half); // sqrt: quiet parts stay visible
                float x = r.xMin + step * (b + .5f);
                Color c = (b + .5f) / Bars <= progress ? accent : color;
                Color baseTint = Color.Lerp(c, new Color(.30f,.285f,.25f), .57f);
                int i = vh.currentVertCount;
                vh.AddVert(new Vector3(x - width * .5f, mid - h), baseTint, Vector2.zero);
                vh.AddVert(new Vector3(x - width * .5f, mid + h), c, Vector2.zero);
                vh.AddVert(new Vector3(x + width * .5f, mid + h), c, Vector2.zero);
                vh.AddVert(new Vector3(x + width * .5f, mid - h), baseTint, Vector2.zero);
                vh.AddTriangle(i, i + 1, i + 2);
                vh.AddTriangle(i, i + 2, i + 3);
            }
            if (progress > 0 && progress < 1)
            {
                float x = Mathf.Lerp(r.xMin, r.xMax, progress);
                GoldUiFrame.Quad(vh, x, r.yMin+3, 1.5f, r.height-6, accent);
                GoldUiFrame.Gem(vh, new Vector2(x,r.yMax-3), 3.5f, accent);
            }
        }
    }
}
