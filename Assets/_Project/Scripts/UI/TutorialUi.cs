using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Building blocks shared by the tutorial views: localized lookup, Noto Sans text, and the key / button hint row.</summary>
    public static class TutorialUi
    {
        public static readonly Color Muted = new Color(.80f, .78f, .72f);
        private static readonly List<string> Paths = new List<string>();

        public static InputModeService Input => GameSystems.Exists ? GameSystems.Instance.InputMode : TutorialPromptView.PreviewInput;

        public static string Language => GameSystems.Exists ? GameSystems.Instance.Localization.CurrentLanguage
            : TutorialPromptView.PreviewLanguage ?? LocalizationTable.DefaultLanguage;

        public static Font Font => FontProvider.Get(Language);

        public static string Lookup(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (GameSystems.Exists) return GameSystems.Instance.Localization.Get(key);
            return LocalizationTable.TryGet(Language, key, out var v) ? v : key;
        }

        public static Text MakeText(string name, Transform parent, int size, Color color)
        {
            var rt = GoldUiTheme.Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.fontSize = size; t.fontStyle = FontStyle.Normal; t.color = color; t.raycastTarget = false;
            t.alignment = TextAnchor.MiddleLeft; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = false; t.lineSpacing = 1.05f;
            return t;
        }

        public static float PreferredWidth(Text t)
        {
            var settings = t.GetGenerationSettings(new Vector2(10000f, 1000f));
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t.cachedTextGeneratorForLayout.GetPreferredWidth(t.text, settings) / t.pixelsPerUnit;
        }

        public static float PreferredHeight(Text t, float width)
        {
            var settings = t.GetGenerationSettings(new Vector2(width, 1000f));
            return Mathf.Ceil(t.cachedTextGeneratorForLayout.GetPreferredHeight(t.text, settings) / t.pixelsPerUnit);
        }

        public static void DestroyAll(List<RectTransform> objects)
        {
            foreach (var g in objects)
                if (g != null) { if (Application.isPlaying) Object.Destroy(g.gameObject); else Object.DestroyImmediate(g.gameObject); }
            objects.Clear();
        }

        /// <summary>
        /// Fills <paramref name="row"/> (left-anchored, vertically centred children) with the hints of the active device.
        /// Returns the row width (0 = nothing). Created objects are appended to <paramref name="created"/>.
        /// </summary>
        public static float BuildGlyphRow(Transform row, TutorialGlyph[] glyphs, Font font, float h, List<RectTransform> created)
        {
            if (glyphs == null || glyphs.Length == 0)
                return 0f;
            var input = Input;
            var device = input != null ? input.ActiveDevice : InputDeviceKind.Keyboard;
            float x = 0f;
            for (int gi = 0; gi < glyphs.Length; gi++)
            {
                var glyph = glyphs[gi];
                if (glyph == TutorialGlyph.None)
                    continue;
                if (x > 0f) x += 12f;
                if (glyph == TutorialGlyph.GoldenLeaf)
                {
                    var leaf = GoldUiTheme.Rect("Leaf", row);
                    GoldUiTheme.Place(leaf, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(x, 0f), new Vector2(h, h));
                    var slot = leaf.gameObject.AddComponent<CollectibleSlot>();
                    slot.raycastTarget = false; slot.SetFilled(true);
                    created.Add(leaf);
                    x += h;
                    continue;
                }
                if (glyph == TutorialGlyph.Check)
                {
                    x += Glyph(row, new GlyphSpec { shape = GlyphShape.Check }, font, x, h * .84f, created);
                    continue;
                }
                var hint = ToHint(glyph);
                if (input != null) input.GetBindingPaths(hint, device, Paths);
                else Paths.Clear();
                int perSet = InputModeService.PathsPerSet(hint, device);
                int sets = Paths.Count / Mathf.Max(1, perSet);
                var shown = new List<string>();
                for (int s = 0; s < sets && shown.Count < 2; s++)
                {
                    // Skip a set whose legends repeat an earlier one (left / right Shift, Enter / numpad Enter).
                    string key = "";
                    for (int p = 0; p < perSet; p++) key += Describe(Paths[s * perSet + p]) + "|";
                    if (shown.Contains(key)) continue;
                    shown.Add(key);
                    if (shown.Count > 1)
                    {
                        x += 6f;
                        x += Separator(row, font, x, h, created) + 6f;
                    }
                    for (int p = 0; p < perSet; p++)
                    {
                        if (p > 0) x += 6f;
                        x += Glyph(row, InputGlyphLabels.Describe(Paths[s * perSet + p]), font, x, h, created);
                    }
                }
            }
            return x;
        }

        private static string Describe(string path)
        {
            var spec = InputGlyphLabels.Describe(path);
            return spec.shape + ":" + spec.label + ":" + spec.variant;
        }

        private static HintAction ToHint(TutorialGlyph glyph)
        {
            switch (glyph)
            {
                case TutorialGlyph.Move: return HintAction.MoveHorizontal;
                case TutorialGlyph.MapStep: return HintAction.MoveVertical;
                case TutorialGlyph.Jump: return HintAction.Jump;
                case TutorialGlyph.Run: return HintAction.Run;
                case TutorialGlyph.Dodge: return HintAction.Dodge;
                case TutorialGlyph.Cancel: return HintAction.Cancel;
                default: return HintAction.Confirm;
            }
        }

        private static float Glyph(Transform row, GlyphSpec spec, Font font, float x, float h, List<RectTransform> created)
        {
            var rt = GoldUiTheme.Rect("Glyph", row);
            var g = rt.gameObject.AddComponent<InputGlyph>();
            g.Set(spec);
            float width = h;
            if (!string.IsNullOrEmpty(spec.label))
            {
                var label = MakeText("Legend", rt, Mathf.RoundToInt(h * (spec.shape == GlyphShape.Stick || spec.shape == GlyphShape.FaceLetter ? .46f : .42f)), GoldUiTheme.Cream);
                label.font = font; label.text = spec.label; label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                GoldUiTheme.Stretch(label.rectTransform);
                if (spec.shape == GlyphShape.Key || spec.shape == GlyphShape.Pill)
                    width = Mathf.Max(h, PreferredWidth(label) + (spec.shape == GlyphShape.Pill ? 30f : 26f));
            }
            GoldUiTheme.Place(rt, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(x, 0f), new Vector2(width, h));
            created.Add(rt);
            return width;
        }

        private static float Separator(Transform row, Font font, float x, float h, List<RectTransform> created)
        {
            var rt = GoldUiTheme.Rect("Or", row);
            var t = MakeText("Slash", rt, Mathf.RoundToInt(h * .44f), Muted);
            t.font = font; t.text = "/"; t.alignment = TextAnchor.MiddleCenter; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            GoldUiTheme.Stretch(t.rectTransform);
            GoldUiTheme.Place(rt, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(x, 0f), new Vector2(14f, h));
            created.Add(rt);
            return 14f;
        }
    }
}
