using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoiceUnlockGraphic : MaskableGraphic
    {
        private static readonly Color Brass = new Color(.92f, .66f, .29f);
        private static readonly Color Light = new Color(1, .90f, .65f);
        private float time;

        public void Sample(float seconds)
        {
            time = seconds;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            float form = Mathf.SmoothStep(0, 1, time / .56f);
            float release = Mathf.Clamp01((time - .54f) / .83f);
            float flash = Mathf.Exp(-Mathf.Pow((time - .62f) / .13f, 2));
            ShadeBehindCopy(mesh);
            Halo(mesh, new Vector2(365, 170), Opacity(Brass, .14f * form));
            if (release < 1) Ribbons(mesh, form, 1 - release);
            for (int side = -1; side <= 1; side += 2)
            {
                Line(mesh, Vector2.right * side * 115, Vector2.right * side * (115 + form * 430), 1, Opacity(Brass, .54f * form));
                GoldUiFrame.Gem(mesh, Vector2.right * side * 146, 3, Opacity(Light, form));
            }

            float radius = Mathf.Lerp(37, 108, form);
            float angle = Mathf.Lerp(32, 0, form);
            GoldUiFrame.Gem(mesh, Vector2.zero, radius, new Color(.086f, .070f, .064f, form * .97f));
            Polygon(mesh, 4, radius, angle, 2.3f, Opacity(Light, form));
            Polygon(mesh, 4, radius + 9, -angle, 1, Opacity(Brass, form * .78f));
            Polygon(mesh, 8, radius - 5, 22.5f + angle, 1, Opacity(Brass, form * .65f));
            for (int i = 0; i < 4; i++)
                GoldUiFrame.Gem(mesh, Polar(90 * i) * (radius + 9), 4, Opacity(Light, form));
            Microphone(mesh, form);

            if (release > 0 && release < 1)
            {
                float spread = 1 - (1 - release) * (1 - release);
                for (int i = 0; i < 16; i++)
                {
                    float angleOnRing = i * 22.5f;
                    // The tiles travel along the four edges of an expanding diamond.
                    var direction = Polar(angleOnRing);
                    var edge = direction / (Mathf.Abs(direction.x) + Mathf.Abs(direction.y));
                    var center = edge * (76 + spread * 195);
                    GoldUiFrame.Gem(mesh, center, (17 + i % 2 * 12) * (1 - release), Opacity(Brass, (1 - release) * .78f));
                }
            }
            Halo(mesh, new Vector2(183, 149) * (1 + flash * .28f), Opacity(Light, flash * .90f));
            for (int i = 0; i < 33; i++)
            {
                float drift = Mathf.Sin(time * .83f + i * 3.2f);
                var point = Polar(i * 137.51f) * (115 + (i * 53 % 174) + 38 * release);
                point.x *= 1.65f;
                point.y = point.y * .5f + drift * 5;
                GoldUiFrame.Gem(mesh, point, i % 6 == 0 ? 3.2f : 1.5f,
                    Opacity(Light, release * (.31f + .23f * drift + .40f * (1 - release))));
            }
        }

        private void Ribbons(VertexHelper mesh, float form, float fade)
        {
            for (int arm = 0; arm < 6; arm++)
            {
                for (int i = 0; i < 28; i++)
                {
                    float t = i / 28f, next = (i + 1) / 28f;
                    var a = RibbonPoint(arm, t, form);
                    var b = RibbonPoint(arm, next, form);
                    Line(mesh, a, b, (3 + 13 * t) * fade, Opacity(Brass, Mathf.Sin(t * Mathf.PI) * fade * form * .26f));
                }
            }
        }

        private static Vector2 RibbonPoint(int arm, float t, float form)
        {
            var p = Polar(arm * 60 + t * 102 + form * 48) * (26 + 250 * t * t);
            return new Vector2(p.x * 2.15f, p.y * .82f);
        }

        private void Microphone(VertexHelper mesh, float alpha)
        {
            Color ink = Opacity(Light, alpha);
            var top = new Vector2(0, 25);
            var bottom = new Vector2(0, -2);
            Arc(mesh, top, 14, 0, 180, 3.8f, ink);
            Arc(mesh, bottom, 14, 180, 180, 3.8f, ink);
            for (int side = -1; side <= 1; side += 2)
            {
                var edge = Vector2.right * 14 * side;
                Line(mesh, top + edge, bottom + edge, 3.8f, ink);
                Line(mesh, bottom + Vector2.right * 23 * side, new Vector2(23 * side, 7), 3, ink);
                for (int i = 0; i < 3; i++)
                {
                    float x = (38 + i * 7) * side;
                    float h = (i == 1 ? 14 : 6) * (.8f + .2f * Mathf.Sin(time * 3 + i));
                    Line(mesh, new Vector2(x, -h), new Vector2(x, h), 2.5f, Opacity(Brass, alpha));
                }
            }
            for (int y = 6; y <= 23; y += 7)
                Line(mesh, new Vector2(-8, y), new Vector2(8, y), 2, ink);
            Arc(mesh, bottom, 23, 180, 180, 3, ink);
            Line(mesh, new Vector2(0, -25), new Vector2(0, -36), 3, ink);
            Line(mesh, new Vector2(-12, -36), new Vector2(12, -36), 3, ink);
        }

        private static Color Opacity(Color color, float alpha) { color.a = alpha; return color; }
        private static Vector2 Polar(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        // Keep bright map markers from competing with the slot name and the confirmation hint.
        private static void ShadeBehindCopy(VertexHelper mesh)
        {
            Color shade = new Color(.045f, .037f, .038f, .94f);
            var center = new Vector2(0, -42);
            int start = mesh.currentVertCount;
            mesh.AddVert(center, shade, Vector2.zero);
            for (int i = 0; i <= 48; i++)
            {
                var direction = Polar(i * 7.5f);
                mesh.AddVert(center + Vector2.Scale(direction, new Vector2(218, 270)), shade, Vector2.zero);
                mesh.AddVert(center + Vector2.Scale(direction, new Vector2(565, 375)), Opacity(shade, 0), Vector2.zero);
                if (i == 0) continue;
                int inner = start + i * 2 + 1;
                mesh.AddTriangle(start, inner, inner - 2);
                mesh.AddTriangle(inner - 2, inner, inner + 1);
                mesh.AddTriangle(inner - 2, inner + 1, inner - 1);
            }
        }

        private static void Halo(VertexHelper mesh, Vector2 size, Color center)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(Vector2.zero, center, Vector2.zero);
            for (int i = 0; i <= 48; i++)
            {
                mesh.AddVert(Vector2.Scale(Polar(i * 7.5f), size), Opacity(center, 0), Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i + 1, start + i);
            }
        }

        private static void Polygon(VertexHelper mesh, int sides, float radius, float angle, float width, Color ink)
        {
            for (int i = 0; i < sides; i++)
                Line(mesh, Polar(angle + 360f * i / sides) * radius, Polar(angle + 360f * (i + 1) / sides) * radius, width, ink);
        }

        private static void Arc(VertexHelper mesh, Vector2 center, float r, float start, float sweep, float width, Color ink)
        {
            for (int i = 0; i < 16; i++)
                Line(mesh, center + Polar(start + sweep * i / 16) * r, center + Polar(start + sweep * (i + 1) / 16) * r, width, ink);
        }

        private static void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width, Color ink)
        {
            Vector2 d = (b - a).normalized;
            var offset = new Vector2(-d.y, d.x) * (width * .5f);
            int n = mesh.currentVertCount;
            mesh.AddVert(a - offset, ink, Vector2.zero);
            mesh.AddVert(a + offset, ink, Vector2.zero);
            mesh.AddVert(b + offset, ink, Vector2.zero);
            mesh.AddVert(b - offset, ink, Vector2.zero);
            mesh.AddTriangle(n, n + 1, n + 2);
            mesh.AddTriangle(n, n + 2, n + 3);
        }
    }
}
