using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// World 5 ambience built in Unity (no art files): the animated lava sea material (MyGame/Ambient/Lava), the volcanic sky
    /// backdrop (MyGame/Sky/ToonSky) and two decoration particle prefabs: EF_FX03_LavaBubbles (FX03 lava surface loop:
    /// glowing swells that pop on the lava sea) and EF_FX10_EmberDrift (sparse embers rising from the lava). Called by
    /// <see cref="EmberFortressImporter"/>; idempotent (assets are updated in place, prefab GUIDs kept).
    /// </summary>
    public static class EmberFortressAmbient
    {
        private const string MatDir = "Assets/_Project/Art/Environment/EmberFortress/Background/Materials";
        private const string FxPrefabDir = "Assets/_Project/Prefabs/Environment/EmberFortress/FX";
        public const string SkyPath = "Assets/_Project/Art/Materials/Sky/M_Sky_EmberFortress.mat";

        private static Material EnsureMaterial(string path, string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
                throw new System.InvalidOperationException("Shader not found: " + shaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                SceneFlowSetup.EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }
            return mat;
        }

        /// <summary>Key-art cel lava (MyGame/Env/LavaSea): bright orange cells, amber-red seams, pale-yellow cores, sparse crust islands.</summary>
        public static Material EnsureLavaMaterial()
        {
            var mat = EnsureMaterial($"{MatDir}/M_EF_B08_LavaFlow.mat", "MyGame/Env/LavaSea");
            mat.SetColor("_SeamColor", new Color(0.88f, 0.33f, 0.06f));
            mat.SetColor("_MoltenColor", new Color(1f, 0.5f, 0.1f));
            mat.SetColor("_HotColor", new Color(1f, 0.68f, 0.24f));
            mat.SetColor("_CrustColor", new Color(0.36f, 0.24f, 0.3f));
            mat.SetFloat("_Brightness", 0.88f);
            mat.SetFloat("_CellScale", 0.15f);
            mat.SetFloat("_SeamWidth", 0.05f);
            mat.SetFloat("_HotSize", 0.42f);
            mat.SetFloat("_SwirlScale", 0.09f);
            mat.SetFloat("_SwirlStrength", 1.6f);
            mat.SetFloat("_CrustAmount", 0.08f);
            mat.SetVector("_FlowDir", new Vector4(1f, 0.25f, 0f, 0f));
            mat.SetFloat("_FlowSpeed", 0.12f);
            mat.SetFloat("_ChurnSpeed", 0.2f);
            mat.SetFloat("_PulseSpeed", 1.3f);
            mat.SetFloat("_HeaveHeight", 0.03f);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Volcanic dusk: smoky plum zenith, ember-glow horizon, low red sun glow and slow ash-smoke clouds (user-preferred look).</summary>
        public static Material EnsureSkyMaterial()
        {
            var mat = EnsureMaterial(SkyPath, "MyGame/Sky/ToonSky");
            mat.SetColor("_ZenithColor", new Color(0.17f, 0.11f, 0.16f));
            mat.SetColor("_HorizonColor", new Color(0.74f, 0.40f, 0.30f));
            mat.SetFloat("_HorizonHeight", 0.18f);
            mat.SetFloat("_GradientPower", 0.8f);
            mat.SetColor("_SunColor", new Color(1f, 0.52f, 0.26f));
            mat.SetVector("_SunPos", new Vector4(0.3f, 0.3f, 0f, 0f));
            mat.SetFloat("_SunSize", 0.3f);
            mat.SetColor("_CloudColor", new Color(0.45f, 0.32f, 0.35f));
            mat.SetColor("_CloudShade", new Color(0.27f, 0.19f, 0.23f));
            mat.SetVector("_CloudScale", new Vector4(7f, 2.6f, 0f, 0f));
            mat.SetFloat("_CloudCover", 0.5f);
            mat.SetVector("_CloudBand", new Vector4(0.3f, 0.95f, 0f, 0f));
            mat.SetFloat("_CloudSpeed", 0.003f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material ParticleMaterial(string name, Color tint, bool additive)
        {
            var mat = EnsureMaterial($"{MatDir}/{name}.mat", "MyGame/Ambient/Particle");
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Softness", 0.7f);
            mat.SetFloat("_SrcBlend", additive ? 1f : 5f);  // One / SrcAlpha
            mat.SetFloat("_DstBlend", additive ? 1f : 10f); // One / OneMinusSrcAlpha
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Builds the two particle decoration prefabs; returns prefab name -> asset path.</summary>
        public static Dictionary<string, string> BuildFx()
        {
            var result = new Dictionary<string, string>();
            SceneFlowSetup.EnsureFolder(FxPrefabDir);
            result["EF_FX03_LavaBubbles"] = Save("EF_FX03_LavaBubbles", go => SetupBubbles(go.AddComponent<ParticleSystem>()));
            result["EF_FX10_EmberDrift"] = Save("EF_FX10_EmberDrift", go => SetupEmbers(go.AddComponent<ParticleSystem>()));
            return result;
        }

        private static string Save(string name, System.Action<GameObject> setup)
        {
            string path = $"{FxPrefabDir}/{name}.prefab";
            var go = new GameObject(name);
            try
            {
                setup(go);
                PrefabUtility.SaveAsPrefabAsset(go, path);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            return path;
        }

        private static void Common(ParticleSystem ps, int max)
        {
            var main = ps.main;
            main.loop = true;
            main.prewarm = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        /// <summary>FX03: hot swells that rise out of the lava, grow and pop (flat discs on the surface). Pivot = lava surface, 32 x 16 m patch.</summary>
        private static void SetupBubbles(ParticleSystem ps)
        {
            Common(ps, 80);
            var main = ps.main;
            main.duration = 4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.72f, 0.28f, 0.75f), new Color(1f, 0.5f, 0.12f, 0.6f));
            var em = ps.emission;
            em.rateOverTime = 9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 0.01f, 16f);
            shape.position = new Vector3(0f, 0.08f, 0f);
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0.15f), new Keyframe(0.7f, 1f), new Keyframe(0.85f, 1.15f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.15f), 0.8f), new GradientColorKey(new Color(1f, 0.85f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.25f), new GradientAlphaKey(0.7f, 0.85f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            r.sharedMaterial = ParticleMaterial("M_EF_FX_LavaBubble", new Color(1.3f, 0.9f, 0.6f, 1f), true);
        }

        /// <summary>Sparse embers drifting up from the lava with a little wander. Pivot = lava surface, 32 x 20 m patch.</summary>
        private static void SetupEmbers(ParticleSystem ps)
        {
            Common(ps, 220);
            var main = ps.main;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.13f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.4f, 1f), new Color(1f, 0.5f, 0.15f, 1f));
            var em = ps.emission;
            em.rateOverTime = 16f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(32f, 0.5f, 20f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            vel.y = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.45f;
            noise.frequency = 0.35f;
            noise.scrollSpeed = 0.2f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.85f, 0.45f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.12f), 0.6f), new GradientColorKey(new Color(0.6f, 0.2f, 0.12f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.1f), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = ParticleMaterial("M_EF_FX_Ember", new Color(1.6f, 1.1f, 0.7f, 1f), true);
        }
    }
}
