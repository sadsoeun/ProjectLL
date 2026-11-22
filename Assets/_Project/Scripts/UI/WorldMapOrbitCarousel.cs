using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Perspective stage plates and animated encounter silhouettes inside a clipped HUD viewport.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorldMapOrbitCarousel : MaskableGraphic
    {
        private readonly Text[] numbers = new Text[3];
        private readonly int[] displayedNumbers = { -1, -1, -1 };
        private WorldMapChartGraphic route;
        private WorldMapEncounterAtlas encounters;
        private int[][] routeModels;
        private double[] encounterAges;
        public override Texture mainTexture => encounters && encounters.texture ? encounters.texture : base.mainTexture;
        private float cursor, origin, elapsed, duration, phase;
        private float labelCursor = float.NaN;
        private Vector2 labelViewport;
        private int destination = -1, count;

        public void Bind(WorldMapChartGraphic chart)
        {
            route = chart;
            encounters = Resources.Load<WorldMapEncounterAtlas>("WorldMapEncounters");
            for (int i = 0; i < numbers.Length; i++)
            {
                var label = UiKit.Label(transform, "PlateNumber" + i, null, 32);
                numbers[i] = label.GetComponent<Text>();
                numbers[i].raycastTarget = false;
                var rect = numbers[i].rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(0, .5f);
                rect.sizeDelta = new Vector2(65, 44);
            }
        }

        public void SetEncounterStages(IReadOnlyList<string> stageIds)
        {
            routeModels = new int[stageIds.Count][];
            encounterAges = new double[stageIds.Count];
            for (int i = 0; i < stageIds.Count; i++)
                routeModels[i] = encounters ? encounters.ModelsFor(stageIds[i]) : System.Array.Empty<int>();
            SetAllDirty();
        }

        public void SelectStop(int index, int total)
        {
            if (destination == index && count == total) return;
            count = total;
            labelCursor = float.NaN;
            if (destination < 0) cursor = index;
            origin = cursor;
            destination = index;
            if (encounterAges != null && index >= 0 && index < encounterAges.Length)
                encounterAges[index] = 0;
            elapsed = 0;
            duration = Mathf.Min(1.45f, .72f + .09f * Mathf.Max(0, Mathf.Abs(index - origin) - 1));
            Advance(0);
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        // One clock keeps the cards, their labels and the route cursor in phase.
        public void Advance(float seconds)
        {
            if (destination < 0 || count == 0) return;
            if (encounterAges != null && destination < encounterAges.Length)
                encounterAges[destination] += seconds;
            phase = Mathf.Repeat(phase + seconds * .8f, Mathf.PI * 20);
            elapsed = Mathf.Min(duration, elapsed + seconds);
            cursor = Mathf.Lerp(origin, destination, Mathf.SmoothStep(0, 1, elapsed / duration));
            route.SetRoute(cursor, count);
            UpdateNumbers();
            SetVerticesDirty();
        }

        private void UpdateNumbers()
        {
            Rect r = rectTransform.rect;
            if (cursor == labelCursor && r.size == labelViewport) return;
            labelCursor = cursor;
            labelViewport = r.size;
            int middle = Mathf.RoundToInt(cursor);
            for (int slot = 0; slot < numbers.Length; slot++)
            {
                int stop = middle + slot - 1;
                Text label = numbers[slot];
                bool visible = stop >= 0 && stop < count;
                if (label.enabled != visible) label.enabled = visible;
                if (!visible) continue;
                if (displayedNumbers[slot] != stop)
                {
                    label.text = (stop + 1).ToString("00");
                    displayedNumbers[slot] = stop;
                }
                float lane = stop - cursor;
                var point = PlatePoint(r, lane, -.84f, .52f);
                var rect = label.rectTransform;
                rect.anchoredPosition = point - r.center;
                rect.localScale = Vector3.one * Mathf.Lerp(1, .82f, Mathf.Abs(lane));
                label.color = new Color(.96f, .96f, .93f, Mathf.Clamp01(1.2f - Mathf.Abs(lane)*.65f));
            }
        }

        private static Vector2 PlatePoint(Rect r, float lane, float x, float y)
        {
            float angle = Mathf.Clamp(lane, -1.5f, 1.5f) * .92f;
            float halfWidth = Mathf.Min(r.width * .32f, r.height * .83f);
            float perspective = 1f / (1f + x * Mathf.Sin(angle) * .27f);
            return r.center + new Vector2(lane * r.width * .62f + x * halfWidth * Mathf.Cos(angle) * perspective,
                y * r.height * .43f * perspective);
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (destination < 0) return;
            Rect r = rectTransform.rect;
            int middle = Mathf.RoundToInt(cursor);
            for (int stop = middle - 1; stop <= middle + 1; stop++)
            {
                if (stop < 0 || stop >= count) continue;
                float lane = stop - cursor;
                DrawPlate(mesh, r, lane, stop);
            }
        }

        private void DrawPlate(VertexHelper mesh, Rect r, float lane, int stop)
        {
            float opacity = Mathf.Clamp01(1.2f - Mathf.Abs(lane) * .65f);
            var frame = new Color(.78f, .69f, .52f, .65f * opacity);
            var a = PlatePoint(r, lane, -1, -1);
            var b = PlatePoint(r, lane, -1, 1);
            var c = PlatePoint(r, lane, 1, 1);
            var d = PlatePoint(r, lane, 1, -1);
            WorldMapChartGraphic.Segment(mesh, a, b, 1.25f, frame);
            WorldMapChartGraphic.Segment(mesh, b, c, 1.25f, frame);
            WorldMapChartGraphic.Segment(mesh, c, d, 1.25f, frame);
            WorldMapChartGraphic.Segment(mesh, d, a, 1.25f, frame);

            if (routeModels == null || stop >= routeModels.Length) return;
            var models = routeModels[stop];
            if (models.Length == 0) return;

            // Cycle only the species in this stage. Fade through the dark plate between creatures.
            const double hold = 3.6;
            double encounterTime = encounterAges[stop];
            double cycle = encounterTime / hold;
            int model = models[(int)(cycle % models.Length)];
            float age = (float)((cycle % 1) * hold);
            if (models.Length > 1)
            {
                if (encounterTime >= hold) opacity *= Mathf.SmoothStep(0, 1, age / .28f);
                opacity *= Mathf.SmoothStep(0, 1, ((float)hold - age) / .28f);
            }

            float angle = phase + model * .61f;
            float pose = (.5f + .5f * Mathf.Sin(angle)) * (encounters.viewCount - 1);
            int view = Mathf.FloorToInt(pose);
            float blend = pose - view;
            float lift = .035f * Mathf.Sin(angle * 2);
            float halfY = .91f;
            float halfX = halfY * r.height * .43f / Mathf.Max(1, Mathf.Min(r.width * .32f, r.height * .83f));
            a = PlatePoint(r, lane, .19f - halfX, lift - halfY);
            b = PlatePoint(r, lane, .19f - halfX, lift + halfY);
            c = PlatePoint(r, lane, .19f + halfX, lift + halfY);
            d = PlatePoint(r, lane, .19f + halfX, lift - halfY);
            DrawView(mesh, a, b, c, d, encounters.UV(model, view), opacity * Mathf.Sqrt(1 - blend));
            if (blend > 0)
                DrawView(mesh, a, b, c, d, encounters.UV(model, view + 1), opacity * Mathf.Sqrt(blend));
        }

        private static void DrawView(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Rect uv, float alpha)
        {
            var tint = new Color(1, 1, 1, alpha);
            int start = mesh.currentVertCount;
            mesh.AddVert(a, tint, uv.min);
            mesh.AddVert(b, tint, new Vector2(uv.xMin, uv.yMax));
            mesh.AddVert(c, tint, uv.max);
            mesh.AddVert(d, tint, new Vector2(uv.xMax, uv.yMin));
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
