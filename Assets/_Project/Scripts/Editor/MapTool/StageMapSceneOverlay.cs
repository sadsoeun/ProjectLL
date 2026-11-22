using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Scene view part of the map tool (design 7.2 to 7.4, 7.7): draws the layout as Handles on the Z = 0 plane and turns
    /// mouse / key input into controller calls. It never changes the model itself. Created by the window; subscribes to
    /// SceneView.duringSceneGui in the constructor and unsubscribes in Dispose.
    /// </summary>
    internal sealed class StageMapSceneOverlay : IDisposable
    {
        private enum DragMode
        {
            None,
            Body,
            Handle,
        }

        private const float PickPixels = 6f;
        private const float HandlePixels = 9f;
        private const float DragStartPixels = 3f;
        private const float GridMinPixels = 10f;
        private const float LabelMinPixelsPerMeter = 14f;
        private const int MaxGridLines = 400;
        private const int MaxSpikeTeeth = 400;

        private static readonly Color Outline = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color SelectionColor = new Color(1f, 0.92f, 0.1f, 1f);
        private static readonly Color SpawnColor = new Color(0.25f, 0.5f, 1f, 0.9f);
        private static readonly Color PatrolEnemyColor = new Color(0.55f, 0.25f, 0.85f, 0.95f);
        private static readonly Color ChaserEnemyColor = new Color(0.82f, 0.1f, 0.25f, 0.95f);
        private static readonly Color DecorationColor = new Color(0.3f, 0.85f, 0.55f, 1f);
        private const float DecorationMarkerPeak = 0.6f;

        private readonly StageMapController controller;
        private readonly int controlHint = "MyGame.MapTool.SceneOverlay".GetHashCode();

        private readonly Vector3[] quad = new Vector3[4];
        private readonly Vector3[] tri = new Vector3[3];
        private readonly Vector3[] line = new Vector3[2];
        private readonly Vector3[] loop = new Vector3[5];
        private readonly List<LayoutGeometry.HandleInfo> handles = new List<LayoutGeometry.HandleInfo>();

        private GUIStyle labelStyle;
        private GUIStyle hudStyle;

        // Pointer state
        private LayoutElementRef hover;
        private Vector2 cursorWorld;
        private bool cursorValid;

        // Drag state
        private DragMode dragMode;
        private LayoutElementRef dragRef;
        private LayoutHandle dragHandle;
        private Vector2 grabOffset;
        private Vector2 downGui;
        private bool dragStarted;

        // Per-call view info
        private Rect visible;
        private float pxToWorld = 0.05f;
        private bool disposed;

        public StageMapSceneOverlay(StageMapController controller)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            SceneView.duringSceneGui += OnSceneGUI;
            controller.Changed += RepaintAll;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            SceneView.duringSceneGui -= OnSceneGUI;
            controller.Changed -= RepaintAll;
            SceneView.RepaintAll();
        }

        private static void RepaintAll() => SceneView.RepaintAll();

        // ------------------------------------------------------------------ framing

        private static SceneView ActiveSceneView()
        {
            if (SceneView.lastActiveSceneView != null)
                return SceneView.lastActiveSceneView;
            return SceneView.sceneViews.Count > 0 ? SceneView.sceneViews[0] as SceneView : null;
        }

        /// <summary>2D view fitted to the whole stage (the "2D 보기" button).</summary>
        public void FrameStage()
        {
            var d = controller.Data;
            var sv = ActiveSceneView();
            if (d == null || sv == null)
                return;
            LookAtRect(sv, LayoutGeometry.StageBounds(d), 2f);
        }

        /// <summary>Centres the view on the selected element (F key / list double click).</summary>
        public void FrameSelection()
        {
            var d = controller.Data;
            var sv = ActiveSceneView();
            var sel = controller.Selection;
            if (d == null || sv == null || sel.IsNone || !sel.IsValid(d))
                return;
            Rect r = LayoutGeometry.GetBounds(d, sel);
            if (sel.kind == LayoutElementKind.Section)
                r = LayoutGeometry.StageBounds(d); // the pick strip is at the top; frame the whole band height instead
            LookAtRect(sv, r, 5f);
        }

        private static void LookAtRect(SceneView sv, Rect r, float minHalfHeight)
        {
            sv.in2DMode = true;
            float aspect = sv.position.height > 1f ? sv.position.width / sv.position.height : 1.6f;
            float half = Mathf.Max(minHalfHeight, r.height * 0.5f * 1.15f, r.width * 0.5f * 1.15f / Mathf.Max(aspect, 0.1f));
            sv.LookAt(new Vector3(r.center.x, r.center.y, 0f), Quaternion.identity, half, true, true);
        }

        // ------------------------------------------------------------------ SceneView entry

        private void OnSceneGUI(SceneView sv)
        {
            if (disposed || !controller.HasDocument)
                return;
            var e = Event.current;
            UpdateView(sv);
            if (e.type == EventType.Repaint)
                Draw(sv);
            if (controller.Session.editEnabled && !StageMapController.IsLocked)
                HandleInput(sv, e);
        }

        private void UpdateView(SceneView sv)
        {
            float w = sv.position.width;
            float h = sv.position.height;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            bool ok = true;
            for (int k = 0; k < 4; k++)
            {
                var c = new Vector2((k & 1) * w, (k >> 1) * h);
                if (!TryWorld(c, out var p))
                {
                    ok = false;
                    break;
                }
                minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x);
                minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y);
            }
            if (!ok || maxX <= minX)
            {
                visible = new Rect(-1000f, -1000f, 2000f, 2000f);
                return;
            }
            visible = new Rect(minX, minY, maxX - minX, maxY - minY);
            pxToWorld = Mathf.Max(1e-5f, (maxX - minX) / Mathf.Max(1f, w));
        }

        private static bool TryWorld(Vector2 gui, out Vector2 world)
        {
            var ray = HandleUtility.GUIPointToWorldRay(gui);
            if (Mathf.Abs(ray.direction.z) < 1e-6f)
            {
                world = default;
                return false;
            }
            float t = -ray.origin.z / ray.direction.z;
            var p = ray.origin + ray.direction * t;
            world = new Vector2(p.x, p.y);
            return true;
        }

        private bool OnScreen(Rect r)
        {
            float pad = 2f;
            return r.xMax >= visible.xMin - pad && r.xMin <= visible.xMax + pad && r.yMax >= visible.yMin - pad && r.yMin <= visible.yMax + pad;
        }

        // ------------------------------------------------------------------ drawing

        private void Draw(SceneView sv)
        {
            var d = controller.Data;
            var session = controller.Session;
            var prevZTest = Handles.zTest;
            var prevColor = Handles.color;
            Handles.zTest = CompareFunction.Always;
            EnsureStyles();
            float pxPerMeter = 1f / pxToWorld;
            bool labels = session.showLabels && pxPerMeter >= LabelMinPixelsPerMeter;

            DrawGrid(session.grid);
            DrawSections(d, session.showSections, labels);
            DrawStageLines(d);

            foreach (var dec in d.decorations) DrawDecoration(dec, labels);
            foreach (var b in d.blocks) DrawBlock(b, labels, pxPerMeter);
            foreach (var m in d.movers) DrawMover(m, labels);
            foreach (var s in d.spikes) DrawSpike(s, labels);
            foreach (var f in d.fallers) DrawFaller(f, labels);
            foreach (var t in d.spikeTraps) DrawSpikeTrap(t, labels);
            foreach (var g in d.gates) DrawGate(g, labels);
            foreach (var sw in d.switches) DrawSwitch(sw, labels);
            if (d.HasBoss) DrawBoss(d.boss, labels);
            foreach (var c in d.coins) DrawCoin(c);
            foreach (var s in d.specials) DrawSpecial(s, labels);
            foreach (var c in d.checkpoints) DrawCheckpoint(c, labels);
            foreach (var en in d.enemies) DrawEnemy(en, labels);
            DrawSpawnGoal(d);

            DrawErrorMarkers(d);
            DrawHoverAndSelection(d);
            DrawPlacementPreview(d, session);
            DrawHud(sv, session);

            Handles.color = prevColor;
            Handles.zTest = prevZTest;
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10, richText = false };
                labelStyle.normal.textColor = Color.white;
            }
            if (hudStyle == null)
            {
                hudStyle = new GUIStyle(EditorStyles.helpBox) { fontSize = 11, alignment = TextAnchor.MiddleLeft };
                hudStyle.normal.textColor = Color.white;
            }
        }

        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private void FillRect(Rect r, Color face, Color outline)
        {
            quad[0] = new Vector3(r.xMin, r.yMin, 0f);
            quad[1] = new Vector3(r.xMax, r.yMin, 0f);
            quad[2] = new Vector3(r.xMax, r.yMax, 0f);
            quad[3] = new Vector3(r.xMin, r.yMax, 0f);
            Handles.DrawSolidRectangleWithOutline(quad, face, outline);
        }

        private void Line(Vector2 a, Vector2 b, Color color, float width)
        {
            line[0] = new Vector3(a.x, a.y, 0f);
            line[1] = new Vector3(b.x, b.y, 0f);
            Handles.color = color;
            Handles.DrawAAPolyLine(width, line);
        }

        private void Dotted(Vector2 a, Vector2 b, Color color)
        {
            Handles.color = color;
            Handles.DrawDottedLine(new Vector3(a.x, a.y, 0f), new Vector3(b.x, b.y, 0f), 4f);
        }

        private void OutlineRect(Rect r, Color color, float width)
        {
            loop[0] = new Vector3(r.xMin, r.yMin, 0f);
            loop[1] = new Vector3(r.xMax, r.yMin, 0f);
            loop[2] = new Vector3(r.xMax, r.yMax, 0f);
            loop[3] = new Vector3(r.xMin, r.yMax, 0f);
            loop[4] = loop[0];
            Handles.color = color;
            Handles.DrawAAPolyLine(width, loop);
        }

        private void Label(Vector2 pos, string text)
        {
            Handles.Label(new Vector3(pos.x, pos.y, 0f), text, labelStyle);
        }

        private void DrawGrid(float grid)
        {
            if (grid <= 0f || grid / pxToWorld < GridMinPixels)
                return;
            Handles.color = new Color(1f, 1f, 1f, 0.07f);
            int n = 0;
            float x0 = Mathf.Floor(visible.xMin / grid) * grid;
            for (float x = x0; x <= visible.xMax && n < MaxGridLines; x += grid, n++)
                Handles.DrawLine(new Vector3(x, visible.yMin, 0f), new Vector3(x, visible.yMax, 0f));
            n = 0;
            float y0 = Mathf.Floor(visible.yMin / grid) * grid;
            for (float y = y0; y <= visible.yMax && n < MaxGridLines; y += grid, n++)
                Handles.DrawLine(new Vector3(visible.xMin, y, 0f), new Vector3(visible.xMax, y, 0f));
        }

        private void DrawSections(StageLayoutData d, bool show, bool labels)
        {
            if (!show)
                return;
            float top = LayoutGeometry.SectionBandTop(d);
            for (int i = 0; i < d.sections.Length; i++)
            {
                var s = d.sections[i];
                var band = Rect.MinMaxRect(s.startX, d.killY, s.endX, top);
                if (!OnScreen(band))
                    continue;
                Color tint = i % 2 == 0 ? new Color(0.3f, 0.55f, 1f, 0.07f) : new Color(0.3f, 1f, 0.6f, 0.07f);
                FillRect(band, tint, new Color(1f, 1f, 1f, 0.12f));
                if (labels)
                    Label(new Vector2(s.startX + 0.2f, top - 0.2f), string.IsNullOrEmpty(s.note) ? s.id : $"{s.id}  {s.note}");
            }
        }

        private void DrawStageLines(StageLayoutData d)
        {
            float top = LayoutGeometry.SectionBandTop(d);
            Color edge = new Color(1f, 1f, 1f, 0.55f);
            Line(new Vector2(0f, d.killY), new Vector2(0f, top), edge, 2f);
            Line(new Vector2(d.length, d.killY), new Vector2(d.length, top), edge, 2f);
            Dotted(new Vector2(-2f, d.killY), new Vector2(d.length + 2f, d.killY), new Color(1f, 0.2f, 0.2f, 0.9f));
            Dotted(new Vector2(-2f, d.cameraMinY), new Vector2(d.length + 2f, d.cameraMinY), new Color(0.75f, 0.75f, 0.75f, 0.7f));
            if (pxToWorld < 0.1f)
            {
                Label(new Vector2(0.3f, d.killY + 0.3f), "killY " + d.killY.ToString("0.##"));
                Label(new Vector2(0.3f, d.cameraMinY + 0.3f), "cameraMinY " + d.cameraMinY.ToString("0.##"));
            }
        }

        private void DrawBlock(LayoutBlock b, bool labels, float pxPerMeter)
        {
            var rect = Rect.MinMaxRect(b.x, b.y - b.h, b.x + b.w, b.y);
            if (!OnScreen(rect))
                return;
            Color c = b.kind == "wall" ? StageLayoutBuilder.Palette.Wall : b.kind == "platform" ? StageLayoutBuilder.Palette.Platform : StageLayoutBuilder.Palette.Ground;
            FillRect(rect, WithAlpha(c, 0.85f), Outline);
            Line(new Vector2(rect.xMin, b.y), new Vector2(rect.xMax, b.y), c * 0.55f, 3f);
            if (labels && b.w * pxPerMeter >= 70f)
                Label(new Vector2(b.x + 0.15f, b.y - 0.15f), b.id);
        }

        // Decorations live at their own z; the overlay is a Z = 0 view, so only x / y are shown. Far layers (z > 2) are faded.
        private void DrawDecoration(LayoutDecoration dec, bool labels)
        {
            Rect marker = LayoutGeometry.DecorationMarker(dec.x, dec.y);
            Rect? local = controller.Props.LocalBounds(dec.prop);
            Rect extent = marker;
            bool hasExtent = false;
            if (local.HasValue && dec.scale > 0f && !float.IsNaN(dec.scale) && !float.IsInfinity(dec.scale))
            {
                Rect l = local.Value;
                extent = Rect.MinMaxRect(dec.x + l.xMin * dec.scale, dec.y + l.yMin * dec.scale, dec.x + l.xMax * dec.scale, dec.y + l.yMax * dec.scale);
                hasExtent = true;
            }
            if (!OnScreen(marker) && !(hasExtent && OnScreen(extent)))
                return;

            float z = controller.Props.ResolveZ(dec);
            float fade = z > 2f ? 0.35f : 1f;
            Color c = DecorationColor;
            if (hasExtent)
                FillRect(extent, WithAlpha(c, 0.10f * fade), WithAlpha(c, 0.7f * fade));
            Handles.color = WithAlpha(c, 0.95f * fade);
            tri[0] = new Vector3(dec.x - 0.3f, dec.y, 0f);
            tri[1] = new Vector3(dec.x + 0.3f, dec.y, 0f);
            tri[2] = new Vector3(dec.x, dec.y + DecorationMarkerPeak, 0f);
            Handles.DrawAAConvexPolygon(tri);
            if (labels)
                Label(new Vector2(dec.x - 0.3f, dec.y + DecorationMarkerPeak + 0.2f), $"{dec.prop} z{z:0.#}");
        }

        private void DrawMover(LayoutMover m, bool labels)
        {
            var rect = Rect.MinMaxRect(m.x, m.y - LayoutGeometry.MoverThickness, m.x + m.w, m.y);
            Vector2 ghostCenter = LayoutGeometry.MoverGhostCenter(m);
            var ghost = new Rect(ghostCenter.x - m.w * 0.5f, ghostCenter.y - LayoutGeometry.MoverThickness * 0.5f, m.w, LayoutGeometry.MoverThickness);
            if (!OnScreen(rect) && !OnScreen(ghost))
                return;
            Color c = StageLayoutBuilder.Palette.Mover;
            FillRect(rect, WithAlpha(c, 0.9f), Outline);
            FillRect(ghost, WithAlpha(c, 0.3f), WithAlpha(c, 0.6f));
            Dotted(rect.center, ghost.center, WithAlpha(c, 0.9f));
            if (labels)
                Label(new Vector2(m.x, m.y + 0.1f), m.id);
        }

        private void DrawSpike(LayoutSpike s, bool labels)
        {
            var rect = Rect.MinMaxRect(s.x, s.y, s.x + s.w, s.y + LayoutGeometry.SpikeHeight);
            if (!OnScreen(rect))
                return;
            int teeth = Mathf.Clamp(Mathf.RoundToInt(s.w / LayoutGeometry.SpikeUnit), 1, MaxSpikeTeeth);
            float step = s.w / teeth;
            Handles.color = StageLayoutBuilder.Palette.Spike;
            for (int i = 0; i < teeth; i++)
            {
                float x = s.x + i * step;
                tri[0] = new Vector3(x, s.y, 0f);
                tri[1] = new Vector3(x + step, s.y, 0f);
                tri[2] = new Vector3(x + step * 0.5f, s.y + LayoutGeometry.SpikeHeight, 0f);
                Handles.DrawAAConvexPolygon(tri);
            }
            if (labels)
                Label(new Vector2(s.x, s.y + LayoutGeometry.SpikeHeight + 0.1f), s.id);
        }

        private void DrawSpikeTrap(LayoutSpikeTrap t, bool labels)
        {
            var rect = Rect.MinMaxRect(t.x, t.y, t.x + t.w, t.y + LayoutGeometry.SpikeTrapHeight);
            if (!OnScreen(rect))
                return;
            Color c = StageLayoutBuilder.Palette.Spike;
            FillRect(rect, WithAlpha(c, 0.35f), WithAlpha(c, 0.9f));
            int teeth = Mathf.Clamp(Mathf.RoundToInt(t.w / LayoutGeometry.SpikeUnit), 1, MaxSpikeTeeth);
            float step = t.w / teeth;
            Handles.color = WithAlpha(c, 0.8f);
            for (int i = 0; i < teeth; i += 2)
            {
                tri[0] = new Vector3(t.x + i * step, t.y, 0f);
                tri[1] = new Vector3(t.x + (i + 1) * step, t.y, 0f);
                tri[2] = new Vector3(t.x + (i + 0.5f) * step, t.y + LayoutGeometry.SpikeTrapHeight, 0f);
                Handles.DrawAAConvexPolygon(tri);
            }
            if (labels)
                Label(new Vector2(t.x, t.y + LayoutGeometry.SpikeTrapHeight + 0.1f), $"{t.id} T{t.period:0.#}");
        }

        private void DrawSwitch(LayoutSwitch s, bool labels)
        {
            var rect = Rect.MinMaxRect(s.x - 0.5f, s.y, s.x + 0.5f, s.y + LayoutGeometry.SwitchHeight);
            if (!OnScreen(rect))
                return;
            FillRect(rect, new Color(1f, 0.85f, 0.25f, 0.95f), Outline);
            if (labels)
                Label(new Vector2(s.x - 0.5f, s.y + 0.45f), $"{s.id} ch{s.channel}");
        }

        private void DrawGate(LayoutGate g, bool labels)
        {
            var rect = Rect.MinMaxRect(g.x - 0.5f, g.y, g.x + 0.5f, g.y + g.h);
            if (!OnScreen(rect))
                return;
            FillRect(rect, new Color(0.75f, 0.6f, 0.35f, 0.9f), Outline);
            if (labels)
                Label(new Vector2(g.x - 0.5f, g.y + g.h + 0.2f), $"{g.id} ch{g.channel}");
        }

        private void DrawBoss(LayoutBoss b, bool labels)
        {
            var arena = Rect.MinMaxRect(b.arenaMinX, b.y, b.arenaMaxX, b.y + 8f);
            var body = Rect.MinMaxRect(b.x - LayoutGeometry.BossWidth * 0.5f, b.y, b.x + LayoutGeometry.BossWidth * 0.5f, b.y + LayoutGeometry.BossHeight);
            if (!OnScreen(arena) && !OnScreen(body))
                return;
            Color c = new Color(0.85f, 0.2f, 0.45f);
            FillRect(arena, WithAlpha(c, 0.07f), WithAlpha(c, 0.6f));
            FillRect(body, WithAlpha(c, 0.85f), Outline);
            if (labels)
                Label(new Vector2(b.x - 1f, b.y + LayoutGeometry.BossHeight + 0.2f), $"boss {b.bossId}");
        }

        private void DrawFaller(LayoutFaller f, bool labels)
        {
            var rect = Rect.MinMaxRect(f.x - LayoutGeometry.FallerSize * 0.5f, f.y - LayoutGeometry.FallerSize * 0.5f,
                f.x + LayoutGeometry.FallerSize * 0.5f, f.y + LayoutGeometry.FallerSize * 0.5f);
            var zone = Rect.MinMaxRect(f.x - f.triggerHalfWidth, f.y - 3f, f.x + f.triggerHalfWidth, f.y + 3f);
            if (!OnScreen(rect) && !OnScreen(zone))
                return;
            Color c = StageLayoutBuilder.Palette.Faller;
            FillRect(rect, WithAlpha(c, 0.9f), Outline);
            Dotted(new Vector2(zone.xMin, zone.yMin), new Vector2(zone.xMin, zone.yMax), WithAlpha(c, 0.7f));
            Dotted(new Vector2(zone.xMax, zone.yMin), new Vector2(zone.xMax, zone.yMax), WithAlpha(c, 0.7f));
            if (labels)
                Label(new Vector2(f.x - 0.6f, f.y + 0.9f), f.id);
        }

        private void DrawCoin(LayoutCoin c)
        {
            if (!OnScreen(Rect.MinMaxRect(c.x - 0.5f, c.y - 0.5f, c.x + 0.5f, c.y + 0.5f)))
                return;
            Handles.color = WithAlpha(StageLayoutBuilder.Palette.Coin, 0.95f);
            Handles.DrawSolidDisc(new Vector3(c.x, c.y, 0f), Vector3.forward, LayoutGeometry.CoinRadius * 0.8f);
        }

        private void DrawSpecial(LayoutSpecial s, bool labels)
        {
            if (!OnScreen(Rect.MinMaxRect(s.x - 0.6f, s.y - 0.6f, s.x + 0.6f, s.y + 0.6f)))
                return;
            const float r = 0.55f;
            Handles.color = StageLayoutBuilder.Palette.Special;
            quad[0] = new Vector3(s.x - r, s.y, 0f);
            quad[1] = new Vector3(s.x, s.y - r, 0f);
            quad[2] = new Vector3(s.x + r, s.y, 0f);
            quad[3] = new Vector3(s.x, s.y + r, 0f);
            Handles.DrawAAConvexPolygon(quad);
            Label(new Vector2(s.x - 0.12f, s.y + 0.2f), s.slot.ToString());
            if (labels)
                Label(new Vector2(s.x + 0.6f, s.y + 0.2f), s.id);
        }

        private void DrawCheckpoint(LayoutCheckpoint c, bool labels)
        {
            var rect = Rect.MinMaxRect(c.x - 0.8f, c.y, c.x + 0.8f, c.y + LayoutGeometry.CheckpointHeight);
            if (!OnScreen(rect))
                return;
            Line(new Vector2(c.x, c.y), new Vector2(c.x, c.y + LayoutGeometry.CheckpointHeight), StageLayoutBuilder.Palette.CheckpointPole, 3f);
            Handles.color = StageLayoutBuilder.Palette.CheckpointOn;
            tri[0] = new Vector3(c.x, c.y + LayoutGeometry.CheckpointHeight, 0f);
            tri[1] = new Vector3(c.x + 0.8f, c.y + LayoutGeometry.CheckpointHeight - 0.4f, 0f);
            tri[2] = new Vector3(c.x, c.y + LayoutGeometry.CheckpointHeight - 0.8f, 0f);
            Handles.DrawAAConvexPolygon(tri);
            if (labels)
                Label(new Vector2(c.x + 0.1f, c.y + LayoutGeometry.CheckpointHeight + 0.2f), c.id);
        }

        private void DrawEnemy(LayoutEnemy e, bool labels)
        {
            var rect = Rect.MinMaxRect(e.x - 0.5f, e.y, e.x + 0.5f, e.y + LayoutGeometry.EnemySize);
            var patrol = Rect.MinMaxRect(e.x, e.y, e.x + e.range, e.y + 1f);
            if (!OnScreen(rect) && !OnScreen(patrol))
                return;
            Color c = e.ResolvedMonsterId == 1002 ? ChaserEnemyColor : PatrolEnemyColor;
            FillRect(rect, c, Outline);
            float midY = e.y + LayoutGeometry.EnemySize * 0.5f;
            Dotted(new Vector2(e.x, midY), new Vector2(e.x + e.range, midY), WithAlpha(c, 0.9f));
            Handles.color = c;
            const float r = 0.22f;
            quad[0] = new Vector3(e.x + e.range - r, midY, 0f);
            quad[1] = new Vector3(e.x + e.range, midY - r, 0f);
            quad[2] = new Vector3(e.x + e.range + r, midY, 0f);
            quad[3] = new Vector3(e.x + e.range, midY + r, 0f);
            Handles.DrawAAConvexPolygon(quad);
            if (labels)
                Label(new Vector2(e.x - 0.5f, e.y + LayoutGeometry.EnemySize + 0.2f), $"{e.id} · {e.ResolvedMonsterId}");
        }

        private void DrawSpawnGoal(StageLayoutData d)
        {
            var spawn = LayoutGeometry.GetBounds(d, new LayoutElementRef(LayoutElementKind.Spawn, 0));
            if (OnScreen(spawn))
            {
                FillRect(spawn, SpawnColor, Outline);
                Label(new Vector2(d.spawn.x - 0.1f, d.spawn.y + 1.2f), "S");
            }
            var goal = LayoutGeometry.GetBounds(d, new LayoutElementRef(LayoutElementKind.Goal, 0));
            if (OnScreen(goal))
            {
                FillRect(goal, WithAlpha(StageLayoutBuilder.Palette.Goal, 0.9f), Outline);
                Label(new Vector2(d.goal.x - 0.1f, d.goal.y + 1.7f), "G");
            }
        }

        private void DrawErrorMarkers(StageLayoutData d)
        {
            float radius = 8f * pxToWorld;
            foreach (var issue in controller.Issues)
            {
                if (issue.Severity != IssueSeverity.Error || issue.Target.IsNone || !issue.Target.IsValid(d))
                    continue;
                Rect b = LayoutGeometry.GetBounds(d, issue.Target);
                if (!OnScreen(b))
                    continue;
                var pos = new Vector3(b.center.x, b.yMax + radius * 1.2f, 0f);
                Handles.color = new Color(0.9f, 0.1f, 0.1f, 1f);
                Handles.DrawSolidDisc(pos, Vector3.forward, radius);
                Handles.Label(pos + new Vector3(-radius * 0.25f, radius * 0.6f, 0f), "!", labelStyle);
            }
        }

        private void DrawHoverAndSelection(StageLayoutData d)
        {
            if (!hover.IsNone && hover.IsValid(d) && hover != controller.Selection && dragMode == DragMode.None)
                OutlineRect(Expand(LayoutGeometry.GetBounds(d, hover), 0.04f), new Color(1f, 1f, 1f, 0.9f), 2f);

            var sel = controller.Selection;
            if (sel.IsNone || !sel.IsValid(d))
                return;
            Rect bounds = LayoutGeometry.GetBounds(d, sel);
            OutlineRect(Expand(bounds, 0.06f), SelectionColor, 4f);
            if (sel.kind == LayoutElementKind.Section)
            {
                var s = d.sections[sel.index];
                OutlineRect(Rect.MinMaxRect(s.startX, d.killY, s.endX, LayoutGeometry.SectionBandTop(d)), WithAlpha(SelectionColor, 0.5f), 2f);
            }
            LayoutGeometry.GetHandles(d, sel, handles);
            float half = 4f * pxToWorld;
            foreach (var h in handles)
            {
                bool active = dragMode == DragMode.Handle && dragHandle == h.Kind;
                FillRect(Rect.MinMaxRect(h.Position.x - half, h.Position.y - half, h.Position.x + half, h.Position.y + half),
                    active ? new Color(1f, 0.55f, 0.1f, 1f) : Color.white, Color.black);
            }
        }

        private static Rect Expand(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        private void DrawPlacementPreview(StageLayoutData d, StageMapSession session)
        {
            if (session.tool == MapPaletteTool.Select || !cursorValid || dragMode != DragMode.None)
                return;
            bool snapOn = session.snap ^ (Event.current.control || Event.current.command);
            Vector2 p = controller.ResolvePlacement(session.tool, cursorWorld, snapOn);
            Rect r = LayoutGeometry.DefaultBounds(session.tool, p);
            FillRect(r, new Color(1f, 1f, 1f, 0.25f), new Color(1f, 1f, 1f, 0.9f));
            string what = session.tool == MapPaletteTool.Decoration ? $"Decoration {session.propId}" : session.tool.ToString();
            Label(new Vector2(r.xMin, r.yMax + 0.2f), $"{what} ({p.x:0.##}, {p.y:0.##})");
        }

        private void DrawHud(SceneView sv, StageMapSession session)
        {
            Handles.BeginGUI();
            string text;
            if (StageMapController.IsLocked)
                text = "Map Tool: Play/컴파일 중 - 편집 잠금 (표시만)";
            else
            {
                string tool = session.tool == MapPaletteTool.Select ? "선택" : session.tool.ToString();
                text = $"Map Tool · {controller.FileName}{(controller.IsDirty ? " *" : "")} · 격자 {session.grid:0.##} m"
                       + (cursorValid ? $" · 커서 x {cursorWorld.x:0.0} y {cursorWorld.y:0.0}" : "")
                       + $" · 도구: {tool}" + (session.editEnabled ? "" : " · 편집 꺼짐");
            }
            var rect = new Rect(8f, sv.position.height - 54f, Mathf.Min(sv.position.width - 16f, 620f), 22f);
            GUI.Label(rect, text, hudStyle);
            Handles.EndGUI();
        }

        // ------------------------------------------------------------------ input

        private bool SnapOn(Event e) => controller.Session.snap ^ (e.control || e.command);

        private void HandleInput(SceneView sv, Event e)
        {
            var d = controller.Data;
            int id = GUIUtility.GetControlID(controlHint, FocusType.Passive);
            sv.wantsMouseMove = true;

            // Commands that Unity's Edit menu / shortcuts send to the focused scene view (R-01).
            if (e.type == EventType.ValidateCommand || e.type == EventType.ExecuteCommand)
            {
                HandleCommand(e);
                return;
            }

            switch (e.GetTypeForControl(id))
            {
                case EventType.Layout:
                    if (!Tools.viewToolActive)
                        HandleUtility.AddDefaultControl(id);
                    break;

                case EventType.MouseMove:
                    if (TryWorld(e.mousePosition, out var moveWorld))
                    {
                        cursorWorld = moveWorld;
                        cursorValid = true;
                        hover = LayoutGeometry.Pick(d, moveWorld, PickPixels * pxToWorld);
                        sv.Repaint();
                    }
                    break;

                case EventType.MouseDown:
                    if (e.button == 0 && !e.alt && !Tools.viewToolActive && HandleUtility.nearestControl == id)
                        OnMouseDown(d, e, id);
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        OnMouseDrag(d, e);
                        e.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id && e.button == 0)
                    {
                        GUIUtility.hotControl = 0;
                        if (dragStarted)
                            controller.EndDrag();
                        ResetDrag();
                        e.Use();
                    }
                    break;

                case EventType.KeyDown:
                    OnKeyDown(e, id);
                    break;
            }
        }

        private void OnMouseDown(StageLayoutData d, Event e, int id)
        {
            if (!TryWorld(e.mousePosition, out var world))
                return;
            Selection.activeObject = null; // keep Delete / Ctrl+D away from GameObjects (R-01)
            cursorWorld = world;
            cursorValid = true;
            var session = controller.Session;

            if (session.tool != MapPaletteTool.Select)
            {
                controller.Place(session.tool, world, SnapOn(e));
                e.Use();
                return;
            }

            // 1. Handles of the selected element.
            var sel = controller.Selection;
            if (!sel.IsNone && sel.IsValid(d))
            {
                LayoutGeometry.GetHandles(d, sel, handles);
                foreach (var h in handles)
                {
                    if ((h.Position - world).magnitude <= HandlePixels * pxToWorld)
                    {
                        BeginPointerDrag(DragMode.Handle, sel, h.Kind, Vector2.zero, e, id);
                        e.Use();
                        return;
                    }
                }
            }

            // 2. Element under the cursor.
            var picked = LayoutGeometry.Pick(d, world, PickPixels * pxToWorld);
            controller.Select(picked);
            if (!picked.IsNone && picked.kind != LayoutElementKind.Section)
            {
                Vector2 pos = StageLayoutEditOps.GetPosition(d, picked);
                BeginPointerDrag(DragMode.Body, picked, LayoutHandle.None, pos - world, e, id);
            }
            e.Use();
        }

        private void BeginPointerDrag(DragMode mode, LayoutElementRef target, LayoutHandle handle, Vector2 offset, Event e, int id)
        {
            dragMode = mode;
            dragRef = target;
            dragHandle = handle;
            grabOffset = offset;
            downGui = e.mousePosition;
            dragStarted = false;
            GUIUtility.hotControl = id;
        }

        private void OnMouseDrag(StageLayoutData d, Event e)
        {
            if (dragMode == DragMode.None || !TryWorld(e.mousePosition, out var world))
                return;
            cursorWorld = world;
            cursorValid = true;
            if (!dragStarted)
            {
                if ((e.mousePosition - downGui).magnitude < DragStartPixels)
                    return;
                controller.BeginDrag("Map Tool: " + (dragMode == DragMode.Handle ? "Resize " : "Move ") + controller.Describe(dragRef));
                dragStarted = true;
            }
            if (!dragRef.IsValid(controller.Data))
                return;

            bool snapOn = SnapOn(e);
            if (dragMode == DragMode.Body)
            {
                float probeDx = dragRef.kind == LayoutElementKind.Spike ? controller.Data.spikes[dragRef.index].w * 0.5f : 0f;
                Vector2 pos = controller.ResolvePoint(dragRef.kind, world + grabOffset, snapOn, probeDx);
                controller.MoveElement(dragRef, pos);
            }
            else
            {
                float value = LayoutGeometry.HandleValueFromWorld(controller.Data, dragRef, dragHandle, world);
                value = snapOn ? StageLayoutEditOps.Snap(value, controller.Session.grid) : StageLayoutEditOps.Round01(value);
                controller.DragEdge(dragRef, dragHandle, value);
            }
        }

        private void ResetDrag()
        {
            dragMode = DragMode.None;
            dragRef = LayoutElementRef.None;
            dragHandle = LayoutHandle.None;
            dragStarted = false;
        }

        private void OnKeyDown(Event e, int id)
        {
            var session = controller.Session;
            var sel = controller.Selection;
            bool hasSel = !sel.IsNone && sel.IsValid(controller.Data);
            bool mod = e.control || e.command;

            switch (e.keyCode)
            {
                case KeyCode.Escape:
                    if (dragStarted)
                    {
                        controller.CancelDrag();
                        GUIUtility.hotControl = 0;
                        ResetDrag();
                    }
                    else if (session.tool != MapPaletteTool.Select)
                        controller.SetTool(MapPaletteTool.Select);
                    else
                        controller.Select(LayoutElementRef.None);
                    e.Use();
                    return;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    if (hasSel && !mod) { controller.DeleteSelected(); e.Use(); }
                    return;
                case KeyCode.D:
                    if (mod && hasSel) { controller.DuplicateSelected(); e.Use(); }
                    return;
                case KeyCode.F:
                    if (!mod && hasSel) { FrameSelection(); e.Use(); }
                    return;
                case KeyCode.LeftArrow: Nudge(e, hasSel, -1, 0); return;
                case KeyCode.RightArrow: Nudge(e, hasSel, 1, 0); return;
                case KeyCode.UpArrow: Nudge(e, hasSel, 0, 1); return;
                case KeyCode.DownArrow: Nudge(e, hasSel, 0, -1); return;
            }

            if (!mod && !e.alt && TryToolForKey(e.keyCode, out var tool))
            {
                controller.SetTool(tool);
                e.Use();
            }
        }

        private void Nudge(Event e, bool hasSel, int sx, int sy)
        {
            if (!hasSel || dragStarted)
                return;
            int mul = e.shift ? 4 : 1;
            controller.NudgeSelected(sx * mul, sy * mul);
            e.Use();
        }

        // 1..9 pick the first nine palette tools, 0 the select tool (faller / section have no key: conflicts removed).
        private static bool TryToolForKey(KeyCode key, out MapPaletteTool tool)
        {
            tool = MapPaletteTool.Select;
            if (key == KeyCode.Alpha0)
                return true;
            if (key >= KeyCode.Alpha1 && key <= KeyCode.Alpha9)
            {
                tool = (MapPaletteTool)((int)key - (int)KeyCode.Alpha1 + 1);
                return true;
            }
            return false;
        }

        private void HandleCommand(Event e)
        {
            var sel = controller.Selection;
            bool hasSel = !sel.IsNone && sel.IsValid(controller.Data);
            if (!hasSel)
                return;
            string name = e.commandName;
            bool isDelete = name == "Delete" || name == "SoftDelete";
            bool isDuplicate = name == "Duplicate";
            bool isFrame = name == "FrameSelected";
            if (!isDelete && !isDuplicate && !isFrame)
                return;
            if (e.type == EventType.ExecuteCommand)
            {
                if (isDelete) controller.DeleteSelected();
                else if (isDuplicate) controller.DuplicateSelected();
                else FrameSelection();
            }
            e.Use();
        }
    }
}
