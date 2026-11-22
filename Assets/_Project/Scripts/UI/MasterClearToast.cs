using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// "Master clear!" banner on the world map: gold-framed plate near the top of the screen with the stage name and
    /// a row of its golden leaves; the frame's light sweep runs across it as it lands. Does not block raycasts; the world map controller pauses input while it plays.
    /// Unscaled time; Sample() drives it deterministically for editor captures.
    /// </summary>
    public sealed class MasterClearToast : MonoBehaviour
    {
        public const float InDuration = 0.4f, OutDuration = 0.35f;
        private const float Width = 820f, Height = 200f;
        private const int MaxLeaves = 5;

        private RectTransform host, content;
        private CanvasGroup group;
        private LocalizedText heading, stageName;
        private GoldUiFrame frame;
        private CollectibleSlot[] leaves;
        private float elapsed, hold;
        private bool closing;
        private float closeAt;

        public bool IsPlaying { get; private set; }
        /// <summary>Freezes the timeline (loading fade, popups, pause).</summary>
        public bool Suspended { get; set; }

        public static MasterClearToast Create(Transform parent)
        {
            var rect = UiKit.Rect("MasterClearToast", parent);
            GoldUiTheme.Stretch(rect);
            var view = UiKit.Ensure<MasterClearToast>(rect);
            view.Build();
            view.gameObject.SetActive(false);
            return view;
        }

        private void Build()
        {
            if (group != null) return;
            host = (RectTransform)transform;
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            content = UiKit.Rect("MasterBanner", transform);
            var plate = content.gameObject.AddComponent<Image>();
            plate.color = new Color(.05f, .042f, .03f, .94f);
            plate.raycastTarget = false;
            frame = GoldUiTheme.Frame(content);
            frame.color = GoldUiTheme.Gold;

            heading = UiKit.Label(content, "Heading", "worldmap.master_clear", 64, TextAnchor.MiddleCenter);
            heading.GetComponent<Text>().color = GoldUiTheme.Gold;
            stageName = UiKit.Label(content, "StageName", null, 28, TextAnchor.MiddleCenter);
            stageName.GetComponent<Text>().color = GoldUiTheme.Cream;

            leaves = new CollectibleSlot[MaxLeaves];
            for (int i = 0; i < MaxLeaves; i++)
            {
                var leaf = UiKit.Rect("Leaf" + i, content);
                leaves[i] = leaf.gameObject.AddComponent<CollectibleSlot>();
                leaves[i].raycastTarget = false;
                leaves[i].SetFilled(true);
            }
            Layout();
        }

        private void Layout()
        {
            float bw = host.rect.width, bh = host.rect.height;
            float fit = bw > 1f && bh > 1f ? Mathf.Min(1f, (bw - 64f) / Width) : 1f;
            UiKit.Place(content, .5f, .78f, 0f, 0f, Width, Height);
            LastFit = Mathf.Max(.1f, fit);
            UiKit.Place(heading.transform, .5f, .5f, 0f, 40f, Width - 60f, 92f);
            UiKit.Place(stageName.transform, .5f, .5f, 0f, -28f, Width - 120f, 40f);
        }
        private float LastFit { get; set; } = 1f;

        /// <param name="holdSeconds">Time the banner stays fully visible before it fades out.</param>
        public void Show(WorldMapNodeData node, float holdSeconds)
        {
            Build();
            var stage = node != null ? node.Stage : null;
            if (stage != null) stageName.SetKey(stage.DisplayNameKey);
            else stageName.SetText("");
            int count = stage != null ? Mathf.Clamp(stage.CollectibleCount, 0, MaxLeaves) : 0;
            for (int i = 0; i < MaxLeaves; i++)
            {
                leaves[i].gameObject.SetActive(i < count);
                // centred row of every collected golden leaf under the stage name
                UiKit.Place(leaves[i].transform, .5f, .5f, (i - (count - 1) * .5f) * 40f, -70f, 26f, 32f);
            }
            hold = Mathf.Max(0f, holdSeconds);
            elapsed = 0f;
            closing = false;
            IsPlaying = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Sample(0f);
            Sfx.Play(SfxId.Special);
        }

        /// <summary>Skips to the fade-out (input skip).</summary>
        public void Dismiss()
        {
            if (!IsPlaying || closing) return;
            closing = true;
            closeAt = elapsed;
        }

        private void Update()
        {
            if (!IsPlaying || Suspended) return;
            Tick(Time.unscaledDeltaTime);
        }

        public void Tick(float delta)
        {
            elapsed += Mathf.Max(0f, delta);
            if (!closing && elapsed >= InDuration + hold) { closing = true; closeAt = elapsed; }
            if (closing && elapsed - closeAt >= OutDuration) { Hide(); return; }
            Sample(elapsed);
        }

        public void Sample(float seconds)
        {
            Build();
            Layout();
            float appear = Mathf.Clamp01(seconds / InDuration);
            float ease = 1f - Mathf.Pow(1f - appear, 3f);
            float fadeOut = closing ? 1f - Mathf.SmoothStep(0f, 1f, (seconds - closeAt) / OutDuration) : 1f;
            group.alpha = Mathf.SmoothStep(0f, 1f, appear) * fadeOut;
            float pop = 1f + 0.18f * (1f - ease) + 0.03f * Mathf.Sin(Mathf.Clamp01((seconds - InDuration) / 0.3f) * Mathf.PI);
            content.localScale = Vector3.one * LastFit * pop;
            content.anchoredPosition = new Vector2(0f, 24f * (1f - ease) + (closing ? 16f * (1f - fadeOut) : 0f));
            // gold light sweep along the frame and a soft flash on the plate
            float sweep = Mathf.Clamp01((seconds - 0.25f) / 0.9f);
            frame.SetMotion(1f, sweep, 0f);
            heading.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Mathf.Clamp01((seconds - 0.15f) / 0.45f) * Mathf.PI));
        }

        private void Hide()
        {
            IsPlaying = false;
            closing = false;
            gameObject.SetActive(false);
        }

        private void OnDisable() => IsPlaying = false;
    }
}
