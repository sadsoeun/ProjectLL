using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Draws the stage card's top rule and route position indicator.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldMapChartGraphic : MaskableGraphic
    {
        private static readonly Color Rule = new Color32(160, 144, 118, 150);
        private static readonly Color Amber = new Color32(255, 193, 90, 255);
        private float cursor = -1f;
        private int count;

        public void SetRoute(float position, int total)
        {
            if (Mathf.Approximately(cursor, position) && count == total) return;
            cursor = position;
            count = total;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect card = rectTransform.rect;
            if (card.width < 48 || card.height < 48) return;

            GoldUiFrame.Quad(mesh, card.xMin, card.yMax - 1.5f, card.width, 1.5f, Rule);
            GoldUiFrame.Quad(mesh, card.xMin, card.yMax - 2.5f, 54, 2.5f, Amber);
            DrawRoute(mesh, card);
        }

        private void DrawRoute(VertexHelper mesh, Rect card)
        {
            float left = card.xMin + 28, right = card.xMax - 28, y = card.yMin + 21;
            Segment(mesh, new Vector2(left, y), new Vector2(right, y), 1, Rule);
            for (int stop = 0; stop < count; stop++)
            {
                float x = Mathf.Lerp(left, right, count == 1 ? .5f : (float)stop / (count - 1));
                GoldUiFrame.Quad(mesh, x - 1, y - 4, 2, 8, Rule);
            }
            if (cursor < 0 || count == 0) return;
            float markerX = Mathf.Lerp(left, right, count == 1 ? .5f : cursor / (count - 1));
            var marker = new Vector2(markerX, y);
            LoadingTrailGraphic.Disc(mesh, marker, 8, Amber);
            LoadingTrailGraphic.Disc(mesh, marker, 5, new Color32(18, 19, 21, 255));
            GoldUiFrame.Gem(mesh, marker, 2.8f, Color.white);
        }

        internal static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color ink)
        {
            Vector2 direction = (b - a).normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 offset = normal * width * .5f;
            Vector2 edge = offset + normal * .8f;
            var clear = new Color(ink.r, ink.g, ink.b, 0);
            int start = mesh.currentVertCount;
            // Feather the edges so fine wires do not flicker as they cross pixels.
            mesh.AddVert(a - edge, clear, Vector2.zero);
            mesh.AddVert(b - edge, clear, Vector2.zero);
            mesh.AddVert(a - offset, ink, Vector2.zero);
            mesh.AddVert(b - offset, ink, Vector2.zero);
            mesh.AddVert(a + offset, ink, Vector2.zero);
            mesh.AddVert(b + offset, ink, Vector2.zero);
            mesh.AddVert(a + edge, clear, Vector2.zero);
            mesh.AddVert(b + edge, clear, Vector2.zero);
            for (int strip = start; strip <= start + 4; strip += 2)
            {
                mesh.AddTriangle(strip, strip + 2, strip + 3);
                mesh.AddTriangle(strip, strip + 3, strip + 1);
            }
        }
    }
}
