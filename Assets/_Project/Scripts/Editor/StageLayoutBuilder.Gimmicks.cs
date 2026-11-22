using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Builder part for schema v1.4: timed spike traps, pressure switches, sliding gates, the boss with its arena gate, and the
    /// theme kit art slots for movers / fallers / static spikes. Empty kit slots keep the primitive look (GreenMeadow unchanged).
    /// </summary>
    public static partial class StageLayoutBuilder
    {
        private const float SpikeTrapUnit = 1.5f;     // SD_P12 width
        private const float TrapRiseHeight = 0.5f;    // primitive pyramid height (0.5) = rise distance
        private const float ThemedTrapRise = 0.58f;   // SD_P12 spikes move +0.58 local Y
        private const float GatePrefabHeight = 3f;    // SD_P13 nominal height
        private const float ArenaGateHeight = 8f;
        private const float MoverNominalWidth = 3f;   // SD_G07_Platform_Thin: 3 x 0.4 x 4
        private const float MoverNominalHeight = 0.4f;
        private const float MoverThickness = 0.5f;
        private const float StaticSpikeUnit = 1f;

        // ------------------------------------------------------------------ shared prefab sources

        private static GameObject MakeSpikeTrap()
        {
            // Units, base slab and the hit box size are per instance (width comes from the layout).
            var root = new GameObject("SpikeTrap");
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.25f, 0f);
            box.size = new Vector3(2f, 0.5f, SpikeDepth);
            root.AddComponent<SpikeTrap>();
            return root;
        }

        private static GameObject MakeFloorSwitch(Material plateMat, Material baseMat)
        {
            var root = new GameObject("FloorSwitch");
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.2f, 0f);
            box.size = new Vector3(1f, 0.4f, 1.5f);
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Prim(PrimitiveType.Cube, "Base", visual, new Vector3(0f, 0.04f, 0f), new Vector3(1.1f, 0.08f, 1.3f), baseMat);
            var plate = Prim(PrimitiveType.Cube, "Plate", visual, new Vector3(0f, 0.12f, 0f), new Vector3(0.8f, 0.08f, 1f), plateMat);
            var comp = root.AddComponent<FloorSwitch>();
            EditorWire.Set(comp, "plate", plate.transform);
            EditorWire.Set(comp, "glow", new List<Renderer> { plate.GetComponent<MeshRenderer>() });
            EditorWire.Set(comp, "tintBase", true);
            return root;
        }

        private static GameObject MakeSlidingGate(Material mat)
        {
            var root = new GameObject("SlidingGate");
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, GatePrefabHeight * 0.5f, 0f);
            box.size = new Vector3(LayoutGate.Width, GatePrefabHeight, BlockDepth);
            root.AddComponent<SlidingGate>();
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            Prim(PrimitiveType.Cube, "Slab", visual, new Vector3(0f, GatePrefabHeight * 0.5f, 0f), new Vector3(LayoutGate.Width, GatePrefabHeight, 3f), mat);
            return root;
        }

        // ------------------------------------------------------------------ spike traps

        private static void BuildSpikeTrap(SceneContext ctx, LayoutSpikeTrap t)
        {
            var go = Spawn(ctx.SpikeTrap, t.id, SectionFor(ctx, t.section, t.id, t.x, t.x + t.w), new Vector3(t.x + t.w * 0.5f, t.y, 0f));
            var comp = go.GetComponent<SpikeTrap>();
            var box = go.GetComponent<BoxCollider>();
            // Forgiving hit box: slightly narrower than the visible trap.
            EditorWire.Set(box, "m_Size", new Vector3(Mathf.Max(0.1f, t.w - 0.1f), 0.5f, SpikeDepth));
            EditorWire.Set(box, "m_Center", new Vector3(0f, 0.25f, 0f));
            EditorWire.Set(comp, "period", t.period);
            EditorWire.Set(comp, "upTime", t.upTime);
            EditorWire.Set(comp, "warn", t.warn);
            EditorWire.Set(comp, "offset", t.offset);

            var risers = new List<Transform>();
            var kit = ctx.Theme;
            if (kit != null && kit.spikeTrapVisual != null)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(t.w / SpikeTrapUnit));
                float unitW = t.w / n;
                for (int i = 0; i < n; i++)
                {
                    var art = AddArt(kit.spikeTrapVisual, go.transform, $"Trap_{i + 1:00}",
                        new Vector3(-t.w * 0.5f + unitW * (i + 0.5f), 0f, 0f), new Vector3(unitW / SpikeTrapUnit, 1f, 1f));
                    foreach (var child in art.GetComponentsInChildren<Transform>(true))
                    {
                        if (child.name.StartsWith("Spike_", System.StringComparison.Ordinal))
                            risers.Add(child);
                    }
                }
                EditorWire.Set(comp, "riseHeight", ThemedTrapRise);
            }
            else
            {
                // Primitive: a dark base strip flush with the floor and pyramids buried 0.5 m that rise out of it.
                var baseMat = SceneFlowSetup.LitMaterial("M_SpikeTrapBase", new Color(0.25f, 0.2f, 0.2f));
                Prim(PrimitiveType.Cube, "Base", go.transform, new Vector3(0f, -0.04f, 0f), new Vector3(t.w, 0.08f, SpikeDepth), baseMat);
                int count = Mathf.Max(1, Mathf.RoundToInt(t.w / SpikeUnitWidth));
                float unitW = t.w / count;
                for (int i = 0; i < count; i++)
                {
                    var unit = new GameObject($"Spike_{i + 1:00}");
                    unit.transform.SetParent(go.transform, false);
                    unit.transform.localPosition = new Vector3(-t.w * 0.5f + unitW * (i + 0.5f), -TrapRiseHeight, 0f);
                    unit.transform.localScale = new Vector3(unitW, 1f, SpikeDepth);
                    unit.AddComponent<MeshFilter>().sharedMesh = ctx.SpikeMesh;
                    unit.AddComponent<MeshRenderer>().sharedMaterial = ctx.SpikeMat;
                    risers.Add(unit.transform);
                }
                EditorWire.Set(comp, "riseHeight", TrapRiseHeight);
            }
            EditorWire.Set(comp, "risers", risers);
        }

        // ------------------------------------------------------------------ switches / gates

        private static void BuildSwitch(SceneContext ctx, LayoutSwitch s)
        {
            var go = Spawn(ctx.FloorSwitch, s.id, SectionFor(ctx, s.section, s.id, s.x, s.x), new Vector3(s.x, s.y, 0f));
            var comp = go.GetComponent<FloorSwitch>();
            EditorWire.Set(comp, "channel", s.channel);
            EditorWire.Set(comp, "once", s.once);
            var kit = ctx.Theme;
            if (kit == null || kit.switchVisual == null)
                return;
            go.transform.Find("Visual").gameObject.SetActive(false);
            var art = AddArt(kit.switchVisual, go.transform, "SwitchArt", Vector3.zero, Vector3.one);
            var plate = FindChild(art.transform, "Plate");
            var emblem = FindChild(art.transform, "Emblem");
            var glow = new List<Renderer>();
            if (emblem != null)
                glow.AddRange(emblem.GetComponentsInChildren<Renderer>(true));
            else if (plate != null)
                glow.AddRange(plate.GetComponentsInChildren<Renderer>(true));
            EditorWire.Set(comp, "plate", plate);
            EditorWire.Set(comp, "glow", glow);
            EditorWire.Set(comp, "tintBase", false);
        }

        private static void BuildGate(SceneContext ctx, LayoutGate g)
        {
            var go = Spawn(ctx.Gate, g.id, SectionFor(ctx, g.section, g.id, g.x, g.x), new Vector3(g.x, g.y, 0f));
            ConfigureGate(ctx, go, g.h, g.channel, g.openSeconds, false);
        }

        private static void ConfigureGate(SceneContext ctx, GameObject go, float h, int channel, float openSeconds, bool invert, bool arena = false)
        {
            var comp = go.GetComponent<SlidingGate>();
            EditorWire.Set(comp, "channel", channel);
            EditorWire.Set(comp, "openSeconds", openSeconds);
            EditorWire.Set(comp, "invert", invert);
            var box = go.GetComponent<BoxCollider>();
            EditorWire.Set(box, "m_Size", new Vector3(LayoutGate.Width, h, BlockDepth));
            EditorWire.Set(box, "m_Center", new Vector3(0f, h * 0.5f, 0f));

            var visual = go.transform.Find("Visual");
            var kit = ctx.Theme;
            if (arena && kit != null && kit.arenaGateVisual != null)
            {
                // Boss entrance gate: art authored at the full arena gate height (no vertical stretch).
                visual.gameObject.SetActive(false);
                AddArt(kit.arenaGateVisual, go.transform, "GateArt", Vector3.zero, new Vector3(1f, h / ArenaGateHeight, 1f));
            }
            else if (kit != null && kit.gateVisual != null)
            {
                visual.gameObject.SetActive(false);
                AddArt(kit.gateVisual, go.transform, "GateArt", Vector3.zero, new Vector3(1f, h / GatePrefabHeight, 1f));
            }
            else
            {
                visual.localScale = new Vector3(1f, h / GatePrefabHeight, 1f); // slab: 1 x 3 x 3 scaled in height
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                    return t;
            }
            return null;
        }

        // ------------------------------------------------------------------ boss

        /// <summary>Creates the boss data / hazard prefabs / placeholder when missing. Must run before the scene is built.</summary>
        private static void PrepareBoss(StageLayoutData d)
        {
            if (d == null || !d.HasBoss)
                return;
            BossSetup.EnsureAssets();
            var data = BossCatalog.Load(d.boss.bossId);
            if (data != null)
                BossSetup.PrefabFor(data);
        }

        private static void BuildBoss(SceneContext ctx, StageLayoutData d)
        {
            var b = d.boss;
            var data = BossCatalog.Load(b.bossId);
            if (data == null)
                throw new System.InvalidOperationException($"BossData {b.bossId} missing at {BossCatalog.PathFor(b.bossId)}.");
            var prefab = BossSetup.PrefabFor(data);
            var go = Spawn(prefab, $"Boss_{b.bossId}", SectionFor(ctx, null, null, b.x, b.x), new Vector3(b.x, b.y, 0f));
            var controller = go.GetComponent<BossController>();
            if (controller == null)
                throw new System.InvalidOperationException($"Boss prefab {AssetDatabase.GetAssetPath(prefab)} has no BossController.");
            EditorWire.Set(controller, "data", data);
            EditorWire.Set(controller, "arenaMinX", b.arenaMinX);
            EditorWire.Set(controller, "arenaMaxX", b.arenaMaxX);

            // Entrance gate: open until the fight starts, closed during it (inverted gate on the boss channel).
            var gate = Spawn(ctx.Gate, $"BossGate_{b.bossId}", SectionFor(ctx, null, null, b.arenaMinX, b.arenaMinX), new Vector3(b.arenaMinX, b.y, 0f));
            ConfigureGate(ctx, gate, ArenaGateHeight, BossController.ArenaChannel, 0.6f, true, arena: true);
        }

        // ------------------------------------------------------------------ theme art for movers / fallers / static spikes

        /// <summary>Mover platform: the thin platform art is tiled over the width; the cube collider and its size stay.</summary>
        private static void ApplyMoverArt(SceneContext ctx, GameObject mover, float width)
        {
            var kit = ctx.Theme;
            if (kit == null || kit.moverVisual == null)
                return;
            var root = NewArtRoot(mover.transform, "Art");
            int n = Mathf.Max(1, Mathf.RoundToInt(width / MoverNominalWidth));
            float step = width / n;
            for (int i = 0; i < n; i++)
            {
                // Pivot top-left: the top surface is at +0.25 local (cube centre = y - 0.25).
                var art = AddArt(kit.moverVisual, root, $"Platform_{i:00}", new Vector3(-width * 0.5f + i * step, MoverThickness * 0.5f, 0f),
                    new Vector3(step / MoverNominalWidth, MoverThickness / MoverNominalHeight, 1f));
                ClearStaticFlags(art);
            }
            HideRenderer(mover.transform);
        }

        /// <summary>Falling hazard: the breakaway platform art replaces the two spiked cubes (the trigger box stays).</summary>
        private static void ApplyFallerArt(SceneContext ctx, GameObject faller)
        {
            var kit = ctx.Theme;
            if (kit == null || kit.fallerVisual == null)
                return;
            var visual = faller.transform.Find("Visual");
            if (visual == null)
                return;
            HideRenderer(visual.Find("BlockA"));
            HideRenderer(visual.Find("BlockB"));
            // Pivot top-left, 3 x 0.4 x 4: scaled to about the 1.4 m trigger box and centred on it.
            ClearStaticFlags(AddArt(kit.fallerVisual, visual, "Art", new Vector3(-0.75f, 0.5f, 0f), new Vector3(0.5f, 2.5f, 0.4f)));
        }

        /// <summary>
        /// Art under a moving object must not be static: a static-batched mesh stays where it was baked while the
        /// collider moves on (the platform looks frozen and the player falls through the visible one).
        /// </summary>
        private static void ClearStaticFlags(GameObject art)
        {
            foreach (var t in art.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        /// <summary>Static spikes with kit art: the red pyramids are hidden and art units (pivot bottom centre, 1 m) are tiled.</summary>
        private static void ApplyStaticSpikeArt(SceneContext ctx, GameObject spikes, float width)
        {
            var kit = ctx.Theme;
            if (kit == null || kit.spikeVisual == null)
                return;
            foreach (var r in spikes.GetComponentsInChildren<MeshRenderer>(true))
                r.enabled = false;
            int n = Mathf.Max(1, Mathf.RoundToInt(width / StaticSpikeUnit));
            float unitW = width / n;
            for (int i = 0; i < n; i++)
                AddArt(kit.spikeVisual, spikes.transform, $"SpikeArt_{i + 1:00}", new Vector3(-width * 0.5f + unitW * (i + 0.5f), 0f, 0f), new Vector3(unitW / StaticSpikeUnit, 1f, 1f));
        }
    }
}
