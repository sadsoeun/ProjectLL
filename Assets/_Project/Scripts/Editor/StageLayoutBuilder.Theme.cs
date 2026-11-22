using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Art for the stage builder (contract v1.3): theme kit visuals on blocks, checkpoints, goal, coins and gems, and the
    /// decoration props. Everything here only ADDS child objects and hides primitive MeshRenderers; colliders, triggers,
    /// scripts and block top heights are never changed. An empty kit slot keeps the primitive visual.
    /// Art is instantiated with PrefabUtility.InstantiatePrefab (prefab link kept) and its colliders are disabled.
    /// </summary>
    public static partial class StageLayoutBuilder
    {
        private const float ModuleSize = 2f;             // T01..T04: 2 x 2 x 4
        private const float FloatingNominalWidth = 3f;   // T06: 3 x 1 x 4
        private const float FloatingNominalHeight = 1f;
        private const float TimberNominalWidth = 3f;     // T07: 3 x 0.4 x 4
        private const float TimberNominalHeight = 0.4f;
        private const float TimberMaxThickness = 0.5f;  // platform blocks up to this thick use the timber platform
        private const float EdgeTolerance = 0.05f;
        private const float StartApronLength = 8f;       // visual ground left of the course start (camera room at the spawn)
        private const float BackFillEndLength = 14f;     // back fill continues this far past the last ground block
        private const float PlatformSupportSpacing = 6f; // one pier per this many metres of platform width
        private const float PlatformSupportMinGap = 0.3f; // a platform closer than this to the block below needs no pier
        private const float GoalCenterAboveSurface = 1.5f; // the Goal root is placed at goal.y + 1.5 (see BuildScene)

        private static StageThemeKit LoadTheme(string theme)
        {
            if (string.IsNullOrEmpty(theme) || !StageThemeKit.IsValidThemeName(theme))
                return null;
            string path = StageThemeKit.PathFor(theme);
            var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(path);
            if (kit == null)
                Debug.LogWarning($"[StageLayoutBuilder] Theme kit not found: {path}. Primitive visuals are used.");
            return kit;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Empty child that cancels the parent's (possibly non-uniform) scale, so art placed below it is in plain metres and
        /// at scale 1: a child at local position p sits p metres from the parent's centre.
        /// </summary>
        private static Transform NewArtRoot(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            Vector3 s = parent.lossyScale;
            t.localScale = new Vector3(Inverse(s.x), Inverse(s.y), Inverse(s.z));
            return t;
        }

        private static float Inverse(float v) => Mathf.Approximately(v, 0f) ? 1f : 1f / v;

        private static GameObject AddArt(GameObject prefab, Transform parent, string name, Vector3 localPosition, Vector3 localScale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name;
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;
            DisableColliders(go);
            return go;
        }

        /// <summary>
        /// Core terrain module (T01 / T04), optionally mirrored left-right. Core modules tile seamlessly on both sides, so
        /// a mirrored module meets any neighbour on the same edge profile; mixing both orientations breaks the visible
        /// 2 m repetition of the rock pattern without extra art. The pivot stays top-left: a mirrored module is shifted
        /// right by its width.
        /// </summary>
        private static GameObject AddModule(GameObject prefab, Transform parent, string name, Vector3 localPosition, Vector3 localScale,
            float width, bool mirror)
        {
            if (mirror)
            {
                localPosition.x += width;
                localScale.x = -localScale.x;
            }
            return AddArt(prefab, parent, name, localPosition, localScale);
        }

        /// <summary>Deterministic hash of a module's world top-left corner (same layout = same art on every rebuild).</summary>
        private static uint CellHash(float left, float top)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(left * 2f) * 73856093u ^ (uint)Mathf.RoundToInt(top * 2f) * 19349663u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return h;
            }
        }

        private static bool MirrorCell(float left, float top) => (CellHash(left, top) & 1u) != 0;

        /// <summary>
        /// The core module or one of its variants (kit arrays; all share the core's seam profiles), chosen per cell.
        /// Null entries are skipped; no variants = the core itself.
        /// </summary>
        private static GameObject PickCore(GameObject core, GameObject[] variants, float left, float top)
        {
            int count = 1 + (variants?.Length ?? 0);
            int pick = (int)((CellHash(left, top) >> 8) % (uint)count);
            return pick == 0 || variants[pick - 1] == null ? core : variants[pick - 1];
        }

        private static void DisableColliders(GameObject root)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
        }

        private static void HideRenderer(Transform t)
        {
            if (t == null)
                return;
            var r = t.GetComponent<MeshRenderer>();
            if (r != null)
                r.enabled = false;
        }

        // ------------------------------------------------------------------ blocks

        /// <summary>
        /// Replaces the look of a block with kit modules. The block keeps its Cube, BoxCollider and size; only its
        /// MeshRenderer is hidden. Modules have their pivot at the top-left corner, depth-centred, so a module at world
        /// (left, top) gets the offset (left - cubeCentre.x, top - cubeCentre.y) from the Cube centre (x + w/2, y - h/2, 0).
        /// </summary>
        /// <summary>Theme atmosphere: a StageAtmosphere (applied at runtime, since stages load additively under 06_InGame) plus
        /// the same fog in this scene's RenderSettings so the editor / map tool preview matches the game.</summary>
        private static void ApplyAtmosphere(StageThemeKit kit, Transform lighting, Light sun, float length)
        {
            if (kit == null || !kit.useAtmosphere)
                return;
            var a = lighting.gameObject.AddComponent<StageAtmosphere>();
            a.skyColor = kit.skyColor;
            a.fog = true;
            a.fogColor = kit.fogColor;
            a.fogStart = kit.fogStart;
            a.fogEnd = kit.fogEnd;
            a.sun = sun;
            a.sunColor = kit.sunColor;
            a.sunIntensity = kit.sunIntensity;
            sun.color = kit.sunColor;
            sun.intensity = kit.sunIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = kit.fogColor;
            RenderSettings.fogStartDistance = kit.fogStart;
            RenderSettings.fogEndDistance = kit.fogEnd;
            if (kit.skyMaterial != null)
            {
                // Inside the gameplay camera's 200 m far plane (camera at z -16); wide enough for the whole course.
                var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
                sky.name = "Sky";
                UnityEngine.Object.DestroyImmediate(sky.GetComponent<Collider>());
                sky.transform.SetParent(lighting, false);
                sky.transform.position = new Vector3(length * 0.5f, 30f, SkyDepth);
                sky.transform.localScale = new Vector3(length + 420f, 150f, 1f);
                var r = sky.GetComponent<MeshRenderer>();
                r.sharedMaterial = kit.skyMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }
        }

        private const float SkyDepth = 175f;

        /// <summary>Theme screen post-processing (for example the frost blizzard): added to the Lighting object so it lives
        /// and dies with the additive stage scene.</summary>
        private static void ApplyScreenWeather(StageThemeKit kit, Transform lighting)
        {
            if (kit == null)
                return;
            if (kit.ambientSeagulls > 0)
                lighting.gameObject.AddComponent<AmbientSeagulls>().count = kit.ambientSeagulls;
            if (kit.screenFxProfile == null)
                return;
            var w = lighting.gameObject.AddComponent<StageScreenWeather>();
            w.profile = kit.screenFxProfile;
        }

        private static void ApplyBlockArt(SceneContext ctx, GameObject cube, LayoutBlock b, LayoutBlock[] all)
        {
            var kit = ctx.Theme;
            if (kit == null)
                return;
            float centerX = b.x + b.w * 0.5f;
            float centerY = b.y - b.h * 0.5f;

            if (b.kind == "wall" && kit.arenaWallVisual != null)
            {
                // Authored for the 2 x 20 x 4 boss arena end wall; other sizes are stretched to fit.
                var wallRoot = NewArtRoot(cube.transform, "Art");
                AddArt(kit.arenaWallVisual, wallRoot, "ArenaWall", Vector3.zero, new Vector3(b.w / 2f, b.h / 20f, 1f));
                HideRenderer(cube.transform);
                return;
            }

            if (b.kind == "platform")
            {
                bool timber = b.h <= TimberMaxThickness;
                var prefab = timber ? kit.timberPlatform : kit.floatingPlatform;
                if (prefab == null)
                    return;
                float nominalW = timber ? TimberNominalWidth : FloatingNominalWidth;
                float nominalH = timber ? TimberNominalHeight : FloatingNominalHeight;
                int n = Mathf.Max(1, Mathf.RoundToInt(b.w / nominalW));
                float step = b.w / n;
                var root = NewArtRoot(cube.transform, "Art");
                for (int i = 0; i < n; i++)
                {
                    AddArt(prefab, root, $"{(timber ? "Timber" : "Floating")}_{i:00}",
                        new Vector3(b.x + i * step - centerX, b.y - centerY, 0f), new Vector3(step / nominalW, b.h / nominalH, 1f));
                }
                HideRenderer(cube.transform);
                return;
            }

            // ground / wall: a cap row (grass) on top, rock rows below down to h.
            if (kit.groundCore == null)
                return;
            float capHeight = Mathf.Min(ModuleSize, b.h);
            float rest = b.h - capHeight;
            int rockRows = rest > EdgeTolerance ? Mathf.Max(1, Mathf.RoundToInt(rest / ModuleSize)) : 0;
            if (rockRows > 0 && kit.rockCore == null)
                return; // a partial look would be worse than the primitive

            int columns = Mathf.Max(1, Mathf.RoundToInt(b.w / ModuleSize));
            float colWidth = b.w / columns;
            float capMidY = b.y - capHeight * 0.5f;
            bool continuesIntoApron = b.kind == "ground" && Mathf.Abs(b.x) <= EdgeTolerance; // see BuildStartApron
            bool airLeft = !continuesIntoApron && BordersAir(b, true, capMidY, all);
            bool airRight = BordersAir(b, false, capMidY, all);

            var art = NewArtRoot(cube.transform, "Art");
            for (int i = 0; i < columns; i++)
            {
                float cellLeft = b.x + i * colWidth;
                GameObject prefab;
                bool core = false; // only core modules are varied and mirrored; end modules have one open side
                if (columns > 1 && i == 0 && airLeft && kit.groundEndLeft != null)
                    prefab = kit.groundEndLeft;
                else if (columns > 1 && i == columns - 1 && airRight && kit.groundEndRight != null)
                    prefab = kit.groundEndRight;
                else
                {
                    prefab = b.surface == "ice" && kit.iceSurface != null
                        ? kit.iceSurface
                        : PickCore(kit.groundCore, kit.groundCoreVariants, cellLeft, b.y);
                    core = true;
                }
                AddModule(prefab, art, $"Cap_{i:00}", new Vector3(cellLeft - centerX, b.y - centerY, 0f),
                    new Vector3(colWidth / ModuleSize, capHeight / ModuleSize, 1f), colWidth, core && MirrorCell(cellLeft, b.y));
            }
            if (rockRows > 0)
            {
                float rowHeight = rest / rockRows;
                for (int row = 0; row < rockRows; row++)
                {
                    float top = b.y - capHeight - row * rowHeight;
                    for (int i = 0; i < columns; i++)
                    {
                        float cellLeft = b.x + i * colWidth;
                        AddModule(PickCore(kit.rockCore, kit.rockCoreVariants, cellLeft, top), art, $"Rock_{row:00}_{i:00}",
                            new Vector3(cellLeft - centerX, top - centerY, 0f),
                            new Vector3(colWidth / ModuleSize, rowHeight / ModuleSize, 1f), colWidth, MirrorCell(cellLeft, top));
                    }
                }
            }
            HideRenderer(cube.transform);
        }

        /// <summary>
        /// Visual-only ground continuing the first ground block to the left of the course start (x &lt; 0). The camera may
        /// show it, so at the spawn the player stands about a third into the screen instead of at its left edge.
        /// The left boundary wall stays at the course start. Returns false when there is no ground block at x = 0.
        /// </summary>
        private static bool BuildStartApron(SceneContext ctx, LayoutBlock[] blocks)
        {
            var kit = ctx.Theme;
            LayoutBlock first = null;
            foreach (var b in blocks)
                if (b.kind == "ground" && Mathf.Abs(b.x) <= EdgeTolerance)
                    first = b;
            if (first == null)
                return false;

            var root = new GameObject("StartApron").transform;
            if (kit == null || kit.groundCore == null)
            {
                SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "ApronGround", root,
                    new Vector3(-StartApronLength * 0.5f, first.y - first.h * 0.5f, 0f), new Vector3(StartApronLength, first.h, BlockDepth), ctx.Ground, false);
                return true;
            }

            float capHeight = Mathf.Min(ModuleSize, first.h);
            float rest = first.h - capHeight;
            int rockRows = rest > EdgeTolerance && kit.rockCore != null ? Mathf.Max(1, Mathf.RoundToInt(rest / ModuleSize)) : 0;
            int columns = Mathf.RoundToInt(StartApronLength / ModuleSize);
            for (int i = 0; i < columns; i++)
            {
                float x = -StartApronLength + i * ModuleSize;
                AddModule(PickCore(kit.groundCore, kit.groundCoreVariants, x, first.y), root, $"Cap_{i:00}", new Vector3(x, first.y, 0f),
                    new Vector3(1f, capHeight / ModuleSize, 1f), ModuleSize, MirrorCell(x, first.y));
                for (int row = 0; row < rockRows; row++)
                {
                    float rowHeight = rest / rockRows;
                    float top = first.y - capHeight - row * rowHeight;
                    AddModule(PickCore(kit.rockCore, kit.rockCoreVariants, x, top), root, $"Rock_{row:00}_{i:00}", new Vector3(x, top, 0f),
                        new Vector3(1f, rowHeight / ModuleSize, 1f), ModuleSize, MirrorCell(x, top));
                }
            }
            return true;
        }

        /// <summary>
        /// Visual-only ground behind every ground block (kit.backFillDepth metres past the lane's back edge), at the
        /// block's own height: the cap modules stretched in depth, plus a plain body below them. Mid-ground decorations
        /// (windmill, trees) then stand on ground instead of hanging in the air behind the lane. Pits stay open.
        /// </summary>
        private static void BuildBackFill(SceneContext ctx, LayoutBlock[] blocks, bool apron)
        {
            var kit = ctx.Theme;
            if (kit == null || kit.backFillDepth <= 0f || kit.groundCore == null)
                return;
            float depth = kit.backFillDepth;
            float zCenter = BlockDepth * 0.5f + depth * 0.5f;
            float zScale = depth / BlockDepth;
            var root = new GameObject("BackFill").transform;
            LayoutBlock last = null; // the ground block that ends the course: its fill runs on past the end (camera room)
            foreach (var b in blocks)
                if (b.kind == "ground" && (last == null || b.x + b.w > last.x + last.w))
                    last = b;
            foreach (var b in blocks)
            {
                if (b.kind != "ground")
                    continue;
                bool raisedOnly = kit.backFillRaisedOnly; // the theme's floor prop already covers y = 0
                if (raisedOnly && b.y <= 0.05f)
                    continue;
                bool first = Mathf.Abs(b.x) <= EdgeTolerance;
                float left = b.x, width = b.w;
                if (apron && first)
                {
                    left -= StartApronLength;
                    width += StartApronLength;
                }
                if (b == last)
                    width += BackFillEndLength;
                float fillHeight = raisedOnly ? Mathf.Min(b.h, b.y) : b.h;
                float capHeight = Mathf.Min(ModuleSize, fillHeight);
                float capMidY = b.y - capHeight * 0.5f;
                int columns = Mathf.Max(1, Mathf.RoundToInt(width / ModuleSize));
                float colWidth = width / columns;
                bool airLeft = !first && BordersAir(b, true, capMidY, blocks);
                bool airRight = b != last && BordersAir(b, false, capMidY, blocks);

                var fill = new GameObject("Fill_" + b.id).transform;
                fill.SetParent(root, false);
                for (int i = 0; i < columns; i++)
                {
                    float cellLeft = left + i * colWidth;
                    GameObject prefab;
                    bool core = false;
                    if (columns > 1 && i == 0 && airLeft && kit.groundEndLeft != null)
                        prefab = kit.groundEndLeft;
                    else if (columns > 1 && i == columns - 1 && airRight && kit.groundEndRight != null)
                        prefab = kit.groundEndRight;
                    else
                    {
                        prefab = PickCore(kit.groundCore, kit.groundCoreVariants, cellLeft, b.y + 1f); // +1: not the lane's pick
                        core = true;
                    }
                    AddModule(prefab, fill, $"Cap_{i:00}", new Vector3(cellLeft, b.y, zCenter),
                        new Vector3(colWidth / ModuleSize, capHeight / ModuleSize, zScale), colWidth, core && MirrorCell(cellLeft, b.y + 1f));
                }
                float rest = fillHeight - capHeight;
                // Pit-side faces: the stretched cap / plain body would show a smeared texture there, so each end that
                // borders air gets unstretched modules tiled in depth (cap row + rock rows).
                if (kit.rockCore != null && width >= ModuleSize * 2f)
                {
                    int depthTiles = Mathf.Max(1, Mathf.RoundToInt(depth / BlockDepth));
                    float tileDepth = depth / depthTiles;
                    float tileScaleZ = tileDepth / BlockDepth;
                    float bodyShown = Mathf.Min(rest, 8f * ModuleSize);
                    int rows = bodyShown > EdgeTolerance ? Mathf.Max(1, Mathf.RoundToInt(bodyShown / ModuleSize)) : 0;
                    float rowH = rows > 0 ? bodyShown / rows : 0f;
                    for (int side = 0; side < 2; side++)
                    {
                        if (!(side == 0 ? airLeft : airRight))
                            continue;
                        float cellLeft = side == 0 ? left - 0.02f : left + width - ModuleSize + 0.02f;
                        var capPrefab = side == 0 ? (kit.groundEndLeft != null ? kit.groundEndLeft : kit.groundCore)
                                                  : (kit.groundEndRight != null ? kit.groundEndRight : kit.groundCore);
                        for (int k = 0; k < depthTiles; k++)
                        {
                            float z = BlockDepth * 0.5f + (k + 0.5f) * tileDepth;
                            AddModule(capPrefab, fill, $"EndCap_{side}_{k}", new Vector3(cellLeft, b.y + 0.01f, z),
                                new Vector3(1f, capHeight / ModuleSize, tileScaleZ), ModuleSize, false);
                            for (int r = 0; r < rows; r++)
                                AddModule(PickCore(kit.rockCore, kit.rockCoreVariants, cellLeft + k, b.y - r), fill, $"EndRock_{side}_{k}_{r}",
                                    new Vector3(cellLeft, b.y - capHeight - r * rowH, z),
                                    new Vector3(1f, rowH / ModuleSize, tileScaleZ), ModuleSize, false);
                        }
                    }
                }
                var bodyMaterial = kit.backFillBody;
                if (bodyMaterial == null)
                {
                    var coreRenderer = kit.groundCore.GetComponentInChildren<Renderer>();
                    bodyMaterial = coreRenderer != null ? coreRenderer.sharedMaterial : null;
                }
                if (rest > EdgeTolerance && bodyMaterial != null)
                {
                    SceneFlowSetup.CreatePrimitive(PrimitiveType.Cube, "Body", fill,
                        new Vector3(left + width * 0.5f, b.y - capHeight - rest * 0.5f, zCenter),
                        new Vector3(Mathf.Max(0.1f, width - 0.6f), rest, depth - 0.1f), bodyMaterial, false);
                }
            }
            BuildBackFillBand(kit, blocks, root, depth);
        }

        private const float BackFillBandMaxGap = 8f;

        /// <summary>
        /// Far band: behind a pit the back fill stops, so scenery standing further back (forest masses, trees) would hang
        /// over the drop. The far part of the fill (kit.backFillFarBand metres) therefore runs on across every gap between
        /// ground blocks: the pit stays open next to the lane, the land closes behind it.
        /// </summary>
        private static void BuildBackFillBand(StageThemeKit kit, LayoutBlock[] blocks, Transform root, float depth)
        {
            float band = Mathf.Min(kit.backFillFarBand, depth);
            if (band <= 0.5f || kit.backFillRaisedOnly)
                return;
            var ground = new List<LayoutBlock>();
            foreach (var b in blocks)
                if (b.kind == "ground")
                    ground.Add(b);
            ground.Sort((a, b) => a.x.CompareTo(b.x));
            float zCenter = BlockDepth * 0.5f + depth - band * 0.5f;
            float zScale = band / BlockDepth;
            LayoutBlock reach = null; // the block that reaches furthest right so far
            int index = 0;
            foreach (var b in ground)
            {
                // only pits: a wide water crossing keeps its open view (a wall there would hide the backdrop)
                if (reach != null && b.x > reach.x + reach.w + EdgeTolerance && b.x - (reach.x + reach.w) <= BackFillBandMaxGap)
                {
                    float left = reach.x + reach.w, width = b.x - left;
                    float top = Mathf.Min(reach.y, b.y);
                    float bottom = Mathf.Min(reach.y - reach.h, b.y - b.h);
                    float capHeight = Mathf.Min(ModuleSize, top - bottom);
                    var fill = new GameObject("Band_" + index++).transform;
                    fill.SetParent(root, false);
                    int columns = Mathf.Max(1, Mathf.RoundToInt(width / ModuleSize));
                    float colWidth = width / columns;
                    float rest = top - bottom - capHeight;
                    int rows = rest > EdgeTolerance && kit.rockCore != null ? Mathf.Max(1, Mathf.RoundToInt(rest / ModuleSize)) : 0;
                    float rowH = rows > 0 ? rest / rows : 0f;
                    for (int i = 0; i < columns; i++)
                    {
                        float cellLeft = left + i * colWidth;
                        AddModule(PickCore(kit.groundCore, kit.groundCoreVariants, cellLeft, top + 2f), fill, $"Cap_{i:00}",
                            new Vector3(cellLeft, top, zCenter), new Vector3(colWidth / ModuleSize, capHeight / ModuleSize, zScale), colWidth, false);
                        for (int r = 0; r < rows; r++)
                            AddModule(PickCore(kit.rockCore, kit.rockCoreVariants, cellLeft, top - r), fill, $"Rock_{i:00}_{r}",
                                new Vector3(cellLeft, top - capHeight - r * rowH, zCenter),
                                new Vector3(colWidth / ModuleSize, rowH / ModuleSize, zScale), colWidth, false);
                    }
                }
                if (reach == null || b.x + b.w > reach.x + reach.w)
                    reach = b;
            }
        }

        /// <summary>
        /// Scatters the kit dressing props (deterministic per position, so a rebuild gives the same stage): small props on
        /// the front and back lips of every ground block, small / medium props on the ground behind the lane, and a row
        /// of large props near the far edge of the back ground. Spots near a layout decoration, a checkpoint or the goal
        /// are left alone; nothing is placed on the running line of the player.
        /// </summary>
        private static void BuildAutoDressing(SceneContext ctx, StageLayoutData d, bool apron)
        {
            var kit = ctx.Theme;
            if (kit == null || kit.dressDepth <= 0f)
                return;
            var small = NonNull(kit.dressSmall);
            var medium = NonNull(kit.dressMedium);
            var large = NonNull(kit.dressLarge);
            if (small.Count == 0 && medium.Count == 0)
                return;
            var blocks = Arr(d.blocks);
            var taken = new List<Vector2>(); // (x, z) of things the dressing must keep clear of
            foreach (var dec in Arr(d.decorations))
                taken.Add(new Vector2(dec.x, dec.z <= LayoutDecoration.ZUnset + 1f ? 0f : dec.z));
            foreach (var c in Arr(d.checkpoints))
                taken.Add(new Vector2(c.x, 0f));
            if (d.goal != null)
                taken.Add(new Vector2(d.goal.x, 0f));
            Transform root = null;
            int placed = 0;

            foreach (var b in blocks)
            {
                if (b.kind != "ground" || b.w < 3f)
                    continue;
                bool first = Mathf.Abs(b.x) <= EdgeTolerance;
                float left = b.x + 0.7f, right = b.x + b.w - 0.7f;
                if (apron && first)
                    left -= StartApronLength - 1f;

                // ground behind the lane: on the back fill, or on the floor prop of the theme for blocks level with it
                bool filled = kit.backFillDepth > 0f && !(kit.backFillRaisedOnly && b.y <= 0.05f);
                bool onFloor = !filled && kit.backFillRaisedOnly && Mathf.Abs(b.y) <= 0.05f;
                float depth = filled ? Mathf.Min(kit.dressDepth, kit.backFillDepth) : onFloor ? kit.dressDepth : 0f;
                float zNear = BlockDepth * 0.5f + 0.9f, zFar = BlockDepth * 0.5f + depth - 0.9f;

                if (depth > 2.5f)
                {
                    const float cell = 2.4f;
                    for (float cx = left; cx < right; cx += cell)
                        for (float cz = zNear; cz < zFar; cz += cell)
                        {
                            uint h = CellHash(cx * 3.1f + cz * 17f, b.y * 5f + cz * 3.7f);
                            if ((h & 0xffff) / 65535f > kit.dressMidDensity * 0.75f)
                                continue;
                            float x = cx + ((h >> 16) & 0xff) / 255f * (cell - 0.5f);
                            float z = cz + ((h >> 24) & 0xff) / 255f * (cell - 0.5f);
                            if (x > right || z > zFar)
                                continue;
                            bool big = medium.Count > 0 && (small.Count == 0 || ((h >> 5) & 7u) < 3u);
                            var list = big ? medium : small;
                            if (!Clear(taken, x, z, big ? 2.2f : 1.1f))
                                continue;
                            Dress(ref root, list[(int)((h >> 9) % (uint)list.Count)], x, b.y, z, h, big ? 0.8f : 0.9f, big ? 1.15f : 1.35f);
                            taken.Add(new Vector2(x, z));
                            placed++;
                        }
                    // far row: hides the straight far edge of the back ground
                    var rowList = large.Count > 0 && depth >= 6f ? large : medium;
                    if (filled && rowList.Count > 0 && depth >= 4f)
                    {
                        for (float cx = left + 1f; cx < right - 1f; cx += 4.6f)
                        {
                            uint h = CellHash(cx * 7.3f, b.y * 11f + 91f);
                            if ((h & 0xff) / 255f > 0.2f + kit.dressMidDensity * 0.4f) // gaps on purpose: the distant backdrop must stay visible
                                continue;
                            float x = cx + ((h >> 8) & 0xff) / 255f * 2.4f;
                            float z = BlockDepth * 0.5f + depth - 1.0f - ((h >> 16) & 0xff) / 255f * 1.2f;
                            if (x > right - 0.5f || !Clear(taken, x, z, 2.6f))
                                continue;
                            Dress(ref root, rowList[(int)((h >> 20) % (uint)rowList.Count)], x, b.y, z, h, 0.9f, 1.25f);
                            taken.Add(new Vector2(x, z));
                            placed++;
                        }
                    }
                }

                // lips of the lane: the strip right at the front edge (toward the camera) and at the back edge
                // (not on a belt, spring pad or crumbling block: the props would sit on the moving / breaking surface)
                bool gimmickTop = b.surface == "conveyor_r" || b.surface == "conveyor_l" || b.surface == "bounce" || b.surface == "crumble";
                if (small.Count > 0 && kit.dressLipDensity > 0f && !gimmickTop)
                {
                    for (float cx = left; cx < right; cx += 1.3f)
                        for (int lip = 0; lip < 2; lip++)
                        {
                            uint h = CellHash(cx * 5.7f + lip * 131f, b.y * 3f + 17f);
                            if ((h & 0xffff) / 65535f > kit.dressLipDensity * (lip == 0 ? 0.45f : 0.6f))
                                continue;
                            float x = cx + ((h >> 16) & 0xff) / 255f * 0.9f;
                            float z = lip == 0 ? -BlockDepth * 0.5f + 0.18f + ((h >> 24) & 0xf) / 15f * 0.22f
                                               : BlockDepth * 0.5f - 0.2f - ((h >> 24) & 0xf) / 15f * 0.3f;
                            if (x > right || !Clear(taken, x, z, 0.9f))
                                continue;
                            Dress(ref root, small[(int)((h >> 9) % (uint)small.Count)], x, b.y, z, h, 0.8f, 1.2f);
                            taken.Add(new Vector2(x, z));
                            placed++;
                        }
                }
            }
            if (placed > 0)
                Debug.Log($"[StageLayoutBuilder] auto dressing: {placed} props.");
        }

        private static List<GameObject> NonNull(GameObject[] a)
        {
            var list = new List<GameObject>();
            foreach (var g in Arr(a))
                if (g != null)
                    list.Add(g);
            return list;
        }

        private static bool Clear(List<Vector2> taken, float x, float z, float radius)
        {
            foreach (var t in taken)
                if (Mathf.Abs(t.x - x) < radius && Mathf.Abs(t.y - z) < radius)
                    return false;
            return true;
        }

        private static void Dress(ref Transform root, GameObject prefab, float x, float y, float z, uint h, float minScale, float maxScale)
        {
            if (root == null)
                root = new GameObject("AutoDressing").transform;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            go.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0f, (h >> 3) % 360u, 0f));
            go.transform.localScale = prefab.transform.localScale * Mathf.Lerp(minScale, maxScale, ((h >> 12) & 0xff) / 255f);
            DisableColliders(go);
        }

        /// <summary>
        /// Visual-only piers (kit.platformSupport) under static platform blocks, so they stand on something instead of
        /// hanging in the air. Skipped where the layout already places a "...Support" decoration under the platform
        /// or the platform rests on another block.
        /// </summary>
        private static void BuildPlatformSupports(SceneContext ctx, LayoutBlock[] blocks, LayoutDecoration[] decorations)
        {
            var kit = ctx.Theme;
            if (kit == null || kit.platformSupport == null)
                return;
            Transform root = null;
            foreach (var b in blocks)
            {
                if (b.kind != "platform")
                    continue;
                float bottom = b.y - b.h;
                bool rests = false, manual = false;
                foreach (var o in blocks)
                    if (o != b && o.x < b.x + b.w && o.x + o.w > b.x && o.y <= bottom + EdgeTolerance && bottom - o.y < PlatformSupportMinGap)
                        rests = true;
                foreach (var dec in decorations)
                    if (dec.prop != null && dec.prop.Contains("Support") && dec.x >= b.x - 0.5f && dec.x <= b.x + b.w + 0.5f)
                        manual = true;
                if (rests || manual)
                    continue;
                if (root == null)
                    root = new GameObject("PlatformSupports").transform;
                int n = Mathf.Max(1, Mathf.RoundToInt(b.w / PlatformSupportSpacing));
                for (int i = 0; i < n; i++)
                {
                    float x = b.x + b.w * (i + 0.5f) / n;
                    var go = AddArt(kit.platformSupport, root, $"Pier_{b.id}_{i:00}", new Vector3(x, bottom, 0f), Vector3.one);
                    go.isStatic = true;
                }
            }
        }

        /// <summary>True if no other block touches the given vertical edge of <paramref name="b"/> at height <paramref name="y"/>.</summary>
        private static bool BordersAir(LayoutBlock b, bool leftEdge, float y, LayoutBlock[] all)
        {
            float edge = leftEdge ? b.x : b.x + b.w;
            foreach (var o in all)
            {
                if (o == b)
                    continue;
                float otherEdge = leftEdge ? o.x + o.w : o.x;
                if (Mathf.Abs(otherEdge - edge) > EdgeTolerance)
                    continue;
                if (y < o.y && y > o.y - o.h)
                    return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ gameplay objects

        /// <summary>Checkpoint: the primitive "Pole" is replaced by the kit pole; the Banner and its references stay untouched.</summary>
        private static void ApplyCheckpointArt(SceneContext ctx, GameObject checkpoint)
        {
            if (ctx.Theme == null || ctx.Theme.checkpointVisual == null)
                return;
            HideRenderer(checkpoint.transform.Find("Pole"));
            AddArt(ctx.Theme.checkpointVisual, checkpoint.transform, "PoleArt", Vector3.zero, Vector3.one); // pivot bottom centre = surface
            HideStaticBanner(checkpoint.transform.Find("PoleArt"));
        }

        /// <summary>The kit pole may carry its own static banner mesh (*_Banner); the animated "Banner" flag replaces it.</summary>
        internal static void HideStaticBanner(Transform poleArt)
        {
            if (poleArt == null)
                return;
            foreach (var r in poleArt.GetComponentsInChildren<Renderer>(true))
                if (r.name.EndsWith("_Banner", System.StringComparison.Ordinal))
                    r.enabled = false;
        }

        /// <summary>Goal: the trigger cube keeps its collider; the arch (pivot bottom centre) stands on the surface below it.</summary>
        private static void ApplyGoalArt(SceneContext ctx, GameObject goal)
        {
            if (ctx.Theme == null || ctx.Theme.goalVisual == null)
                return;
            var art = NewArtRoot(goal.transform, "Art");
            AddArt(ctx.Theme.goalVisual, art, "GoalArt", new Vector3(0f, -GoalCenterAboveSurface, 0f), Vector3.one);
            HideRenderer(goal.transform);
        }

        /// <summary>Coin / gem: the art (pivot centre) goes under "Spin" so it keeps spinning; the primitive child is hidden.</summary>
        private static void ApplySpinArt(GameObject collectible, string primitiveChild, GameObject prefab)
        {
            if (prefab == null)
                return;
            var spin = collectible.transform.Find("Spin");
            if (spin == null)
            {
                Debug.LogWarning($"[StageLayoutBuilder] {collectible.name} has no 'Spin' child; theme art skipped.");
                return;
            }
            HideRenderer(spin.Find(primitiveChild));
            AddArt(prefab, spin, "Art", Vector3.zero, Vector3.one);
        }

        // ------------------------------------------------------------------ decorations

        /// <summary>Places the layout's decorations under a "Decorations" root. Returns how many were placed.</summary>
        private static int BuildDecorations(SceneContext ctx, StageLayoutData d)
        {
            var list = Arr(d.decorations);
            if (list.Length == 0)
                return 0;
            var root = new GameObject("Decorations").transform;
            int placed = 0;
            foreach (var dec in list)
            {
                PropRecord rec = null;
                if (ctx.Props == null || !ctx.Props.TryGet(dec.prop, out rec) || !rec.IsImplemented)
                {
                    Debug.LogWarning($"[StageLayoutBuilder] Decoration '{dec.id}' skipped: prop '{dec.prop}' is not an Implemented row of {PropCatalog.TablePath}.");
                    continue;
                }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rec.prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[StageLayoutBuilder] Decoration '{dec.id}' skipped: prefab not found: {rec.prefabPath}.");
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
                go.name = dec.id;
                go.transform.SetPositionAndRotation(new Vector3(dec.x, dec.y, dec.ResolveZ(rec.defaultZ)), Quaternion.Euler(0f, dec.rotY, 0f));
                go.transform.localScale = prefab.transform.localScale * dec.scale;
                if (!rec.collision)
                    DisableColliders(go);
                placed++;
            }
            return placed;
        }
    }
}
