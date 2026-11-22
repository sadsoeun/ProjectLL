using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>How a binding is drawn. Everything is vector-drawn (no font glyph or vendor texture dependency).</summary>
    public enum GlyphShape
    {
        /// <summary>Keyboard keycap with a text legend.</summary>
        Key,
        /// <summary>Keycap with a drawn arrow (ArrowDirection).</summary>
        ArrowKey,
        /// <summary>Round face button with a letter (Xbox / Nintendo pads).</summary>
        FaceLetter,
        /// <summary>Round face button with a PlayStation symbol (FaceSymbol).</summary>
        FaceSymbol,
        /// <summary>Vendor-neutral face button: four dots, the bound position filled (FacePosition).</summary>
        FacePosition,
        /// <summary>Shoulder / trigger pill with a text legend.</summary>
        Pill,
        /// <summary>Analog stick (ring) with a text legend.</summary>
        Stick,
        /// <summary>Success tick: gold disc with a dark check mark (not a binding).</summary>
        Check,
    }

    public struct GlyphSpec
    {
        public GlyphShape shape;
        public string label;
        /// <summary>Arrow: 0 left, 1 right, 2 up, 3 down. FacePosition: 0 south, 1 east, 2 west, 3 north.
        /// FaceSymbol: 0 cross, 1 circle, 2 square, 3 triangle.</summary>
        public int variant;
    }

    /// <summary>Maps live binding paths to glyphs. Gamepad legends follow the connected pad's family; with an unknown
    /// pad (or none) face buttons use the neutral position icon and shoulders/triggers generic names.</summary>
    public static class InputGlyphLabels
    {
        private enum PadFamily { Unknown, Xbox, PlayStation, Nintendo }

        public static GlyphSpec Describe(string path)
        {
            if (string.IsNullOrEmpty(path))
                return new GlyphSpec { shape = GlyphShape.Key, label = "?" };
            int slash = path.IndexOf('/');
            string device = slash > 0 ? path.Substring(0, slash) : path;
            string control = slash > 0 ? path.Substring(slash + 1) : path;
            return device == "<Gamepad>" ? Pad(control) : Key(control);
        }

        private static GlyphSpec Key(string control)
        {
            switch (control)
            {
                case "leftArrow": return Arrow(0);
                case "rightArrow": return Arrow(1);
                case "upArrow": return Arrow(2);
                case "downArrow": return Arrow(3);
                case "space": return Text("Space");
                case "ctrl": case "leftCtrl": case "rightCtrl": return Text("Ctrl");
                case "shift": case "leftShift": case "rightShift": return Text("Shift");
                case "alt": case "leftAlt": case "rightAlt": return Text("Alt");
                case "enter": case "numpadEnter": return Text("Enter");
                case "escape": return Text("Esc");
                case "tab": return Text("Tab");
                case "backspace": return Text("Back");
            }
            if (control.Length == 1)
                return Text(control.ToUpperInvariant());
            if (control.StartsWith("digit") && control.Length == 6)
                return Text(control.Substring(5));
            return Text(InputControlPath.ToHumanReadableString("<Keyboard>/" + control, InputControlPath.HumanReadableStringOptions.OmitDevice));
        }

        private static GlyphSpec Pad(string control)
        {
            var family = Family();
            switch (control)
            {
                case "buttonSouth": return Face(family, 0);
                case "buttonEast": return Face(family, 1);
                case "buttonWest": return Face(family, 2);
                case "buttonNorth": return Face(family, 3);
                case "leftShoulder": return Pill(family == PadFamily.PlayStation ? "L1" : family == PadFamily.Nintendo ? "L" : "LB");
                case "rightShoulder": return Pill(family == PadFamily.PlayStation ? "R1" : family == PadFamily.Nintendo ? "R" : "RB");
                case "leftTrigger": return Pill(family == PadFamily.PlayStation ? "L2" : family == PadFamily.Nintendo ? "ZL" : "LT");
                case "rightTrigger": return Pill(family == PadFamily.PlayStation ? "R2" : family == PadFamily.Nintendo ? "ZR" : "RT");
                case "leftStick": return new GlyphSpec { shape = GlyphShape.Stick, label = "L" };
                case "rightStick": return new GlyphSpec { shape = GlyphShape.Stick, label = "R" };
                case "leftStickPress": return new GlyphSpec { shape = GlyphShape.Stick, label = "L3" };
                case "rightStickPress": return new GlyphSpec { shape = GlyphShape.Stick, label = "R3" };
                case "start": return Pill(family == PadFamily.PlayStation ? "Options" : family == PadFamily.Nintendo ? "+" : "Menu");
                case "select": return Pill(family == PadFamily.PlayStation ? "Share" : family == PadFamily.Nintendo ? "−" : "View");
                case "dpad": return Pill("D-Pad");
            }
            return Pill(InputControlPath.ToHumanReadableString("<Gamepad>/" + control, InputControlPath.HumanReadableStringOptions.OmitDevice));
        }

        private static GlyphSpec Face(PadFamily family, int position)
        {
            switch (family)
            {
                case PadFamily.Xbox: return new GlyphSpec { shape = GlyphShape.FaceLetter, label = new[] { "A", "B", "X", "Y" }[position] };
                case PadFamily.Nintendo: return new GlyphSpec { shape = GlyphShape.FaceLetter, label = new[] { "B", "A", "Y", "X" }[position] };
                case PadFamily.PlayStation: return new GlyphSpec { shape = GlyphShape.FaceSymbol, variant = position };
                default: return new GlyphSpec { shape = GlyphShape.FacePosition, variant = position };
            }
        }

        private static PadFamily Family()
        {
            var pad = Gamepad.current;
            if (pad == null)
                return PadFamily.Unknown;
            string layout = (pad.layout ?? "") + " " + (pad.description.product ?? "") + " " + (pad.description.manufacturer ?? "");
            layout = layout.ToLowerInvariant();
            if (layout.Contains("dualshock") || layout.Contains("dualsense") || layout.Contains("sony") || layout.Contains("playstation"))
                return PadFamily.PlayStation;
            if (layout.Contains("switch") || layout.Contains("nintendo"))
                return PadFamily.Nintendo;
            if (layout.Contains("xinput") || layout.Contains("xbox"))
                return PadFamily.Xbox;
            return PadFamily.Unknown;
        }

        private static GlyphSpec Text(string label) => new GlyphSpec { shape = GlyphShape.Key, label = label };
        private static GlyphSpec Pill(string label) => new GlyphSpec { shape = GlyphShape.Pill, label = label };
        private static GlyphSpec Arrow(int direction) => new GlyphSpec { shape = GlyphShape.ArrowKey, variant = direction };
    }

    /// <summary>
    /// One key / button hint, drawn in the gold UI style (dark fill, fine gold rim). The legend is a child Text set by
    /// <see cref="TutorialPromptView"/>; this graphic draws only the cap and any symbol.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class InputGlyph : MaskableGraphic
    {
        private static readonly Color Fill = new Color(.10f, .095f, .085f, .96f);
        private static readonly Color Rim = new Color(1f, .80f, .45f, 1f);
        private static readonly Color Lip = new Color(0f, 0f, 0f, .45f);
        private static readonly Color Ink = new Color(1f, .94f, .78f, 1f);

        public GlyphSpec spec;

        public void Set(GlyphSpec value)
        {
            spec = value;
            raycastTarget = false;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float a = color.a;
            Color fill = Fill, rim = Rim, lip = Lip, ink = Ink;
            fill.a *= a; rim.a *= a; lip.a *= a; ink.a *= a;
            switch (spec.shape)
            {
                case GlyphShape.Key:
                case GlyphShape.ArrowKey:
                {
                    float radius = r.height * .2f;
                    RoundRect(vh, new Rect(r.x, r.y - 3f, r.width, r.height), radius, lip); // bottom lip
                    RoundRect(vh, r, radius, rim);
                    RoundRect(vh, Inset(r, 2f), radius - 2f, fill);
                    if (spec.shape == GlyphShape.ArrowKey)
                        ArrowSymbol(vh, r.center, r.height * .2f, spec.variant, ink);
                    break;
                }
                case GlyphShape.Pill:
                {
                    float radius = r.height * .5f;
                    RoundRect(vh, r, radius, rim);
                    RoundRect(vh, Inset(r, 2f), radius - 2f, fill);
                    break;
                }
                case GlyphShape.FaceLetter:
                case GlyphShape.FaceSymbol:
                {
                    float radius = Mathf.Min(r.width, r.height) * .5f;
                    Disc(vh, r.center, radius, rim);
                    Disc(vh, r.center, radius - 2f, fill);
                    if (spec.shape == GlyphShape.FaceSymbol)
                        PadSymbol(vh, r.center, radius * .42f, spec.variant, ink);
                    break;
                }
                case GlyphShape.Stick:
                {
                    float radius = Mathf.Min(r.width, r.height) * .5f;
                    Disc(vh, r.center, radius, rim);
                    Disc(vh, r.center, radius - 2f, fill);
                    Ring(vh, r.center, radius * .72f, 1.5f, new Color(rim.r, rim.g, rim.b, rim.a * .55f));
                    break;
                }
                case GlyphShape.Check:
                {
                    float radius = Mathf.Min(r.width, r.height) * .5f;
                    Disc(vh, r.center, radius, rim);
                    var dark = new Color(.05f, .045f, .03f, a);
                    float w = Mathf.Max(3f, radius * .2f);
                    Vector2 c = r.center;
                    Line(vh, c + new Vector2(-radius * .46f, radius * .02f), c + new Vector2(-radius * .10f, -radius * .36f), w, dark);
                    Line(vh, c + new Vector2(-radius * .14f, -radius * .36f), c + new Vector2(radius * .50f, radius * .36f), w, dark);
                    break;
                }
                case GlyphShape.FacePosition:
                {
                    float radius = Mathf.Min(r.width, r.height) * .5f;
                    Disc(vh, r.center, radius, rim);
                    Disc(vh, r.center, radius - 2f, fill);
                    float d = radius * .48f, dot = radius * .2f;
                    var offsets = new[] { Vector2.down, Vector2.right, Vector2.left, Vector2.up };
                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 p = r.center + offsets[i] * d;
                        if (i == spec.variant) Disc(vh, p, dot * 1.25f, rim);
                        else Ring(vh, p, dot, 1.3f, new Color(ink.r, ink.g, ink.b, ink.a * .6f));
                    }
                    break;
                }
            }
        }

        private static Rect Inset(Rect r, float d) => new Rect(r.x + d, r.y + d, r.width - d * 2f, r.height - d * 2f);

        private static void RoundRect(VertexHelper vh, Rect r, float radius, Color c)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(r.width, r.height) * .5f);
            const int seg = 6;
            int center = vh.currentVertCount;
            vh.AddVert(r.center, c, Vector2.zero);
            var corners = new[]
            {
                new Vector2(r.xMax - radius, r.yMax - radius), new Vector2(r.xMin + radius, r.yMax - radius),
                new Vector2(r.xMin + radius, r.yMin + radius), new Vector2(r.xMax - radius, r.yMin + radius),
            };
            int first = vh.currentVertCount;
            for (int k = 0; k < 4; k++)
                for (int s = 0; s <= seg; s++)
                {
                    float ang = (k * 90f + s * 90f / seg) * Mathf.Deg2Rad;
                    vh.AddVert(corners[k] + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius, c, Vector2.zero);
                }
            int count = vh.currentVertCount - first;
            for (int i = 0; i < count; i++)
                vh.AddTriangle(center, first + i, first + (i + 1) % count);
        }

        private static void Disc(VertexHelper vh, Vector2 p, float radius, Color c)
        {
            const int seg = 28;
            int center = vh.currentVertCount;
            vh.AddVert(p, c, Vector2.zero);
            for (int i = 0; i < seg; i++)
            {
                float ang = i * Mathf.PI * 2f / seg;
                vh.AddVert(p + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius, c, Vector2.zero);
            }
            for (int i = 0; i < seg; i++)
                vh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % seg);
        }

        private static void Ring(VertexHelper vh, Vector2 p, float radius, float width, Color c)
        {
            const int seg = 28;
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Line(vh, p + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, p + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, width, c);
            }
        }

        private static void ArrowSymbol(VertexHelper vh, Vector2 p, float s, int direction, Color c)
        {
            Vector2 dir = direction == 0 ? Vector2.left : direction == 1 ? Vector2.right : direction == 2 ? Vector2.up : Vector2.down;
            Vector2 side = new Vector2(-dir.y, dir.x);
            Vector2 tip = p + dir * s * 1.1f, baseCenter = p - dir * s * .15f;
            int i = vh.currentVertCount;
            vh.AddVert(tip, c, Vector2.zero);
            vh.AddVert(baseCenter + side * s * .85f, c, Vector2.zero);
            vh.AddVert(baseCenter - side * s * .85f, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            Line(vh, baseCenter, p - dir * s * 1.05f, s * .5f, c);
        }

        private static void PadSymbol(VertexHelper vh, Vector2 p, float s, int symbol, Color c)
        {
            float w = Mathf.Max(1.6f, s * .28f);
            switch (symbol)
            {
                case 0: // cross
                    Line(vh, p + new Vector2(-s, -s), p + new Vector2(s, s), w, c);
                    Line(vh, p + new Vector2(-s, s), p + new Vector2(s, -s), w, c);
                    break;
                case 1: Ring(vh, p, s, w, c); break; // circle
                case 2: // square
                    Line(vh, p + new Vector2(-s, -s), p + new Vector2(s, -s), w, c);
                    Line(vh, p + new Vector2(s, -s), p + new Vector2(s, s), w, c);
                    Line(vh, p + new Vector2(s, s), p + new Vector2(-s, s), w, c);
                    Line(vh, p + new Vector2(-s, s), p + new Vector2(-s, -s), w, c);
                    break;
                default: // triangle
                    var t0 = p + new Vector2(0f, s * 1.1f); var t1 = p + new Vector2(s, -s * .8f); var t2 = p + new Vector2(-s, -s * .8f);
                    Line(vh, t0, t1, w, c); Line(vh, t1, t2, w, c); Line(vh, t2, t0, w, c);
                    break;
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
}
