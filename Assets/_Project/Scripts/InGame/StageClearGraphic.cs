using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Animated gold filigree and deterministic sparks in a single uGUI mesh.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StageClearGraphic : MaskableGraphic
    {
        private float progress;
        private static readonly Color Gold = new Color(1f, 0.73f, 0.28f);
        private static readonly Color PaleGold = new Color(1f, 0.94f, 0.72f);
        public float Progress
        {
            get => progress;
            set { progress = Mathf.Clamp01(value); SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float reveal = Mathf.SmoothStep(0f, 1f, progress / 0.25f);
            float burst = Mathf.Clamp01((progress - 0.07f) / 0.22f);
            float flash = Mathf.Sin(burst * Mathf.PI) * 0.8f;
            // Soft radial illumination: transparent perimeter avoids a hard rectangular glow.
            Glow(vh, new Vector2(420f, 190f), Tint(Gold, 0.09f + flash * 0.15f));
            Glow(vh, new Vector2(520f * reveal, 16f), Tint(PaleGold, flash * 0.7f));

            for (int i = 0; i < 16; i++)
            {
                float angle = (i * 22.5f + 7f) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float length = Mathf.Lerp(120f, 370f, burst);
                Vector2 side = new Vector2(-direction.y, direction.x) * 9f;
                Triangle(vh, direction * 80f, direction * length + side, direction * length - side,
                    Tint(Gold, (1f - burst) * flash * 0.2f), Color.clear, Color.clear);
            }

            // Split triangle around the text band, with a second inset hairline.
            float scale = Mathf.Lerp(0.78f, 1f, reveal);
            for (int border = 0; border < 2; border++)
            {
                float k = scale * (border == 0 ? 1f : 0.91f);
                Color line = Tint(Gold, reveal * (border == 0 ? 0.9f : 0.35f));
                float width = border == 0 ? 2.6f : 1.2f;
                Segment(vh, new Vector2(0f, 232f) * k, new Vector2(-96f, 82f) * k, width, line);
                Segment(vh, new Vector2(0f, 232f) * k, new Vector2(96f, 82f) * k, width, line);
                Segment(vh, new Vector2(-198f, -78f) * k, new Vector2(-236f, -138f) * k, width, line);
                Segment(vh, new Vector2(198f, -78f) * k, new Vector2(236f, -138f) * k, width, line);
                Segment(vh, new Vector2(-236f, -138f) * k, new Vector2(236f, -138f) * k, width, line);
            }
            Diamond(vh, new Vector2(0f, 249f), 7f, 11f, Tint(PaleGold, reveal));
            Diamond(vh, new Vector2(0f, -162f), 5f, 8f, Tint(PaleGold, reveal));
            for (int side = -1; side <= 1; side += 2)
            {
                Segment(vh, new Vector2(side * 65f, -162f), new Vector2(side * 205f * reveal, -162f), 1.3f, Tint(Gold, reveal * 0.6f));
                for (int j = 0; j < 4; j++)
                    Diamond(vh, new Vector2(side * (25f + j * 18f), -162f), 2.4f, 3.8f, Tint(Gold, reveal * 0.7f));
                Vector2 tip = new Vector2(side * (500f + 20f * (1f - reveal)), 0f);
                Triangle(vh, tip, tip + new Vector2(-side * 18f, 9f), tip + new Vector2(-side * 18f, -9f),
                    Tint(PaleGold, reveal), Tint(Gold, reveal * 0.6f), Tint(Gold, reveal * 0.6f));
                Segment(vh, new Vector2(side * 220f, 94f), new Vector2(side * 420f * reveal, 94f), 1f, Tint(Gold, reveal * 0.35f));
            }

            // Seeds are mathematical, leaving Unity's gameplay Random state untouched.
            for (int i = 0; i < 56; i++)
            {
                float seed = Fraction(Mathf.Sin(i * 127.1f + 17f) * 43758.5453f);
                float seed2 = Fraction(Mathf.Sin(i * 311.7f + 53f) * 19341.13f);
                float age = (progress - 0.08f - seed * 0.13f) / (0.48f + seed2 * 0.3f);
                if (age <= 0f || age >= 1f) continue;
                float angle = (i * 137.508f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float travel = 1f - Mathf.Pow(1f - age, 2f);
                Vector2 point = dir * Mathf.Lerp(95f, 240f + seed * 260f, travel);
                point.y *= 0.66f;
                point.y += age * age * 38f;
                // Keep the localized title readable.
                if (Mathf.Abs(point.y) < 76f && Mathf.Abs(point.x) < 470f) point.y += Mathf.Sign(dir.y) * 90f;
                float alpha = Mathf.Sin(age * Mathf.PI) * (0.35f + seed2 * 0.65f);
                float size = 1.4f + seed2 * 3.4f;
                Diamond(vh, point, size, size * 1.6f, Tint(PaleGold, alpha));
                if (i % 7 == 0)
                {
                    Segment(vh, point - Vector2.right * size * 2.5f, point + Vector2.right * size * 2.5f, 0.8f, Tint(PaleGold, alpha * 0.65f));
                    Segment(vh, point - Vector2.up * size * 3f, point + Vector2.up * size * 3f, 0.8f, Tint(PaleGold, alpha * 0.65f));
                }
            }
            // A short light sweep beneath the title, separate from its glyphs.
            float sweep = Mathf.Clamp01((progress - 0.3f) / 0.25f);
            float x = Mathf.Lerp(-430f, 430f, sweep);
            Diamond(vh, new Vector2(x, -64f), 85f, 2f, Tint(PaleGold, Mathf.Sin(sweep * Mathf.PI) * 0.9f));
        }

        private static float Fraction(float x) => x - Mathf.Floor(x);
        private static Color Tint(Color color, float alpha) { color.a = Mathf.Clamp01(alpha); return color; }
        private static void Vertex(VertexHelper vh, Vector2 position, Color color)
        {
            var v = UIVertex.simpleVert;
            v.position = position;
            v.color = color;
            vh.AddVert(v);
        }
        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            int n = vh.currentVertCount;
            Vertex(vh, a, ca); Vertex(vh, b, cb); Vertex(vh, c, cc);
            vh.AddTriangle(n, n + 1, n + 2);
        }
        private static void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 delta = b - a;
            if (delta.sqrMagnitude < 0.001f) return;
            Vector2 n = new Vector2(-delta.y, delta.x).normalized * (width * 0.5f);
            int start = vh.currentVertCount;
            Vertex(vh, a - n, color); Vertex(vh, a + n, color);
            Vertex(vh, b + n, color); Vertex(vh, b - n, color);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
        private static void Diamond(VertexHelper vh, Vector2 p, float w, float h, Color color)
        {
            Triangle(vh, p + Vector2.left * w, p + Vector2.up * h, p + Vector2.right * w, color, color, color);
            Triangle(vh, p + Vector2.left * w, p + Vector2.right * w, p + Vector2.down * h, color, color, color);
        }
        private static void Glow(VertexHelper vh, Vector2 radius, Color color)
        {
            const int segments = 48;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float b = (i + 1) * Mathf.PI * 2f / segments;
                Triangle(vh, Vector2.zero, new Vector2(Mathf.Cos(a) * radius.x, Mathf.Sin(a) * radius.y),
                    new Vector2(Mathf.Cos(b) * radius.x, Mathf.Sin(b) * radius.y), color, Color.clear, Color.clear);
            }
        }
    }
}
