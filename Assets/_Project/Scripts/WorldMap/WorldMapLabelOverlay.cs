using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>
    /// Node ids ("4-1") drawn as crisp screen-space badges that follow the nodes: a rounded dark pill with bold white
    /// text, rendered at screen resolution so it stays sharp and never smears at a distance (unlike 3D TextMesh).
    /// Built at runtime by WorldMapController from each node's 3D "Label" text, which is then hidden.
    /// </summary>
    [DefaultExecutionOrder(60)] // after WorldMapCameraRig (50) / CameraFollow have placed the camera
    public sealed class WorldMapLabelOverlay : MonoBehaviour
    {
        [SerializeField] private int fontSize = 30;
        [SerializeField] private Color textColor = GoldUiTheme.Cream;
        [SerializeField] private Color badgeColor = GoldUiTheme.Panel;
        [SerializeField] private Color bossBadgeColor = new Color(.26f,.20f,.10f,.95f);
        [Tooltip("Badge anchor height above the node (m).")]
        [SerializeField] private float worldHeight = 2.1f;
        [Tooltip("Badge scale at the near / far camera distance (m); in between it is interpolated.")]
        [SerializeField] private Vector2 scaleNearFar = new Vector2(1f, 0.72f);
        [SerializeField] private Vector2 distanceNearFar = new Vector2(10f, 60f);
        [Tooltip("Sorting order of the overlay canvas (below the HUD and menus).")]
        [SerializeField] private int sortingOrder = -10;

        private struct Entry
        {
            public Transform node;
            public RectTransform badge;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private Camera cam;
        private RectTransform canvasRect;
        private static Sprite pill;

        public void Build(Camera targetCamera, IEnumerable<WorldMapNode> nodes)
        {
            cam = targetCamera;
            var canvasGo = new GameObject("NodeLabelCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasRect = (RectTransform)canvasGo.transform;
            var font = FontProvider.Get(null);

            foreach (var n in nodes)
            {
                if (n == null)
                    continue;
                var label3D = n.transform.Find("Label");
                var mesh = label3D != null ? label3D.GetComponent<TextMesh>() : null;
                if (mesh == null)
                    continue;
                bool boss = n.Data.NodeId.EndsWith("_B", System.StringComparison.Ordinal);
                entries.Add(new Entry { node = n.transform, badge = CreateBadge(mesh.text, boss, font) });
                label3D.gameObject.SetActive(false);
            }
        }

        private RectTransform CreateBadge(string text, bool boss, Font font)
        {
            var go = new GameObject("Badge_" + text, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(canvasRect, false);
            rt.anchorMin = rt.anchorMax = Vector2.zero; // positioned in canvas pixels from the bottom-left
            rt.pivot = new Vector2(0.5f, 0f);
            var bg = go.AddComponent<Image>();
            bg.sprite = Pill();
            bg.type = Image.Type.Sliced;
            bg.color = boss ? bossBadgeColor : badgeColor;
            bg.raycastTarget = false; // clicks go through to the node pick
            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 4, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(rt, false);
            var t = textGo.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = fontSize;
            t.fontStyle = FontStyle.Bold;
            t.color = boss ? GoldUiTheme.Gold : textColor;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return rt;
        }

        private void LateUpdate()
        {
            if (cam == null || canvasRect == null)
                return;
            Vector2 canvasSize = canvasRect.rect.size;
            foreach (var e in entries)
            {
                Vector3 world = e.node.position + Vector3.up * worldHeight;
                Vector3 sp = cam.WorldToScreenPoint(world);
                bool visible = sp.z > 0f && sp.x > -100f && sp.x < Screen.width + 100f && sp.y > -100f && sp.y < Screen.height + 100f;
                if (e.badge.gameObject.activeSelf != visible)
                    e.badge.gameObject.SetActive(visible);
                if (!visible)
                    continue;
                e.badge.anchoredPosition = new Vector2(sp.x / Screen.width * canvasSize.x, sp.y / Screen.height * canvasSize.y);
                float k = Mathf.InverseLerp(distanceNearFar.x, distanceNearFar.y, sp.z);
                e.badge.localScale = Vector3.one * Mathf.Lerp(scaleNearFar.x, scaleNearFar.y, k);
            }
        }

        /// <summary>Procedural rounded pill (64x64, 9-sliced) so no sprite asset is needed.</summary>
        private static Sprite Pill()
        {
            if (pill != null)
                return pill;
            const int size = 64;
            const float r = 31.5f;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    byte a = (byte)(Mathf.Clamp01(r - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            pill = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(31, 31, 31, 31));
            return pill;
        }
    }
}
