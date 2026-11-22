using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Crumbling platform slab (T11): one fractured stone slab model shared by every world, recoloured per theme.
    /// Source: ArtSource/Common/_build_scripts/build_crumble_slab.py (Blender). This menu makes the toon materials,
    /// one prefab per world (model chunks + a dust burst) and assigns it to the theme kits (crumbleSlab). Idempotent.
    /// Rebuild the stages afterwards so blocks with surface "crumble" pick it up.
    /// </summary>
    public static class CrumbleSlabSetup
    {
        private const string ToonShader = "MyGame/Toon/Lit";
        private const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";
        private const string ModelPath = "Assets/_Project/Art/Environment/Common/Models/T11_CrumbleSlab.fbx";
        private const string MatDir = "Assets/_Project/Art/Environment/Common/Materials";
        private const string PrefabDir = "Assets/_Project/Prefabs/Environment/Common";

        private readonly struct Look
        {
            public readonly string Key, Theme;
            public readonly Color Top, Side, Inner, Dust;
            public readonly bool Glow;
            public Look(string key, string theme, Color top, Color side, Color inner, Color dust, bool glow = false)
            { Key = key; Theme = theme; Top = top; Side = side; Inner = inner; Dust = dust; Glow = glow; }
        }

        // top = walked face (palest, so the slab reads as loose stone), side = outer faces, inner = fracture faces
        private static readonly Look[] Looks =
        {
            new Look("GM", "GreenMeadow", new Color(0.92f, 0.80f, 0.58f), new Color(0.78f, 0.62f, 0.42f), new Color(0.52f, 0.38f, 0.26f), new Color(0.86f, 0.76f, 0.58f)),
            new Look("SD", "SunDunes", new Color(0.99f, 0.82f, 0.56f), new Color(0.93f, 0.62f, 0.36f), new Color(0.66f, 0.38f, 0.22f), new Color(0.96f, 0.80f, 0.56f)),
            new Look("FP", "FrostPeaks", new Color(0.94f, 0.97f, 1.00f), new Color(0.62f, 0.70f, 0.83f), new Color(0.36f, 0.44f, 0.62f), new Color(0.92f, 0.96f, 1.00f)),
            new Look("CC", "CoralCoast", new Color(0.98f, 0.91f, 0.80f), new Color(0.91f, 0.74f, 0.64f), new Color(0.62f, 0.42f, 0.40f), new Color(0.96f, 0.88f, 0.76f)),
            new Look("EF", "EmberFortress", new Color(0.50f, 0.43f, 0.50f), new Color(0.34f, 0.28f, 0.35f), new Color(1.00f, 0.48f, 0.10f), new Color(0.42f, 0.36f, 0.40f), true),
        };

        [MenuItem("Tools/MyGame/Art/Setup Crumble Slabs")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            AssetDatabase.Refresh();
            var sb = new StringBuilder("[CrumbleSlabSetup]\n");
            var shader = Shader.Find(ToonShader);
            if (shader == null)
                return "Shader not found: " + ToonShader;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                return "FBX missing: " + ModelPath;
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();

            foreach (var look in Looks)
            {
                var mats = new Dictionary<string, Material>
                {
                    { "Top", Toon(shader, $"M_{look.Key}_Crumble_Top", look.Top, false) },
                    { "Side", Toon(shader, $"M_{look.Key}_Crumble_Side", look.Side, false) },
                    { "Inner", Toon(shader, $"M_{look.Key}_Crumble_Inner", look.Inner, look.Glow) },
                };

                string id = $"{look.Key}_T11_CrumbleSlab";
                var root = new GameObject(id);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                visual.name = "Visual";
                int chunks = 0;
                var bounds = new Bounds();
                bool first = true;
                foreach (var r in visual.GetComponentsInChildren<MeshRenderer>())
                {
                    var slots = r.sharedMaterials;
                    for (int i = 0; i < slots.Length; i++)
                    {
                        string n = slots[i] != null ? slots[i].name : "";
                        slots[i] = mats[n.EndsWith("Top") ? "Top" : n.EndsWith("Inner") ? "Inner" : "Side"];
                    }
                    r.sharedMaterials = slots;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
                    chunks++;
                }
                // model: X 0..3, top at Y 0, depth centred -> same pivot as the platform modules (top-left / depth centre)
                BuildDust(root.transform, look, bounds);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{id}.prefab");
                Object.DestroyImmediate(root);
                var kit = AssetDatabase.LoadAssetAtPath<StageThemeKit>(StageThemeKit.PathFor(look.Theme));
                if (kit != null)
                {
                    kit.crumbleSlab = prefab;
                    EditorUtility.SetDirty(kit);
                }
                sb.AppendLine($"  {id}: {chunks} chunks, bounds min {bounds.min} max {bounds.max}, kit {(kit != null ? "assigned" : "MISSING")}");
            }
            AssetDatabase.SaveAssets();
            return sb.ToString();
        }

        private static Material Toon(Shader shader, string name, Color color, bool glow)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            if (glow)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(2.0f, 0.62f, 0.10f));
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material DustMaterial()
        {
            string path = $"{MatDir}/M_Crumble_Dust.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(ParticleShader);
            if (shader == null)
                return null;
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_Crumble_Dust" };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            var tex = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", Color.white);
            // alpha blended, no depth write
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>One soft puff burst along the slab; CrumbleSurface plays it when the block breaks.</summary>
        private static void BuildDust(Transform root, Look look, Bounds slab)
        {
            var go = new GameObject("Dust");
            go.transform.SetParent(root, false);
            go.transform.localPosition = new Vector3(slab.center.x, slab.center.y, slab.min.z - 0.1f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var pale = look.Dust; pale.a = 0.55f;
            var dim = Color.Lerp(look.Dust, look.Side, 0.5f); dim.a = 0.45f;
            main.startColor = new ParticleSystem.MinMaxGradient(pale, dim);
            main.gravityModifier = -0.03f;
            main.maxParticles = 24;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 9), new ParticleSystem.Burst(0.14f, 6) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(slab.size.x * 0.9f, slab.size.y * 0.6f, 0.6f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.45f), new Keyframe(0.35f, 1f), new Keyframe(1f, 1.25f)));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            var mat = DustMaterial();
            if (mat != null)
                pr.sharedMaterial = mat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
