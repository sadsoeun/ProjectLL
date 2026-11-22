using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Illustrated counterattack cue, driven by unscaled time while the boss is groggy.</summary>
    public sealed class NowCutIn : MonoBehaviour
    {
        public const string TextKey = "cutin.now";
        private const float Total = 1.15f, BandIn = .14f, TextAt = .20f, GlintAt = .30f, BandOut = .88f;
        private const float BandHeight = 310f, BandAngle = 5f;
        private static NowCutIn current;
        public static bool IsPlaying => current != null;
        /// <summary>Set by the stage controller: the cue is skipped while this returns false (player dead, stage cleared...).</summary>
        public static System.Func<bool> CanPlay;

        private float t, savedTimeScale = 1f;
        private bool ownsTime;
        private RectTransform canvasRect, layout, motion, band, portrait, lettering, glintA, glintB;
        private RectTransform edgeTop, edgeBottom, speedRect;
        private CanvasGroup group, portraitGroup, letteringGroup;
        private RawImage lines;
        private Image dim, flash;
        private float travel = 2600f;
        private string language;

        public static void Play(Transform player)
        {
            if (current != null || player == null) return;
            if (CanPlay != null && !CanPlay()) return; // no "Now!" for a player who cannot act on it
            // TimeStop restores its saved scale before the cut-in takes ownership.
            TimeStop.Cancel();
            var go = new GameObject("NowCutIn");
            current = go.AddComponent<NowCutIn>();
            try { current.Begin(); }
            catch
            {
                current.RestoreTime();
                current.enabled = false;
                Destroy(go);
                current = null;
                throw;
            }
        }

        private void Begin()
        {
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            ownsTime = true;
            Time.timeScale = 0f;
            Voice.PlayOr(VoiceSlot.Now, SfxId.CutInNow);
            BuildUi(GameSystems.Exists ? GameSystems.Instance.Localization.CurrentLanguage : "ko");
            Apply(0f);
        }

        private void BuildUi(string languageCode)
        {
            language = languageCode;
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            canvasRect = (RectTransform)transform;
            dim = FullScreen("Dim", new Color(.025f, .035f, .05f, 0));

            layout = Rect("SafeComposition", transform, new Vector2(1920, 1080));
            motion = Rect("CutInMotion", layout, new Vector2(1920, 1080));
            motion.localRotation = Quaternion.Euler(0, 0, BandAngle);
            group = motion.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            band = Rect("CharcoalBand", motion, new Vector2(2700, BandHeight));
            var back = band.gameObject.AddComponent<Image>();
            back.color = new Color(.035f, .05f, .07f, .98f);
            back.raycastTarget = false;
            band.gameObject.AddComponent<RectMask2D>();
            speedRect = Rect("SpeedLines", band, new Vector2(2700, BandHeight));
            lines = speedRect.gameObject.AddComponent<RawImage>();
            lines.texture = SpeedLineTexture();
            lines.color = new Color(.88f, .70f, .35f, .28f);
            lines.raycastTarget = false;
            // Restrained turquoise strokes tie the portrait to her costume.
            for (int i = 0; i < 3; i++)
            {
                var r = Rect("TealStreak" + i, band, new Vector2(220 + i * 75, 3 + i));
                r.anchoredPosition = new Vector2(-780 + i * 170, -90 + i * 85);
                var g = r.gameObject.AddComponent<Image>();
                g.color = new Color(.88f, .70f, .42f, .7f);
                g.raycastTarget = false;
            }

            var portraitWindow = Rect("PortraitWindow", motion, new Vector2(650, 600));
            portraitWindow.anchoredPosition = new Vector2(-550, 145);
            portraitWindow.gameObject.AddComponent<RectMask2D>();
            portraitGroup = portraitWindow.gameObject.AddComponent<CanvasGroup>();
            portrait = Rect("Illustration", portraitWindow, new Vector2(610, 610));
            var art = Resources.Load<Texture2D>("CutInArt/CounterPortrait");
            if (art == null)
            {
                Debug.LogWarning("[NowCutIn] CounterPortrait missing; using the bundled HUD portrait.");
                art = Resources.Load<Texture2D>("HudArt/Portrait");
            }
            AddArtwork(portrait, art, new Vector2(610, 610));

            lettering = Rect("Now", motion, new Vector2(1080, 360));
            lettering.anchoredPosition = new Vector2(335, 12);
            letteringGroup = lettering.gameObject.AddComponent<CanvasGroup>();
            var word = language == "ko" ? Resources.Load<Texture2D>("CutInArt/NowKo") : null;
            if (word != null) AddArtwork(lettering, word, new Vector2(1080, 360));
            else BuildLocalizedLabel();

            // Borders are above the portrait's cropped base; the top ribbon can break the top edge.
            edgeTop = Stripe("GoldTop", BandHeight * .5f, 4f);
            edgeTop.SetSiblingIndex(1);
            edgeBottom = Stripe("GoldBottom", -BandHeight * .5f, 6f);
            // On the two irises of CounterPortrait (1254 px square shown at 610: eyes at about (905, 640) and (742, 645) px).
            glintA = Glint("EyeGlint", portrait, 110f, new Vector2(135, -6));
            glintB = Glint("EyeGlintSmall", portrait, 55f, new Vector2(56, -9));
            flash = FullScreen("Flash", new Color(1, .97f, .86f, 0));
        }

        private void BuildLocalizedLabel()
        {
            lettering.sizeDelta = new Vector2(1060, 260);
            var label = lettering.gameObject.AddComponent<Text>();
            label.font = FontProvider.Get(language);
            LocalizationTable.TryGet(language, TextKey, out string value);
            label.text = value ?? "NOW!";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 150;
            label.fontStyle = FontStyle.BoldAndItalic;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 80;
            label.resizeTextMaxSize = 150;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.color = GoldUiTheme.Cream;
            label.raycastTarget = false;
            var outline = lettering.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.02f, .025f, .03f, 1);
            outline.effectDistance = new Vector2(5, -5);
            var shadow = lettering.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(.72f, .45f, .1f, 1);
            shadow.effectDistance = new Vector2(4, -9);
        }

        private static void AddArtwork(RectTransform rect, Texture2D texture, Vector2 limit)
        {
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            if (texture == null) { image.enabled = false; return; }
            float scale = Mathf.Min(limit.x / texture.width, limit.y / texture.height);
            rect.sizeDelta = new Vector2(texture.width, texture.height) * scale;
        }

        private RectTransform Stripe(string name, float y, float height)
        {
            var r = Rect(name, motion, new Vector2(2700, height));
            r.anchoredPosition = new Vector2(0, y);
            var image = r.gameObject.AddComponent<Image>();
            image.color = new Color(1, .76f, .30f, 1);
            image.raycastTarget = false;
            return r;
        }

        private static RectTransform Glint(string name, Transform parent, float size, Vector2 at)
        {
            var r = Rect(name, parent, new Vector2(size, size));
            r.anchoredPosition = at;
            var image = r.gameObject.AddComponent<Image>();
            image.sprite = GlintSprite();
            image.color = new Color(1, .98f, .83f, 1);
            image.raycastTarget = false;
            return r;
        }

        private Image FullScreen(string name, Color color)
        {
            var r = Rect(name, transform, Vector2.zero);
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var image = r.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent, false); r.sizeDelta = size;
            return r;
        }

        private void FitComposition()
        {
            var size = canvasRect.rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            float scale = Mathf.Min(size.x / 1920f, size.y / 1080f);
            layout.localScale = Vector3.one * scale;
            float width = size.x / scale + 800f;
            band.sizeDelta = new Vector2(width, BandHeight);
            speedRect.sizeDelta = band.sizeDelta;
            edgeTop.sizeDelta = new Vector2(width, 4);
            edgeBottom.sizeDelta = new Vector2(width, 6);
            travel = width + 400f;
        }

        private void LateUpdate()
        {
            t += Time.unscaledDeltaTime;
            Apply(t);
            if (t >= Total) Finish();
        }

        private void Apply(float time)
        {
            FitComposition();
            float exit = Mathf.Clamp01((time - BandOut) / (Total - BandOut));
            float enter = EaseOut(Mathf.Clamp01(time / BandIn));
            group.alpha = (time <= 0 ? 0 : 1) * (1 - exit);
            dim.color = new Color(.025f, .035f, .05f, .36f * Mathf.Clamp01(time / .10f) * (1 - exit));
            float pulse = time >= GlintAt && time < GlintAt + .06f ? .12f * (1 - (time - GlintAt) / .06f) : 0;
            flash.color = new Color(1, .97f, .86f, pulse);
            float x = time < BandIn ? Mathf.Lerp(travel, 0, enter) : time < BandOut ? -18f * Mathf.Clamp01((time - BandIn) / (BandOut - BandIn)) : Mathf.Lerp(-18f, -travel, exit * exit);
            motion.anchoredPosition = new Vector2(x, 65);
            // Only the background unfolds vertically. The painted art never stretches or warps.
            band.localScale = new Vector3(1, Mathf.Lerp(.15f, 1, enter), 1);
            float artIn = EaseOut(Mathf.Clamp01((time - .055f) / .17f));
            portraitGroup.alpha = artIn;
            portrait.anchoredPosition = new Vector2(Mathf.Lerp(-110, 0, artIn), 0);
            portrait.localScale = Vector3.one * Mathf.Lerp(1.025f, 1, artIn);
            lines.uvRect = new Rect(time * 1.8f, 0, 3, 1);
            float tk = time - TextAt;
            letteringGroup.alpha = Mathf.Clamp01(tk / .045f);
            float textScale = tk < 0 ? 1 : tk < .10f ? Mathf.Lerp(1.12f, .985f, EaseOut(tk / .10f)) : Mathf.Lerp(.985f, 1, Mathf.Clamp01((tk - .10f) / .08f));
            lettering.localScale = Vector3.one * textScale;
            glintA.localScale = Vector3.one * GlintScale(time - GlintAt);
            glintB.localScale = Vector3.one * GlintScale(time - GlintAt - .065f);
            glintA.localRotation = Quaternion.Euler(0, 0, 20 * Mathf.Clamp01((time - GlintAt) / .3f));
        }

        private static float GlintScale(float k)
        {
            if (k < 0 || k > .34f) return 0;
            return k < .055f ? k / .055f : 1 - (k - .055f) / .285f;
        }
        private static float EaseOut(float x) => 1 - (1 - x) * (1 - x);

        private void RestoreTime()
        {
            if (!ownsTime) return;
            if (Time.timeScale == 0) Time.timeScale = savedTimeScale;
            ownsTime = false;
        }
        private void Finish()
        {
            if (current == this) current = null;
            RestoreTime();
            enabled = false;
            Destroy(gameObject);
        }
        private void OnDisable()
        {
            RestoreTime();
            if (current == this) current = null;
        }
        private void OnDestroy() => RestoreTime();

        // Procedural accents are shared between plays, not recreated with each cue.

        private static Texture2D speedLines;
        private static Sprite glint;

        /// <summary>Horizontal comic speed lines (tiles in X).</summary>
        private static Texture2D SpeedLineTexture()
        {
            if (speedLines != null)
                return speedLines;
            const int w = 512, h = 128;
            speedLines = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, name = "CutInSpeedLines" };
            var px = new Color32[w * h];
            var rnd = new System.Random(5);
            for (int i = 0; i < 26; i++)
            {
                int y = rnd.Next(2, h - 2);
                int thick = rnd.Next(1, 4);
                int start = rnd.Next(0, w);
                int len = rnd.Next(w / 4, w * 3 / 4);
                for (int k = 0; k < len; k++)
                {
                    int x = (start + k) % w;
                    float taper = Mathf.Sin(Mathf.PI * k / len);
                    for (int dy = -thick; dy <= thick; dy++)
                    {
                        int yy = Mathf.Clamp(y + dy, 0, h - 1);
                        float a = taper * (1f - Mathf.Abs(dy) / (thick + 1f));
                        var c = px[yy * w + x];
                        byte v = (byte)Mathf.Max(c.a, a * 255f);
                        px[yy * w + x] = new Color32(255, 255, 255, v);
                    }
                }
            }
            speedLines.SetPixels32(px);
            speedLines.Apply();
            return speedLines;
        }

        /// <summary>Four-point star with a soft glow (white; tinted by the Image colour).</summary>
        private static Sprite GlintSprite()
        {
            if (glint != null)
                return glint;
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CutInGlint" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                float ax = Mathf.Abs(u), ay = Mathf.Abs(v);
                float star = Mathf.Max(Mathf.Clamp01(1f - ax / Mathf.Max(0.001f, 0.16f * (1f - ay))), Mathf.Clamp01(1f - ay / Mathf.Max(0.001f, 0.16f * (1f - ax))));
                star *= Mathf.Clamp01(1f - Mathf.Max(ax, ay));
                float glow = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) / 0.45f);
                float a = Mathf.Clamp01(star * 1.6f + glow * glow * 0.8f);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            glint = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return glint;
        }
    }
}
