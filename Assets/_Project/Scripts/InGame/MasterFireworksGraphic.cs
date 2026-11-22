using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Tunables of the master-clear fireworks on the stage result screen (exposed on InGameView).</summary>
    [System.Serializable]
    public sealed class MasterFireworksSettings
    {
        [Tooltip("Bursts in the opening volley.")]
        [Range(1, 16)] public int volleyBursts = 9;
        [Tooltip("Seconds between launches of the opening volley.")]
        [Min(0.05f)] public float volleySpacing = 0.28f;
        [Tooltip("After the volley, one more burst every N seconds while the result is shown. 0 = stop after the volley.")]
        [Min(0f)] public float repeatInterval = 1.2f;
        [Range(8, 64)] public int sparksPerBurst = 40;
        [Tooltip("Rocket rise time before it bursts (s).")]
        [Min(0.1f)] public float rocketSeconds = 0.5f;
        [Tooltip("Rocket rise height in reference pixels (960 px tall canvas).")]
        [Min(0f)] public float rocketRise = 240f;
        [Tooltip("Lifetime of the burst sparks (s).")]
        [Min(0.2f)] public float sparkSeconds = 1.75f;
        [Tooltip("Burst radius in reference pixels (960 px tall canvas).")]
        [Min(20f)] public float burstRadius = 205f;
        [Tooltip("Downward droop of the sparks at the end of their life, as a fraction of the radius.")]
        [Range(0f, 1.5f)] public float gravity = 0.55f;
        [Tooltip("Trail length as a fraction of the spark lifetime.")]
        [Range(0f, 0.6f)] public float trailLength = 0.3f;
        [Range(0.5f, 3f)] public float sparkWidth = 1.7f;
        [Range(0f, 1f)] public float opacity = 1f;
        [Tooltip("Spark opacity multiplier where a spark passes behind protected text (title, badge).")]
        [Range(0f, 1f)] public float behindTextOpacity = 0.25f;
        [Tooltip("Burst accent colours. Every other spark of a burst is gold, so each burst reads colourful-gold.")]
        public Color[] palette =
        {
            new Color(1f, 0.80f, 0.28f),  // gold
            new Color(1f, 0.42f, 0.52f),  // rose
            new Color(0.40f, 0.86f, 1f),  // sky
            new Color(0.62f, 1f, 0.46f),  // lime
            new Color(0.80f, 0.56f, 1f),  // violet
            new Color(1f, 0.62f, 0.25f),  // orange
        };
        [Tooltip("Burst centres as fractions of the screen (0,0 = bottom left), used in order and cycled. " +
                 "The result column sits at x = 0.07..0.43, so most points ring it and the open right side.")]
        public Vector2[] burstPoints =
        {
            new Vector2(0.10f, 0.83f), new Vector2(0.50f, 0.70f), new Vector2(0.06f, 0.40f), new Vector2(0.66f, 0.86f),
            new Vector2(0.47f, 0.28f), new Vector2(0.31f, 0.94f), new Vector2(0.84f, 0.62f), new Vector2(0.20f, 0.10f),
            new Vector2(0.58f, 0.50f), new Vector2(0.92f, 0.88f),
        };
    }

    /// <summary>
    /// Firework rockets, two-tone bursts with tapered trails, flashes and twinkling glitter in a single uGUI mesh.
    /// Deterministic in time (Sample) so the editor can render frames; unscaled time while playing (result is paused).
    /// Sparks dim where they pass behind protected rects so the title and badge stay readable.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MasterFireworksGraphic : MaskableGraphic
    {
        private const int TrailSteps = 4;
        private static readonly Color Gold = new Color(1f, 0.80f, 0.30f);
        private static readonly Color PaleGold = new Color(1f, 0.95f, 0.74f);

        private MasterFireworksSettings settings = new MasterFireworksSettings();
        private float time;
        private bool running;
        private RectTransform[] protectedRects = new RectTransform[0];
        private Rect[] protectedLocal = new Rect[0];

        public bool IsRunning => running;
        public float ElapsedSeconds => time;

        public void SetProtected(params RectTransform[] rects) => protectedRects = rects ?? new RectTransform[0];

        public void Play(MasterFireworksSettings config)
        {
            settings = config ?? new MasterFireworksSettings();
            gameObject.SetActive(true);
            running = true;
            Sample(0f);
        }

        public void Stop()
        {
            running = false;
            gameObject.SetActive(false);
        }

        /// <summary>Shows the fireworks at the given time since Play (editor verification uses this).</summary>
        public void Sample(float seconds)
        {
            time = Mathf.Max(0f, seconds);
            SetVerticesDirty();
        }

        private void Update()
        {
            if (!running) return;
            time += UnityEngine.Time.unscaledDeltaTime;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (settings == null) return;
            Rect r = rectTransform.rect;
            if (r.width < 1f || r.height < 1f) return;
            float unit = r.height / 960f;
            CacheProtected();

            float life = settings.rocketSeconds + settings.sparkSeconds;
            int volley = Mathf.Max(1, settings.volleyBursts);
            for (int i = 0; i < volley; i++)
                Burst(vh, r, unit, i, i * settings.volleySpacing);
            if (settings.repeatInterval > 0f)
            {
                float repeatStart = volley * settings.volleySpacing;
                int first = Mathf.Max(0, Mathf.FloorToInt((time - repeatStart - life) / settings.repeatInterval));
                int last = Mathf.FloorToInt((time - repeatStart) / settings.repeatInterval);
                for (int k = first; k <= last; k++)
                    Burst(vh, r, unit, volley + k, repeatStart + k * settings.repeatInterval);
            }
        }

        private void Burst(VertexHelper vh, Rect r, float unit, int index, float launch)
        {
            float t = time - launch;
            if (t < 0f || t > settings.rocketSeconds + settings.sparkSeconds) return;
            var points = settings.burstPoints;
            Vector2 n = points != null && points.Length > 0 ? points[index % points.Length] : new Vector2(0.5f, 0.6f);
            float h1 = Hash(index * 12.9898f + 1.3f), h2 = Hash(index * 78.233f + 4.1f), h3 = Hash(index * 37.719f + 9.7f);
            // Cycles after the first pass get jittered so the repeat show never looks copy-pasted.
            if (points != null && index >= points.Length) n += new Vector2((h1 - 0.5f) * 0.10f, (h2 - 0.5f) * 0.08f);
            Vector2 centre = new Vector2(r.xMin + r.width * n.x, r.yMin + r.height * n.y);
            float radius = settings.burstRadius * unit * (0.8f + 0.4f * h3);
            var palette = settings.palette;
            Color accent = palette != null && palette.Length > 0 ? palette[(int)(h1 * 997f) % palette.Length] : Gold;
            float width = settings.sparkWidth * unit;

            if (t < settings.rocketSeconds)
            {
                Rocket(vh, centre, unit, t / settings.rocketSeconds, (h2 - 0.5f) * 60f * unit, accent, width);
                return;
            }

            float tau = t - settings.rocketSeconds;
            float u = tau / settings.sparkSeconds;
            // Flash + soft bloom at the burst point.
            float flash = 1f - Mathf.Clamp01(u / 0.16f);
            if (flash > 0f)
            {
                Glow(vh, centre, radius * Mathf.Lerp(0.25f, 0.62f, 1f - flash), Tint(Color.Lerp(accent, Color.white, 0.6f), 0.55f * flash));
                Star(vh, centre, 26f * unit * (0.6f + flash), Tint(Color.white, flash));
            }
            Glow(vh, centre, radius * 0.8f, Tint(Color.Lerp(accent, Color.white, 0.3f), 0.06f * Mathf.Sin(Mathf.Clamp01(u * 2f) * Mathf.PI)));

            int sparks = settings.sparksPerBurst;
            float spin = h2 * Mathf.PI * 2f;
            for (int j = 0; j < sparks; j++)
            {
                float hs = Hash(index * 131.1f + j * 17.37f);
                float angle = spin + (j + hs * 0.6f) / sparks * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float speed = radius * (0.72f + 0.28f * Hash(index * 7.7f + j * 3.3f));
                Color c = (j & 1) == 0 ? accent : (j % 4 == 1 ? Gold : PaleGold);
                // tail fades with age; the last third crackles
                float alpha = (1f - Mathf.SmoothStep(0.45f, 1f, u)) * settings.opacity;
                if (u > 0.6f) alpha *= 0.55f + 0.45f * Mathf.Sin(hs * 60f + tau * 38f);
                if (alpha <= 0.01f) continue;

                Vector2 head = SparkPos(centre, dir, speed, radius, u);
                float prevW = width * 3.0f * (1f - 0.5f * u);
                Vector2 prev = head;
                for (int s = 1; s <= TrailSteps; s++)
                {
                    float us = Mathf.Max(0f, u - settings.trailLength * s / TrailSteps);
                    Vector2 p = SparkPos(centre, dir, speed, radius, us);
                    float k = 1f - (float)s / TrailSteps;
                    float w = width * 3.0f * (1f - 0.5f * u) * k;
                    float aHead = alpha * Mathf.Lerp(0.9f, 0.1f, (s - 1f) / TrailSteps);
                    float aTail = alpha * Mathf.Lerp(0.9f, 0.1f, (float)s / TrailSteps);
                    // darker under-stroke keeps the trail readable on bright skies, bright core on top
                    Color edge = Color.Lerp(c, new Color(0.35f, 0.12f, 0.02f), 0.55f);
                    Taper(vh, prev, p, prevW * 2.2f, w * 2.2f, Shield(prev, Tint(edge, aHead * 0.35f)), Shield(p, Tint(edge, aTail * k * 0.35f)));
                    Taper(vh, prev, p, prevW, w, Shield(prev, Tint(c, aHead)), Shield(p, Tint(c, aTail * k)));
                    prev = p; prevW = w;
                    if (us <= 0f) break;
                }
                Color headColor = Color.Lerp(c, Color.white, 0.55f);
                Diamond(vh, head, width * 2.6f, width * 4.2f, Shield(head, Tint(headColor, alpha)));
                if (j % 6 == 0 && u > 0.3f)
                    Star(vh, head, width * 9f * (0.6f + 0.4f * Mathf.Sin(tau * 20f + hs * 9f)), Shield(head, Tint(PaleGold, alpha * 0.8f)));
            }

            // Glitter: little stars that twinkle in the burst cloud as it falls.
            for (int g = 0; g < 12; g++)
            {
                float hg = Hash(index * 51.3f + g * 9.1f), hg2 = Hash(index * 23.9f + g * 4.7f);
                float born = 0.35f + hg * 0.35f;
                float ga = (u - born) / (1f - born);
                if (ga <= 0f || ga >= 1f) continue;
                float ang = hg2 * Mathf.PI * 2f;
                Vector2 p = centre + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius * (0.3f + 0.7f * hg)
                            - new Vector2(0f, radius * settings.gravity * u * u * 0.9f);
                float twinkle = Mathf.Max(0f, Mathf.Sin(ga * Mathf.PI) * Mathf.Sin(tau * 16f + hg * 30f));
                Star(vh, p, width * 8f * twinkle, Shield(p, Tint(g % 2 == 0 ? PaleGold : Color.Lerp(accent, Color.white, 0.5f), twinkle * settings.opacity)));
            }
        }

        private void Rocket(VertexHelper vh, Vector2 burstAt, float unit, float k, float drift, Color accent, float width)
        {
            // Ease-out rise; the trail is the last few positions.
            float rise = settings.rocketRise * unit;
            Vector2 start = burstAt + new Vector2(-drift, -rise);
            Vector2 At(float q) { float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(q), 2.2f); return Vector2.Lerp(start, burstAt, e) + new Vector2(Mathf.Sin(q * 9f) * 2.5f * unit, 0f); }
            Vector2 head = At(k);
            float a = Mathf.SmoothStep(0f, 1f, k / 0.15f) * settings.opacity;
            Vector2 prev = head;
            float prevW = width * 3.2f;
            for (int s = 1; s <= 6; s++)
            {
                Vector2 p = At(k - s * 0.07f);
                float w = width * 3.2f * (1f - s / 6f);
                Taper(vh, prev, p, prevW, w, Shield(prev, Tint(PaleGold, a * (1f - (s - 1) / 6f))), Shield(p, Tint(Gold, a * (1f - s / 6f) * 0.6f)));
                prev = p; prevW = w;
            }
            Glow(vh, head, 14f * unit, Tint(Color.Lerp(accent, Color.white, 0.4f), a * 0.6f));
            Diamond(vh, head, width * 3f, width * 5f, Shield(head, Tint(Color.white, a)));
        }

        private Vector2 SparkPos(Vector2 centre, Vector2 dir, float speed, float radius, float u)
        {
            // Drag-damped radial travel plus gravity droop.
            const float drag = 4f;
            float travel = (1f - Mathf.Exp(-drag * u)) / (1f - Mathf.Exp(-drag));
            return centre + dir * speed * travel - new Vector2(0f, radius * settings.gravity * u * u);
        }

        private void CacheProtected()
        {
            if (protectedLocal.Length != protectedRects.Length) protectedLocal = new Rect[protectedRects.Length];
            var corners = new Vector3[4];
            for (int i = 0; i < protectedRects.Length; i++)
            {
                var pr = protectedRects[i];
                if (pr == null || !pr.gameObject.activeInHierarchy) { protectedLocal[i] = Rect.zero; continue; }
                pr.GetWorldCorners(corners);
                Vector2 a = rectTransform.InverseTransformPoint(corners[0]);
                Vector2 b = rectTransform.InverseTransformPoint(corners[2]);
                protectedLocal[i] = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            }
        }

        /// <summary>Dims a colour where it falls behind protected text.</summary>
        private Color Shield(Vector2 p, Color c)
        {
            for (int i = 0; i < protectedLocal.Length; i++)
            {
                var pr = protectedLocal[i];
                if (pr.width > 0f && pr.Contains(p)) { c.a *= settings.behindTextOpacity; break; }
            }
            return c;
        }

        private static float Hash(float x) { float s = Mathf.Sin(x) * 43758.5453f; return s - Mathf.Floor(s); }
        private Color Tint(Color c, float alpha) { c.a = Mathf.Clamp01(alpha) * color.a; return c; }

        private static void Vertex(VertexHelper vh, Vector2 position, Color c)
        {
            var v = UIVertex.simpleVert;
            v.position = position;
            v.color = c;
            vh.AddVert(v);
        }

        private static void Triangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color ca, Color cb, Color cc)
        {
            int n = vh.currentVertCount;
            Vertex(vh, a, ca); Vertex(vh, b, cb); Vertex(vh, c, cc);
            vh.AddTriangle(n, n + 1, n + 2);
        }

        private static void Taper(VertexHelper vh, Vector2 a, Vector2 b, float wa, float wb, Color ca, Color cb)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude < 0.01f) return;
            Vector2 n = new Vector2(-d.y, d.x).normalized;
            int s = vh.currentVertCount;
            Vertex(vh, a - n * wa * 0.5f, ca); Vertex(vh, a + n * wa * 0.5f, ca);
            Vertex(vh, b + n * wb * 0.5f, cb); Vertex(vh, b - n * wb * 0.5f, cb);
            vh.AddTriangle(s, s + 1, s + 2);
            vh.AddTriangle(s, s + 2, s + 3);
        }

        private static void Diamond(VertexHelper vh, Vector2 p, float w, float h, Color c)
        {
            if (c.a <= 0.003f) return;
            Triangle(vh, p + Vector2.left * w, p + Vector2.up * h, p + Vector2.right * w, c, c, c);
            Triangle(vh, p + Vector2.left * w, p + Vector2.right * w, p + Vector2.down * h, c, c, c);
        }

        /// <summary>Four-point star: two thin crossed diamonds with a bright centre.</summary>
        private static void Star(VertexHelper vh, Vector2 p, float size, Color c)
        {
            if (size <= 0.2f || c.a <= 0.003f) return;
            Color clear = new Color(c.r, c.g, c.b, 0f);
            float thin = size * 0.16f;
            Triangle(vh, p + Vector2.up * size, p + Vector2.left * thin, p + Vector2.right * thin, clear, c, c);
            Triangle(vh, p + Vector2.down * size, p + Vector2.right * thin, p + Vector2.left * thin, clear, c, c);
            Triangle(vh, p + Vector2.left * size, p + Vector2.down * thin, p + Vector2.up * thin, clear, c, c);
            Triangle(vh, p + Vector2.right * size, p + Vector2.up * thin, p + Vector2.down * thin, clear, c, c);
            Diamond(vh, p, thin * 1.2f, thin * 1.2f, c);
        }

        private static void Glow(VertexHelper vh, Vector2 centre, float radius, Color c)
        {
            if (radius <= 0.5f || c.a <= 0.003f) return;
            const int segments = 24;
            Color clear = new Color(c.r, c.g, c.b, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
                Triangle(vh, centre, centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    centre + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, c, clear, clear);
            }
        }
    }
}
