using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// A small ring over the player's head that shows when the next dodge is available: it empties when a dodge starts,
    /// fills while the dodge and its cooldown run, flashes once when the dodge is back and then fades out. While the
    /// air dodge is spent it stays dim until she lands. Hidden whenever a dodge is simply available, so it never
    /// clutters normal play. View only: reads PlayerMotor, changes nothing. Built at runtime (no prefab).
    /// </summary>
    public sealed class DodgeCooldownIndicator : MonoBehaviour
    {
        [Header("Placement (world units)")]
        [Tooltip("Ring diameter.")]
        [SerializeField, Min(0.1f)] private float diameter = 0.66f;
        [Tooltip("Gap between the top of the player's collider and the ring centre.")]
        [SerializeField] private float heightAboveHead = 0.05f;
        [Tooltip("Offset toward the camera, so the ring is not buried in the character or in scenery at her depth.")]
        [SerializeField] private float depthOffset = -0.6f;

        [Header("Look")]
        [SerializeField] private Color trackColor = new Color(0.05f, 0.08f, 0.14f, 0.7f);
        [SerializeField] private Color fillColor = new Color(0.55f, 0.9f, 1f, 0.95f);
        [SerializeField] private Color readyColor = new Color(1f, 0.86f, 0.45f, 1f);
        [Tooltip("Ring colour while the air dodge is spent (waiting for the landing).")]
        [SerializeField] private Color blockedColor = new Color(0.75f, 0.75f, 0.75f, 0.55f);
        [Tooltip("Seconds the ring stays (flashing) after the dodge is back, before it fades out.")]
        [SerializeField, Min(0f)] private float readyHold = 0.18f;
        [SerializeField, Min(0.01f)] private float fadeSeconds = 0.2f;
        [Tooltip("Size pop of the flash when the dodge is back (1 = none).")]
        [SerializeField, Min(1f)] private float readyPop = 1.35f;

        private PlayerMotor motor;
        private CapsuleCollider capsule;
        private RectTransform ring;
        private Image track, fill;
        private CanvasGroup group;
        private bool wasReady = true;
        private float readyFor = 10f; // seconds since the dodge came back (large = long ago)

        public static DodgeCooldownIndicator Attach(PlayerMotor player)
        {
            var existing = player.GetComponentInChildren<DodgeCooldownIndicator>(true);
            if (existing != null)
                return existing;
            var go = new GameObject("DodgeCooldownIndicator", typeof(RectTransform));
            go.transform.SetParent(player.transform, false);
            var indicator = go.AddComponent<DodgeCooldownIndicator>();
            indicator.Build(player);
            return indicator;
        }

        private void Build(PlayerMotor player)
        {
            motor = player;
            capsule = player.GetComponent<CapsuleCollider>();
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 50;
            group = gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;
            group.alpha = 0f;
            ring = (RectTransform)transform;
            ring.sizeDelta = new Vector2(100f, 100f);

            var sprite = MakeRingSprite();
            track = NewImage("Track", sprite);
            fill = NewImage("Fill", sprite);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
        }

        private Image NewImage(string name, Sprite sprite)
        {
            var rt = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>An anti-aliased white ring (the Image colours tint it).</summary>
        private static Sprite MakeRingSprite()
        {
            const int size = 96;
            const float outer = 46f, inner = 26f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "DodgeRing", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            float c = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(outer - d) * Mathf.Clamp01(d - inner);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private void LateUpdate()
        {
            if (motor == null)
                return;
            float dt = Time.unscaledDeltaTime;
            float progress = motor.DodgeCooldownProgress;
            bool cooling = progress < 1f;
            bool blocked = !cooling && motor.DodgeBlockedInAir;
            bool ready = !cooling && !blocked;

            if (ready && !wasReady)
                readyFor = 0f;
            else if (ready)
                readyFor += dt;
            wasReady = ready;

            float targetAlpha, pop = 1f;
            if (cooling)
            {
                targetAlpha = 1f;
                fill.fillAmount = progress;
                fill.color = fillColor;
                track.color = trackColor;
            }
            else if (blocked)
            {
                targetAlpha = 1f;
                fill.fillAmount = 1f;
                fill.color = blockedColor;
                track.color = trackColor;
            }
            else
            {
                fill.fillAmount = 1f;
                fill.color = readyColor;
                targetAlpha = readyFor < readyHold ? 1f : 0f;
                float t = readyHold > 0f ? Mathf.Clamp01(readyFor / readyHold) : 1f;
                pop = Mathf.Lerp(readyPop, 1f, t);
            }
            group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, dt / fadeSeconds);

            // World placement: over the head, upright and facing the side camera whatever way she faces.
            Vector3 p = motor.transform.position;
            float top = capsule != null ? p.y + capsule.center.y + capsule.height * 0.5f : p.y + 1.6f;
            ring.position = new Vector3(p.x, top + heightAboveHead, p.z + depthOffset);
            ring.rotation = Quaternion.identity;
            float scale = diameter / 100f * pop;
            Vector3 parent = motor.transform.lossyScale;
            ring.localScale = new Vector3(scale / Mathf.Max(0.0001f, Mathf.Abs(parent.x)), scale / Mathf.Max(0.0001f, Mathf.Abs(parent.y)), 1f);
        }
    }
}
