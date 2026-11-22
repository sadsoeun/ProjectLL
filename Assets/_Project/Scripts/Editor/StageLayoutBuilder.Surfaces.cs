using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    // Block surfaces of schema v1.6: bounce pad, crumbling block, conveyor belt. Each gets its runtime component and a
    // marker that reads from the side camera by shape and colour (pink cushion / pale cracked cap / dark belt with
    // yellow arrows). The markers are simple generated shapes on top of the themed block art.
    public static partial class StageLayoutBuilder
    {
        private static readonly Color BounceColor = new Color(1.00f, 0.42f, 0.62f);
        private static readonly Color BounceRimColor = new Color(0.98f, 0.92f, 0.86f);
        private static readonly Color CrumbleColor = new Color(0.83f, 0.78f, 0.68f);
        private static readonly Color CrackColor = new Color(0.23f, 0.19f, 0.16f);
        private static readonly Color BeltColor = new Color(0.20f, 0.21f, 0.25f);
        private static readonly Color ArrowColor = new Color(1.00f, 0.83f, 0.12f);

        private const float ConveyorSpeed = 3f;

        private static void ApplySurfaceGimmick(GameObject cube, LayoutBlock b, StageThemeKit kit)
        {
            switch (b.surface)
            {
                case "bounce": BuildBouncePad(cube, b); break;
                case "crumble": BuildCrumble(cube, b, kit); break;
                case "conveyor_r": BuildConveyor(cube, b, ConveyorSpeed); break;
                case "conveyor_l": BuildConveyor(cube, b, -ConveyorSpeed); break;
            }
        }

        /// <summary>Unscaled marker root centred on the block (the block cube itself is scaled to its size).</summary>
        private static Transform MarkRoot(GameObject cube) => NewArtRoot(cube.transform, "SurfaceMark");

        private static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, string matName, Color color, float rotZ = 0f)
        {
            var go = SceneFlowSetup.CreatePrimitive(type, name, parent, pos, scale, SceneFlowSetup.LitMaterial(matName, color), false);
            if (!Mathf.Approximately(rotZ, 0f))
                go.transform.localRotation = Quaternion.Euler(0f, 0f, rotZ);
            return go;
        }

        private static void BuildBouncePad(GameObject cube, LayoutBlock b)
        {
            var pad = cube.AddComponent<BounceSurface>();
            var root = MarkRoot(cube);
            float top = b.h * 0.5f;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(SurfaceGimmickSetup.SpringPadPrefab);
            if (model != null)
            {
                // modelled spring pad (authored for a 2 m block): base plate + the cushion that is squashed on a launch
                var art = AddArt(model, root, "SpringPad", new Vector3(0f, top, 0f), new Vector3(b.w / SpringPadWidth, 1f, 1f));
                EditorWire.Set(pad, "cushion", art.transform.Find("Cushion"));
                return;
            }
            // pale rim plate + a pink dome that is squashed on every launch
            Shape(PrimitiveType.Cube, "Rim", root, new Vector3(0f, top + 0.04f, 0f), new Vector3(b.w + 0.1f, 0.08f, BlockDepth * 0.9f), "M_BounceRim", BounceRimColor);
            var pivot = new GameObject("Cushion").transform; // scaled from its base, so the squash stays on the pad
            pivot.SetParent(root, false);
            pivot.localPosition = new Vector3(0f, top + 0.08f, 0f);
            Shape(PrimitiveType.Sphere, "Dome", pivot, Vector3.zero, new Vector3(b.w * 0.92f, 0.62f, BlockDepth * 0.78f), "M_BouncePad", BounceColor);
            EditorWire.Set(pad, "cushion", pivot);
        }

        private const float CrumbleSlabLength = 3f;   // authored size of kit.crumbleSlab
        private const float CrumbleSlabThickness = 0.5f;

        private static void BuildCrumble(GameObject cube, LayoutBlock b, StageThemeKit kit)
        {
            var crumble = cube.AddComponent<CrumbleSurface>();
            var root = MarkRoot(cube);
            float top = b.h * 0.5f;
            if (kit != null && kit.crumbleSlab != null)
            {
                // the fractured slab replaces the themed platform: its chunks are what falls when the block breaks
                var themed = cube.transform.Find("Art");
                if (themed != null)
                    Object.DestroyImmediate(themed.gameObject);
                HideRenderer(cube.transform);
                int n = Mathf.Max(1, Mathf.RoundToInt(b.w / CrumbleSlabLength));
                float step = b.w / n;
                var chunks = new List<Transform>();
                var dust = new List<ParticleSystem>();
                for (int i = 0; i < n; i++)
                {
                    var slab = AddArt(kit.crumbleSlab, root, $"Slab_{i:00}", new Vector3(-b.w * 0.5f + i * step, top, 0f),
                        new Vector3(step / CrumbleSlabLength, b.h / CrumbleSlabThickness, 1f));
                    foreach (var r in slab.GetComponentsInChildren<MeshRenderer>())
                        chunks.Add(r.transform);
                    dust.AddRange(slab.GetComponentsInChildren<ParticleSystem>());
                }
                EditorWire.Set(crumble, "chunks", chunks.ToArray());
                EditorWire.Set(crumble, "dust", dust.ToArray());
                EditorWire.Set(crumble, "visual", root);
                return;
            }
            float front = -(BlockDepth * 0.5f + 0.02f);
            // loose pale cap tiles with gaps between them
            int tiles = Mathf.Max(2, Mathf.RoundToInt(b.w / 1.0f));
            float tileW = b.w / tiles;
            for (int i = 0; i < tiles; i++)
            {
                float x = -b.w * 0.5f + tileW * (i + 0.5f);
                float tilt = (i % 2 == 0 ? 1f : -1f) * 2.5f;
                Shape(PrimitiveType.Cube, $"Tile_{i:00}", root, new Vector3(x, top + 0.05f, 0f), new Vector3(tileW - 0.1f, 0.12f, BlockDepth + 0.08f), "M_CrumbleCap", CrumbleColor, tilt);
            }
            // dark cracks on the face the camera sees
            float crackH = Mathf.Min(b.h, 0.9f);
            int cracks = Mathf.Max(2, Mathf.RoundToInt(b.w / 1.3f));
            for (int i = 0; i < cracks; i++)
            {
                float x = -b.w * 0.5f + b.w * (i + 0.5f) / cracks;
                float lean = (i % 2 == 0 ? 1f : -1f) * 22f;
                Shape(PrimitiveType.Cube, $"Crack_{i:00}a", root, new Vector3(x, top - crackH * 0.28f, front), new Vector3(0.07f, crackH * 0.55f, 0.04f), "M_CrumbleCrack", CrackColor, lean);
                Shape(PrimitiveType.Cube, $"Crack_{i:00}b", root, new Vector3(x + 0.12f * Mathf.Sign(lean), top - crackH * 0.72f, front), new Vector3(0.06f, crackH * 0.4f, 0.04f), "M_CrumbleCrack", CrackColor, -lean);
            }
            // shake the themed art together with the marker when there is one
            var art = cube.transform.Find("Art");
            if (art != null)
                art.SetParent(root, true);
            EditorWire.Set(crumble, "visual", root);
        }

        // Authored sizes of the surface gimmick kit (ArtSource/Common/_build_scripts/build_surface_gimmicks.py).
        private const float SpringPadWidth = 2f;
        private const float ConveyorEndLength = 0.5f;
        private const float ConveyorBeltTop = 0.04f;
        private const float ConveyorCleatPitch = 0.3125f;
        private const float ConveyorArrowPitch = 0.8f;
        private static readonly Vector2 ConveyorWheelAt = new Vector2(-0.24f, -0.075f);   // y below the top, z in front of the face
        private static readonly Vector3 ConveyorDrumAt = new Vector3(0.24f, -0.37f, -0.225f); // x from the end, y, z in front of the face
        private static readonly Vector2 ConveyorArrowAt = new Vector2(-0.565f, -0.079f);

        /// <summary>
        /// Modelled conveyor: end housings with a driving drum, 1 m belt sections with rollers, ribs that travel along the
        /// belt and chevrons that travel along the front panel. False when the kit prefabs are missing.
        /// </summary>
        private static bool BuildConveyorKit(ConveyorSurface belt, Transform root, LayoutBlock b, float top, float speed)
        {
            GameObject Piece(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(SurfaceGimmickSetup.ConveyorPrefab(name));
            GameObject mid = Piece("Mid"), end = Piece("End"), wheel = Piece("Wheel"), drum = Piece("Drum"), cleat = Piece("Cleat"), arrow = Piece("Arrow");
            if (mid == null || end == null || wheel == null || drum == null || cleat == null || arrow == null || b.w < 2f * ConveyorEndLength + 0.5f)
                return false;

            float half = b.w * 0.5f;
            float face = -BlockDepth * 0.5f;
            AddArt(end, root, "End_L", new Vector3(-half, top, 0f), Vector3.one);
            AddArt(end, root, "End_R", new Vector3(half, top, 0f), new Vector3(-1f, 1f, 1f));

            var wheels = new List<Transform>();
            float inner = b.w - 2f * ConveyorEndLength;
            int tiles = Mathf.Max(1, Mathf.RoundToInt(inner));
            float tileW = inner / tiles;
            for (int i = 0; i < tiles; i++)
            {
                float x = -half + ConveyorEndLength + tileW * (i + 0.5f);
                AddArt(mid, root, $"Belt_{i:00}", new Vector3(x, top, 0f), new Vector3(tileW, 1f, 1f));
                foreach (float side in new[] { -0.25f, 0.25f })
                    wheels.Add(AddArt(wheel, root, $"Wheel_{wheels.Count:00}", new Vector3(x + side * tileW, top + ConveyorWheelAt.x, face + ConveyorWheelAt.y), Vector3.one).transform);
            }

            // the drums are not children of the (mirrored) housings, so both turn the same way
            var drums = new List<Transform>();
            foreach (float sx in new[] { -1f, 1f })
                drums.Add(AddArt(drum, root, sx < 0f ? "Drum_L" : "Drum_R",
                    new Vector3(sx * (half - ConveyorDrumAt.x), top + ConveyorDrumAt.y, face + ConveyorDrumAt.z), Vector3.one).transform);

            // travelling parts: ribs on the belt and chevrons on the front panel wrap around at the housings, which hide the jump
            var moving = new List<Transform>();
            int ribs = Mathf.Max(2, Mathf.RoundToInt(b.w / ConveyorCleatPitch));
            for (int i = 0; i < ribs; i++)
                moving.Add(AddArt(cleat, root, $"Cleat_{i:00}", new Vector3(-half + b.w * (i + 0.5f) / ribs, top + ConveyorBeltTop, 0f), Vector3.one).transform);
            int count = Mathf.Max(2, Mathf.RoundToInt(b.w / ConveyorArrowPitch));
            for (int i = 0; i < count; i++)
            {
                var a = AddArt(arrow, root, $"Arrow_{i:00}", new Vector3(-half + b.w * (i + 0.5f) / count, top + ConveyorArrowAt.x, face + ConveyorArrowAt.y), Vector3.one);
                if (speed < 0f)
                    a.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
                moving.Add(a.transform);
            }

            EditorWire.Set(belt, "arrows", moving.ToArray());
            EditorWire.Set(belt, "halfLength", half);
            EditorWire.Set(belt, "wheels", wheels.ToArray());
            EditorWire.Set(belt, "drums", drums.ToArray());
            return true;
        }

        private static void BuildConveyor(GameObject cube, LayoutBlock b, float speed)
        {
            var belt = cube.AddComponent<ConveyorSurface>();
            EditorWire.Set(belt, "speed", speed);
            var root = MarkRoot(cube);
            float top = b.h * 0.5f;
            if (BuildConveyorKit(belt, root, b, top, speed))
                return;
            float front = -(BlockDepth * 0.5f + 0.03f);
            float bandH = Mathf.Min(0.6f, b.h);
            Shape(PrimitiveType.Cube, "Belt", root, new Vector3(0f, top + 0.03f, 0f), new Vector3(b.w, 0.08f, BlockDepth + 0.06f), "M_ConveyorBelt", BeltColor);
            Shape(PrimitiveType.Cube, "Band", root, new Vector3(0f, top - bandH * 0.5f, front + 0.01f), new Vector3(b.w, bandH, 0.04f), "M_ConveyorBelt", BeltColor);
            // rollers at both ends
            foreach (float sx in new[] { -1f, 1f })
            {
                var roller = Shape(PrimitiveType.Cylinder, sx < 0f ? "Roller_L" : "Roller_R", root,
                    new Vector3(sx * (b.w * 0.5f - 0.02f), top - bandH * 0.5f, 0f), new Vector3(bandH, BlockDepth * 0.5f + 0.04f, bandH), "M_ConveyorBelt", BeltColor);
                roller.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            // arrow heads pointing the way the belt carries, scrolled by ConveyorSurface
            var arrows = new List<Transform>();
            int count = Mathf.Max(2, Mathf.RoundToInt(b.w / 1.2f));
            float dir = Mathf.Sign(speed);
            float arm = bandH * 0.42f;
            for (int i = 0; i < count; i++)
            {
                var a = new GameObject($"Arrow_{i:00}").transform;
                a.SetParent(root, false);
                a.localPosition = new Vector3(-b.w * 0.5f + b.w * (i + 0.5f) / count, top - bandH * 0.5f, front - 0.01f);
                Shape(PrimitiveType.Cube, "Up", a, new Vector3(0f, arm * 0.33f, 0f), new Vector3(0.09f, arm, 0.04f), "M_ConveyorArrow", ArrowColor, dir * 45f);
                Shape(PrimitiveType.Cube, "Down", a, new Vector3(0f, -arm * 0.33f, 0f), new Vector3(0.09f, arm, 0.04f), "M_ConveyorArrow", ArrowColor, -dir * 45f);
                arrows.Add(a);
            }
            EditorWire.Set(belt, "arrows", arrows.ToArray());
            EditorWire.Set(belt, "halfLength", b.w * 0.5f);
        }
    }
}
