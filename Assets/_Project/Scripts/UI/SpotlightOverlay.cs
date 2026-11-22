using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Highlight hole in overlay-local pixels: a rounded rectangle; a circle is half.x = half.y = corner.</summary>
    public struct SpotShape
    {
        public Vector2 center;
        public Vector2 half;
        public float corner;

        public static SpotShape Circle(Vector2 center, float radius) => new SpotShape { center = center, half = new Vector2(radius, radius), corner = radius };
        public static SpotShape Rect(Vector2 center, Vector2 half, float corner) => new SpotShape { center = center, half = half, corner = Mathf.Min(corner, Mathf.Min(half.x, half.y)) };

        public static SpotShape Lerp(SpotShape a, SpotShape b, float t) => new SpotShape
        {
            center = Vector2.Lerp(a.center, b.center, t), half = Vector2.Lerp(a.half, b.half, t), corner = Mathf.Lerp(a.corner, b.corner, t),
        };
    }

    /// <summary>
    /// Full-screen dim layer with one soft-edged hole (circle or rounded rectangle) and a thin ring around it.
    /// Pure procedural mesh (no texture, no custom shader), so it works in Screen Space - Overlay at any resolution.
    /// It is a raycast target over the whole screen: nothing under it can be clicked while it is up.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SpotlightOverlay : MaskableGraphic
    {
        private const int CornerSegments = 16;
        private const float Far = 8000f; // beyond any screen edge

        private SpotShape shape;
        private float dimAlpha = .7f, feather = 24f;
        private Color ringColor = Color.white;
        private float ringWidth = 4f, ringOffset = 8f, ringAlpha = 1f;

        private static readonly Vector2[] Points = new Vector2[4 * (CornerSegments + 1)];
        private static readonly Vector2[] Normals = new Vector2[4 * (CornerSegments + 1)];

        public void Set(SpotShape hole, float dim, float featherPixels, Color ring, float ringWidthPixels, float ringOffsetPixels, float ringOpacity)
        {
            shape = hole; dimAlpha = dim; feather = Mathf.Max(0f, featherPixels);
            ringColor = ring; ringWidth = ringWidthPixels; ringOffset = ringOffsetPixels; ringAlpha = ringOpacity;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // Outline of the hole, counter-clockwise, with outward normals.
            float cr = Mathf.Max(0f, Mathf.Min(shape.corner, Mathf.Min(shape.half.x, shape.half.y)));
            float ix = shape.half.x - cr, iy = shape.half.y - cr;
            int n = 0;
            for (int k = 0; k < 4; k++)
            {
                Vector2 cc = shape.center + new Vector2(k == 0 || k == 3 ? ix : -ix, k < 2 ? iy : -iy);
                for (int s = 0; s <= CornerSegments; s++)
                {
                    float ang = (k * 90f + s * 90f / CornerSegments) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                    Points[n] = cc + dir * cr;
                    Normals[n] = dir;
                    n++;
                }
            }

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color dim = new Color(0f, 0f, 0f, dimAlpha * color.a);
            Band(vh, n, 0f, feather, clear, dim);   // soft edge
            Band(vh, n, feather, Far, dim, dim);    // the rest of the screen

            if (ringAlpha > 0.001f && ringWidth > 0f)
            {
                Color ring = ringColor; ring.a *= ringAlpha * color.a;
                Color ringClear = ring; ringClear.a = 0f;
                Band(vh, n, ringOffset - 1.5f, ringOffset, ringClear, ring);
                Band(vh, n, ringOffset, ringOffset + ringWidth, ring, ring);
                Band(vh, n, ringOffset + ringWidth, ringOffset + ringWidth + 1.5f, ring, ringClear);
            }
        }

        /// <summary>Closed strip between the outline pushed out by d0 and by d1.</summary>
        private static void Band(VertexHelper vh, int n, float d0, float d1, Color c0, Color c1)
        {
            int first = vh.currentVertCount;
            for (int i = 0; i < n; i++)
            {
                vh.AddVert(Points[i] + Normals[i] * d0, c0, Vector2.zero);
                vh.AddVert(Points[i] + Normals[i] * d1, c1, Vector2.zero);
            }
            for (int i = 0; i < n; i++)
            {
                int a = first + i * 2, b = first + ((i + 1) % n) * 2;
                vh.AddTriangle(a, a + 1, b + 1);
                vh.AddTriangle(a, b + 1, b);
            }
        }
    }
}
