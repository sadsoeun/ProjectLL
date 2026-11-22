using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Engraved action symbols shared by the slot list and the recording monitor.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoiceSlotGlyph : MaskableGraphic
    {
        // Each pair of points is a separate stroke in a 48-unit seal.
        private static readonly Vector2[][] Strokes = {
            new Vector2[] { new(-10,-8),new(0,9), new(0,9),new(10,-3), new(-8,-12),new(7,-12), new(-12,-3),new(-6,5) },
            new Vector2[] { new(-12,6),new(5,6), new(-8,0),new(11,0), new(-12,-6),new(2,-6), new(4,9),new(12,1), new(12,1),new(5,-8) },
            new Vector2[] { new(0,13),new(4,3), new(4,3),new(12,0), new(12,0),new(4,-3), new(4,-3),new(0,-13), new(0,-13),new(-4,-3), new(-4,-3),new(-12,0), new(-12,0),new(-4,3), new(-4,3),new(0,13) },
            new Vector2[] { new(-13,2),new(-7,2), new(-7,2),new(-3,9), new(-3,9),new(2,-10), new(2,-10),new(6,1), new(6,1),new(13,1) },
            new Vector2[] { new(2,13),new(-7,0), new(-7,0),new(3,0), new(3,0),new(-2,-13), new(-2,-13),new(11,3) },
            new Vector2[] { new(-7,-12),new(-7,13), new(-7,11),new(10,8), new(10,8),new(3,2), new(3,2),new(-7,3) },
            new Vector2[] { new(-13,7),new(-8,-6), new(-8,-6),new(8,-6), new(8,-6),new(13,7), new(13,7),new(5,2), new(5,2),new(0,11), new(0,11),new(-5,2), new(-5,2),new(-13,7), new(-7,-10),new(7,-10) },
            new Vector2[] { new(-10,3),new(-4,3), new(-4,3),new(7,10), new(7,10),new(7,-8), new(7,-8),new(-4,-2), new(-4,-2),new(-10,-2), new(-10,-2),new(-10,3), new(-5,-3),new(-2,-10), new(11,3),new(14,5) }
        };
        private VoiceSlot slot;
        private bool medallion;
        private bool locked;
        private float reveal = 1;

        public void SetSlot(VoiceSlot value, bool large = false, bool isLocked = false)
        {
            slot = value;
            medallion = large;
            locked = isLocked;
            SetVerticesDirty();
        }

        public void SetReveal(float value)
        {
            if (Mathf.Approximately(value, reveal)) return;
            reveal = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var rect = rectTransform.rect;
            var center = rect.center;
            float scale = Mathf.Min(rect.width, rect.height) / 48;
            var fine = color;
            fine.a *= .56f;
            float radius = medallion ? 17 : 22;
            if (locked)
                DrawPadlock(mesh, center, scale);
            else
            {
                var points = Strokes[(int)slot];
                for (int i = 0; i < points.Length; i += 2)
                    Stroke(mesh, center + points[i] * scale * .9f, center + points[i+1] * scale * .9f, scale * (medallion ? .95f : 1.55f), color);
            }
            for (int side = 0; side < 4; side++)
            {
                float angle = side * 90 + (1 - reveal) * 28;
                Stroke(mesh, center + Unit(angle) * radius * scale, center + Unit(angle+90) * radius * scale, scale * .6f, fine);
            }
            if (!medallion) return;
            for (int i = 0; i < 64; i++)
            {
                float degrees = i * 360f / 64;
                var dir = Unit(degrees);
                var outer = center + dir * scale * 22.5f;
                Stroke(mesh, outer, center + Unit(degrees+360f/64) * scale * 22.5f, scale * .24f, fine);
                if (i % 2 == 0) Stroke(mesh, outer, outer - dir * scale * (i % 8 == 0 ? 2 : .85f), scale * .32f, fine);
            }
            for (int i = 0; i < 4; i++)
                GoldUiFrame.Gem(mesh, center + Unit(i*90) * radius * scale, 1.1f * scale, color);
        }

        private static Vector2 Unit(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        private void DrawPadlock(VertexHelper mesh, Vector2 center, float scale)
        {
            var arch = center + Vector2.up * (5 * scale);
            for (int i = 0; i < 12; i++)
                Stroke(mesh, arch + Unit(i * 15) * (6 * scale), arch + Unit((i + 1) * 15) * (6 * scale), 2.5f * scale, color);
            for (int side = -1; side <= 1; side += 2)
            {
                var foot = center + Vector2.right * (side * 6 * scale);
                Stroke(mesh, foot, foot + Vector2.up * (5 * scale), 2.5f * scale, color);
            }
            var body = center + Vector2.down * (4 * scale);
            Stroke(mesh, body - Vector2.right * (9 * scale), body + Vector2.right * (9 * scale), 12 * scale, color);
            GoldUiFrame.Gem(mesh, body + Vector2.up * scale, 2.1f * scale, GoldUiTheme.Panel);
            Stroke(mesh, body, body + Vector2.down * (3 * scale), 1.9f * scale, GoldUiTheme.Panel);
        }

        private static void Stroke(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color tint)
        {
            var direction = (b-a).normalized;
            var edge = new Vector2(-direction.y, direction.x) * (width * .5f);
            int n = mesh.currentVertCount;
            mesh.AddVert(a-edge, tint, Vector2.zero);
            mesh.AddVert(a+edge, tint, Vector2.zero);
            mesh.AddVert(b+edge, tint, Vector2.zero);
            mesh.AddVert(b-edge, tint, Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2);
            mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
