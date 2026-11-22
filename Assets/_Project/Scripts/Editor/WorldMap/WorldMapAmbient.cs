using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Ambient animation for the world map art, driven by the art's naming conventions (see Docs/art/worldmap-asset-manifest.md):
    ///   materials - wind materials (leaves, flowers, banners, sails, "*_Wind") -> MyGame/Ambient/Wind Lit (sway weight = vertex colour R),
    ///               WM_Water / WM_WaterOasis -> Water (Sea), WM_WaterFlow -> Water (Flow), WM_Foam -> Water (Foam), WM_Lava -> Lava.
    ///   objects   - "*_Spin" -> AmbientSpin around the thinnest bounds axis, "*_Bob" -> AmbientBob,
    ///               renderers using WM_IceCrystal* / WM_Ember -> AmbientPulse, "FX_&lt;Kind&gt;_n" empties -> particle prefabs.
    /// Run by WorldMapBuilder.Build after the art is placed; idempotent.
    /// </summary>
    public static class WorldMapAmbient
    {
        public const string FxDir = WorldMapBuilder.PrefabDir + "FX/";
        private const string AmbientRootName = "MapAmbient";

        internal const string WindShader = "MyGame/Ambient/Wind Lit";
        private const string WaterShader = "MyGame/Ambient/Water";
        private const string LavaShader = "MyGame/Ambient/Lava";
        private const string ParticleShader = "MyGame/Ambient/Particle";

        // name -> (sway metres, speed, flutter, double sided)
        private static readonly Dictionary<string, (float strength, float speed, float flutter, bool twoSided)> WindPresets = new()
        {
            // Trees lean slowly as one mass (almost no per-vertex flutter, which reads as wobbling jelly); cloth flutters.
            { "WM_LeafDark", (0.035f, 0.8f, 0.0f, false) },
            { "WM_LeafGreen", (0.035f, 0.8f, 0.0f, false) },
            { "WM_LeafLight", (0.035f, 0.8f, 0.0f, false) },
            { "WM_PineLeaf", (0.025f, 0.7f, 0.0f, false) },
            { "WM_PineLeafLight", (0.025f, 0.7f, 0.0f, false) },
            { "WM_PalmLeaf", (0.07f, 1.0f, 0.05f, true) },
            { "WM_PalmLeafLight", (0.07f, 1.0f, 0.05f, true) },
            { "WM_FlowerPink", (0.03f, 1.4f, 0.08f, false) },
            { "WM_FlowerWhite", (0.03f, 1.4f, 0.08f, false) },
            { "WM_FlowerYellow", (0.03f, 1.4f, 0.08f, false) },
            { "WM_Banner", (0.22f, 2.8f, 0.45f, true) },
            { "WM_Sail", (0.08f, 1.3f, 0.10f, true) },
        };
        private static readonly (float strength, float speed, float flutter, bool twoSided) DefaultWind = (0.03f, 1.2f, 0.05f, true);

        private static readonly string[] PulsePrefixes = { "WM_IceCrystal", "WM_Ember" };

        [MenuItem("Tools/MyGame/World Map/Apply Ambient Animation")]
        public static void ApplyMenu()
        {
            var world = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name.StartsWith("WM_World") && t.parent != null && t.parent.name == "MapEnvironment");
            if (world == null)
            {
                Debug.LogError("[WorldMapAmbient] open 05_WorldMap with the WM_World art first.");
                return;
            }
            Debug.Log(Apply(world.gameObject));
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
        }

        public static string Apply(GameObject world)
        {
            var log = new System.Text.StringBuilder("  ambient:");
            var renderers = world.GetComponentsInChildren<Renderer>(true);
            log.Append(ConvertMaterials(renderers));

            var (spins, bobs) = ConfigureMotion(world);
            int pulses = 0;
            foreach (var r in renderers)
                if (r.sharedMaterials.Any(NeedsPulse) && AddOnce<AmbientPulse>(r.gameObject, out _))
                    pulses++;
            log.Append($" spin {spins}, bob {bobs}, pulse {pulses}");

            log.Append(PlaceEffects(world));
            return log.ToString();
        }

        /// <summary>Adds AmbientSpin / AmbientBob to every "*_Spin" / "*_Bob" object below <paramref name="root"/> (also used on asset prefabs by WorldMapAssetImporter).</summary>
        internal static (int spins, int bobs) ConfigureMotion(GameObject root)
        {
            int spins = 0, bobs = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.EndsWith("_Spin") && AddOnce<AmbientSpin>(t.gameObject, out var spin))
                {
                    spin.Configure(ThinnestAxis(t), 35f);
                    spins++;
                }
                if (t.name.EndsWith("_Bob") && AddOnce<AmbientBob>(t.gameObject, out _))
                    bobs++;
            }
            return (spins, bobs);
        }

        /// <summary>Legacy v2 emissive materials by name; v3 materials by an emission texture from Art/WorldMap/Textures with a non-black emission colour.</summary>
        private static bool NeedsPulse(Material m)
        {
            if (m == null) return false;
            if (PulsePrefixes.Any(p => m.name.StartsWith(p))) return true;
            if (!IsV3Material(m) || !m.HasProperty("_EmissionMap") || !m.HasProperty("_EmissionColor")) return false;
            var tex = m.GetTexture("_EmissionMap");
            return tex != null && AssetDatabase.GetAssetPath(tex).Contains("_Emission") && m.GetColor("_EmissionColor").maxColorComponent > 0f;
        }

        /// <summary>v3 materials (Materials/v3/M_WM_*) are fully configured by the importer: one material per asset, wind chosen by shader.</summary>
        internal static bool IsV3Material(Material m) => m != null && AssetDatabase.GetAssetPath(m).Contains("/Materials/v3/");

        private static bool AddOnce<T>(GameObject go, out T component) where T : Component
        {
            component = go.GetComponent<T>();
            if (component != null)
                return false;
            component = go.AddComponent<T>();
            return true;
        }

        /// <summary>Blades and wheels are flat: they spin around their thinnest local axis.</summary>
        private static Vector3 ThinnestAxis(Transform t)
        {
            var mf = t.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return Vector3.forward;
            Vector3 s = mf.sharedMesh.bounds.size;
            if (s.x <= s.y && s.x <= s.z) return Vector3.right;
            return s.y <= s.z ? Vector3.up : Vector3.forward;
        }

        // ------------------------------------------------------------------ materials

        private static string ConvertMaterials(Renderer[] renderers)
        {
            var users = new Dictionary<Material, List<Mesh>>();
            foreach (var r in renderers)
            {
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (IsV3Material(m)) continue;
                    if (!users.TryGetValue(m, out var list)) users[m] = list = new List<Mesh>();
                    if (mesh != null && !list.Contains(mesh)) list.Add(mesh);
                }
            }

            int wind = 0, water = 0, lava = 0;
            var noColour = new List<string>();
            foreach (var (mat, meshes) in users)
            {
                string n = mat.name;
                if (WindPresets.ContainsKey(n) || n.EndsWith("_Wind"))
                {
                    MakeWind(mat, meshes, noColour);
                    wind++;
                }
                else if (n == "WM_Water" || n == "WM_WaterOasis")
                {
                    MakeWater(mat, "_MODE_SEA", 0, 3000);
                    water++;
                }
                else if (n == "WM_WaterFlow")
                {
                    MakeWater(mat, "_MODE_FLOW", 1, 3001);
                    water++;
                }
                else if (n == "WM_Foam")
                {
                    MakeWater(mat, "_MODE_FOAM", 2, 3002);
                    water++;
                }
                else if (n == "WM_Lava")
                {
                    SwapShader(mat, LavaShader);
                    lava++;
                }
            }
            AssetDatabase.SaveAssets();
            string warn = noColour.Count > 0 ? $" (no vertex colours, height-weighted: {string.Join(", ", noColour)})" : "";
            return $" materials wind {wind}{warn}, water {water}, lava {lava};";
        }

        internal static void SwapShader(Material mat, string shaderName)
        {
            var shader = Shader.Find(shaderName) ?? throw new System.InvalidOperationException("Shader not found: " + shaderName);
            if (mat.shader == shader) return;
            // Unity keeps values of same-named properties (_BaseColor, _EmissionColor) across the swap.
            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
            Color emission = mat.HasProperty("_EmissionColor") && mat.IsKeywordEnabled("_EMISSION") ? mat.GetColor("_EmissionColor") : Color.black;
            mat.shader = shader;
            mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emission);
            mat.shaderKeywords = new string[0];
            EditorUtility.SetDirty(mat);
        }

        private static void MakeWind(Material mat, List<Mesh> meshes, List<string> noColour)
        {
            SwapShader(mat, WindShader);
            var p = WindPresets.TryGetValue(mat.name, out var preset) ? preset : DefaultWind;
            mat.SetFloat("_WindStrength", p.strength);
            mat.SetFloat("_WindSpeed", p.speed);
            mat.SetFloat("_Flutter", p.flutter);
            mat.SetFloat("_Cull", (float)(p.twoSided ? CullMode.Off : CullMode.Back));
            mat.enableInstancing = true;

            // Art without vertex colours falls back to object-space height weighting.
            bool coloured = meshes.Count > 0 && meshes.All(m => m.colors32 != null && m.colors32.Length == m.vertexCount && m.vertexCount > 0);
            mat.SetFloat("_HeightWeight", coloured ? 0f : 1f);
            if (!coloured)
            {
                noColour.Add(mat.name);
                float lo = meshes.Count > 0 ? meshes.Average(m => m.bounds.min.y) : 0f;
                float hi = meshes.Count > 0 ? meshes.Average(m => m.bounds.max.y) : 1f;
                mat.SetVector("_HeightRange", new Vector4(lo + (hi - lo) * 0.25f, hi, 0f, 0f));
            }
            EditorUtility.SetDirty(mat);
        }

        internal static void MakeWater(Material mat, string keyword, int mode, int queue)
        {
            bool fresh = mat.shader.name != WaterShader;
            Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : new Color(0.15f, 0.71f, 0.92f, 0.82f);
            SwapShader(mat, WaterShader);
            mat.SetFloat("_Mode", mode);
            foreach (var k in new[] { "_MODE_SEA", "_MODE_FLOW", "_MODE_FOAM" })
                if (k == keyword) mat.EnableKeyword(k); else mat.DisableKeyword(k);
            if (fresh)
            {
                mat.SetColor("_BaseColor", baseColor);
                Color.RGBToHSV(baseColor, out float h, out float s, out float v);
                Color shallow = Color.HSVToRGB(h, s * 0.55f, Mathf.Min(1f, v * 1.12f));
                shallow.a = baseColor.a * 0.6f;
                mat.SetColor("_ShallowColor", shallow);
                if (mode == 2) mat.SetColor("_FoamColor", baseColor);
            }
            mat.renderQueue = queue;
            EditorUtility.SetDirty(mat);
        }

        // ------------------------------------------------------------------ particles

        private static string PlaceEffects(GameObject world)
        {
            var root = world.scene.GetRootGameObjects().FirstOrDefault(g => g.name == AmbientRootName) ?? new GameObject(AmbientRootName);
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            if (root.GetComponent<AmbientWind>() == null)
                root.AddComponent<AmbientWind>();

            var counts = new Dictionary<string, int>();
            foreach (var t in world.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("FX_")) continue;
                string kind = t.name.Split('_')[1];
                var prefab = EffectPrefab(kind);
                if (prefab == null) continue;
                var fx = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                fx.name = t.name;
                fx.transform.SetPositionAndRotation(t.position, Quaternion.identity);
                counts[kind] = counts.TryGetValue(kind, out int c) ? c + 1 : 1;
            }
            return counts.Count == 0 ? ", fx none" : ", fx " + string.Join(" ", counts.Select(kv => $"{kv.Key} {kv.Value}"));
        }

        internal static GameObject EffectPrefab(string kind)
        {
            string path = FxDir + "FX_" + kind + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var go = new GameObject("FX_" + kind);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            var emission = ps.emission;
            var shape = ps.shape;
            var col = ps.colorOverLifetime;
            var size = ps.sizeOverLifetime;
            var vel = ps.velocityOverLifetime;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            main.loop = true;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;
            shape.rotation = new Vector3(-90f, 0f, 0f); // cones and circles face up
            col.enabled = true;
            size.enabled = true;
            bool additive = false;

            switch (kind)
            {
                case "Smoke":
                    main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 6f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
                    main.startColor = new Color(0.75f, 0.72f, 0.7f, 0.55f);
                    main.maxParticles = 60;
                    emission.rateOverTime = 5f;
                    shape.shapeType = ParticleSystemShapeType.Cone;
                    shape.angle = 12f;
                    shape.radius = 0.5f;
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.World;
                    vel.x = new ParticleSystem.MinMaxCurve(0.5f, 0.5f);
                    vel.y = new ParticleSystem.MinMaxCurve(0f, 0f);
                    vel.z = new ParticleSystem.MinMaxCurve(0.3f, 0.3f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3f));
                    col.color = Fade(Color.white, 0.15f, 0.6f);
                    break;
                case "Embers":
                    main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3.5f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.1f), new Color(1f, 0.85f, 0.3f));
                    main.maxParticles = 120;
                    emission.rateOverTime = 14f;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.rotation = Vector3.zero;
                    shape.scale = new Vector3(6f, 0.1f, 6f);
                    var noise = ps.noise;
                    noise.enabled = true;
                    noise.strength = 0.4f;
                    noise.frequency = 0.8f;
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.World;
                    vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
                    vel.y = new ParticleSystem.MinMaxCurve(0.6f, 0.6f);
                    vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
                    col.color = Fade(Color.white, 0.1f, 0.5f);
                    additive = true;
                    break;
                case "Mist":
                    main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
                    main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                    main.startColor = new Color(1f, 1f, 1f, 0.35f);
                    main.maxParticles = 40;
                    emission.rateOverTime = 9f;
                    shape.shapeType = ParticleSystemShapeType.Hemisphere;
                    shape.radius = 0.9f;
                    size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.5f));
                    col.color = Fade(Color.white, 0.2f, 0.5f);
                    break;
                case "Sparkle":
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                    main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.95f, 1f), Color.white);
                    main.maxParticles = 20;
                    emission.rateOverTime = 4f;
                    shape.shapeType = ParticleSystemShapeType.Sphere;
                    shape.radius = 1.4f;
                    size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0f)));
                    col.color = Fade(Color.white, 0.3f, 0.3f);
                    additive = true;
                    break;
                case "Snow":
                    main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 12f);
                    main.startSpeed = 0f;
                    main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
                    main.startColor = new Color(1f, 1f, 1f, 0.9f);
                    main.maxParticles = 900;
                    emission.rateOverTime = 70f;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.rotation = Vector3.zero;
                    shape.position = new Vector3(0f, 12f, 0f);
                    shape.scale = new Vector3(32f, 0.1f, 32f);
                    var snowNoise = ps.noise;
                    snowNoise.enabled = true;
                    snowNoise.strength = 0.3f;
                    snowNoise.frequency = 0.3f;
                    vel.enabled = true;
                    vel.space = ParticleSystemSimulationSpace.World;
                    vel.x = new ParticleSystem.MinMaxCurve(0.35f, 0.35f);
                    vel.y = new ParticleSystem.MinMaxCurve(-1.2f, -1.2f);
                    vel.z = new ParticleSystem.MinMaxCurve(0.15f, 0.15f);
                    size.enabled = false;
                    col.color = Fade(Color.white, 0.1f, 0.15f);
                    break;
                default:
                    Object.DestroyImmediate(go);
                    Debug.LogWarning("[WorldMapAmbient] unknown FX kind: " + kind);
                    return null;
            }

            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ParticleMaterial(additive ? "M_FX_Additive" : "M_FX_Soft", additive);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            SceneFlowSetup.EnsureFolder(FxDir.TrimEnd('/'));
            var saved = SceneFlowSetup.SavePrefab(go, path);
            return saved;
        }

        /// <summary>Alpha 0 -> 1 over <paramref name="fadeIn"/> of the lifetime, back to 0 over the last <paramref name="fadeOut"/>.</summary>
        private static ParticleSystem.MinMaxGradient Fade(Color c, float fadeIn, float fadeOut)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, 1f - fadeOut), new GradientAlphaKey(0f, 1f) });
            return new ParticleSystem.MinMaxGradient(g);
        }

        private static Material ParticleMaterial(string name, bool additive)
        {
            string path = FxDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
            SceneFlowSetup.EnsureFolder(FxDir.TrimEnd('/'));
            mat = new Material(Shader.Find(ParticleShader)) { name = name, enableInstancing = true };
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_Softness", additive ? 0.9f : 0.7f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
