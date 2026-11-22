using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// Boss HP bar: localized boss name above one continuous bar with the HP as a percentage. A hit drains the bar
    /// smoothly; a pale "damage" chunk lingers a moment and then shrinks after it. Built in code on its own overlay canvas
    /// (no scene or prefab changes needed), created by BossController when the fight starts and destroyed when it ends.
    /// </summary>
    public sealed class BossHud : MonoBehaviour
    {
        private static readonly Color Full = GoldUiTheme.Gold;
        private static readonly Color Low = new Color(.94f, .49f, .23f);
        private static readonly Color Chunk = new Color(1f, 0.93f, 0.75f, 0.95f);

        private const float PanelWidth = 760f;
        private const float PercentWidth = 120f;
        private const float DrainSpeed = 1.6f;   // bar fraction per second (fast, but readable)
        private const float ChunkDelay = 0.35f;  // the damage chunk lingers this long before it shrinks
        private const float ChunkSpeed = 0.8f;

        private RectTransform root;
        private RectTransform fill, chunk;
        private Image fillImage;
        private Text percent;
        private int maxHp = 1;
        private float target = 1f, shown = 1f, chunkShown = 1f, chunkWait;
        private float pulse;
        private InGameView gameView;

        public static BossHud Create(string nameKey, int maxHp)
        {
            var go = new GameObject("BossHud");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var hud = go.AddComponent<BossHud>();
            hud.Build(nameKey, Mathf.Max(1, maxHp));
            return hud;
        }

        private void Build(string nameKey, int max)
        {
            maxHp = max;
            root = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -120f);
            root.sizeDelta = new Vector2(PanelWidth, 100f);
            var plate=root.gameObject.AddComponent<Image>();plate.color=GoldUiTheme.Panel;plate.raycastTarget=false;
            GoldUiTheme.Frame(root).diamond=false;
            gameView=Object.FindFirstObjectByType<InGameView>();

            var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
            var nameRect = nameGo.GetComponent<RectTransform>();
            nameRect.SetParent(root, false);
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.sizeDelta = new Vector2(0f, 48f);
            var text = nameGo.GetComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            UiKit.TextStyle(nameRect,UiKit.HudSize,TextAnchor.MiddleCenter);
            text.color = GoldUiTheme.Cream;
            text.raycastTarget = false;
            var localized = nameGo.AddComponent<LocalizedText>();
            localized.SetKey(nameKey);
            if (!GameSystems.Exists)
            {
                LocalizationTable.TryGet(LocalizationTable.DefaultLanguage, nameKey, out string fallback);
                localized.SetText(fallback ?? nameKey);
            }

            // Bar track (left) + percentage (right) on the bottom row.
            var back = NewImage("Back", root, new Color(0f, 0f, 0f, 0.65f));
            back.anchorMin = new Vector2(0f, 0f);
            back.anchorMax = new Vector2(1f, 0f);
            back.pivot = new Vector2(0f, 0f);
            back.offsetMin = new Vector2(12f, 10f);
            back.offsetMax = new Vector2(-PercentWidth, 44f);

            var inner = NewImage("Inner", back, GoldUiTheme.Panel);
            inner.anchorMin = Vector2.zero;
            inner.anchorMax = Vector2.one;
            inner.offsetMin = new Vector2(4f, 4f);
            inner.offsetMax = new Vector2(-4f, -4f);
            chunk = NewImage("Damage", inner, Chunk);
            fill = NewImage("Fill", inner, Full);
            fillImage = fill.GetComponent<Image>();
            SetWidth(chunk, 1f);
            SetWidth(fill, 1f);

            var pctGo = new GameObject("Percent", typeof(RectTransform), typeof(Text));
            var pctRect = pctGo.GetComponent<RectTransform>();
            pctRect.SetParent(root, false);
            pctRect.anchorMin = new Vector2(1f, 0f);
            pctRect.anchorMax = new Vector2(1f, 0f);
            pctRect.pivot = new Vector2(1f, 0f);
            pctRect.anchoredPosition = new Vector2(-10f, 4f);
            pctRect.sizeDelta = new Vector2(PercentWidth - 14f, 46f);
            percent = pctGo.GetComponent<Text>();
            UiKit.TextStyle(pctRect, UiKit.HudSize, TextAnchor.MiddleRight);
            percent.font = FontProvider.Get(null);
            percent.fontStyle = FontStyle.Bold;
            percent.text = "100%";
        }

        private static RectTransform NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>Stretches a bar from the left edge to <paramref name="fraction"/> of its parent.</summary>
        private static void SetWidth(RectTransform rt, float fraction)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(Mathf.Clamp01(fraction), 1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Sets the remaining HP. The bar drains toward it; a drop pulses the panel.</summary>
        public void SetHp(int hp)
        {
            float next = Mathf.Clamp01(hp / (float)maxHp);
            if (next < target)
            {
                pulse = 1f;
                chunkWait = ChunkDelay;
            }
            else
            {
                shown = chunkShown = next; // refill (reset) jumps straight up
            }
            target = next;
        }

        private void Update()
        {
            if (root == null)return;
            if(gameView==null)gameView=Object.FindFirstObjectByType<InGameView>();
            bool visible=(gameView==null||gameView.IsHudVisible)&&(!GameSystems.Exists||!GameSystems.Instance.Popups.IsBlocking);
            root.gameObject.SetActive(visible);
            float width=((RectTransform)transform).rect.width;
            root.anchoredPosition=new Vector2(0,width<1240?-220:-120);

            float dt = Time.unscaledDeltaTime; // keeps draining during the stomp hit-stop / time stop
            shown = Mathf.MoveTowards(shown, target, dt * DrainSpeed);
            if (chunkWait > 0f)
                chunkWait -= dt;
            else
                chunkShown = Mathf.MoveTowards(chunkShown, shown, dt * ChunkSpeed);
            chunkShown = Mathf.Max(chunkShown, shown);
            SetWidth(fill, shown);
            SetWidth(chunk, chunkShown);
            fillImage.color = Color.Lerp(Low, Full, Mathf.InverseLerp(0.15f, 0.5f, shown));
            percent.text = Mathf.CeilToInt(shown * 100f - 0.001f) + "%";

            pulse = Mathf.MoveTowards(pulse, 0f, dt * 3f);
            root.localScale = Vector3.one * Mathf.Min(1,Mathf.Max(.1f,width/900f)) * (1f + 0.04f * pulse);
        }
    }
}
