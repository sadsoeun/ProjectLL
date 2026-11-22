using System.IO;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Idempotent builder for the golden-leaf glow: camera-facing procedural glow quad, sparkle particles and the
    /// collect-burst prefab. Called by StageLayoutBuilder.MakeSpecial and by the menu (applies to the existing prefab).
    /// </summary>
    public static class SpecialCollectibleFx
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Level/SpecialCollectible.prefab";
        public const string BurstPrefabPath = "Assets/_Project/Prefabs/FX/SpecialCollectBurst.prefab";
        const string MatDir = "Assets/_Project/Art/Materials/FX";
        const string GlowName = "GlowBillboard";
        const string SparkleName = "Sparkles";

        [MenuItem("MyGame/Art/Apply Special Collectible FX")]
        public static void ApplyToPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds (or rebuilds) the glow children on the pickup root and wires the burst prefab. Safe to rerun.</summary>
        public static void Apply(GameObject root)
        {
            foreach (var n in new[] { GlowName, SparkleName })
            {
                var old = root.transform.Find(n);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var glowMat = GlowMaterial();
            var sparkMat = SparkleMaterial();
            var burst = EnsureBurstPrefab(sparkMat);

            // Glow quad lives under the root (not Spin) and billboards in the shader.
            var glow = new GameObject(GlowName);
            glow.transform.SetParent(root.transform, false);
            glow.transform.localScale = Vector3.one * 5f; // ~2.5 m radius, shader fades out before the border
            glow.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var gr = glow.AddComponent<MeshRenderer>();
            gr.sharedMaterial = glowMat;
            gr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            gr.receiveShadows = false;
            gr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            gr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // Sparkle dust: local space so it follows the pickup, 8-ish alive at once.
            var sp = new GameObject(SparkleName);
            sp.transform.SetParent(root.transform, false);
            var ps = sp.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 2f;
            main.prewarm = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 12;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.35f), new Color(1f, 1f, 0.85f));
            main.gravityModifier = -0.03f;
            var em = ps.emission;
            em.rateOverTime = 7f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.9f;
            var size = ps.sizeOverLifetime;
            size.enabled = true; // twinkle: up, dip, up, out
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.45f, 0.35f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);
            var pr = sp.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = sparkMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            ps.Play();

            var comp = root.GetComponent<SpecialCollectible>();
            if (comp != null)
                EditorWire.Set(comp, "collectFxPrefab", burst);
        }

        static GameObject EnsureBurstPrefab(Material sparkMat)
        {
            Directory.CreateDirectory("Assets/_Project/Prefabs/FX");
            var go = new GameObject("SpecialCollectBurst");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.duration = 0.5f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.8f, 0.25f), new Color(1f, 1f, 0.85f));
            main.gravityModifier = 0.35f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.maxParticles = 40;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.7f, 0.2f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f)));
            var vel = ps.limitVelocityOverLifetime;
            vel.enabled = true;
            vel.dampen = 0.15f;
            vel.limit = 3f;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = sparkMat;
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;

            // Flash: one big star that pops and fades.
            var flash = new GameObject("Flash");
            flash.transform.SetParent(go.transform, false);
            var fps = flash.AddComponent<ParticleSystem>();
            fps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var fm = fps.main;
            fm.playOnAwake = true;
            fm.loop = false;
            fm.duration = 0.3f;
            fm.simulationSpace = ParticleSystemSimulationSpace.World;
            fm.startLifetime = 0.35f;
            fm.startSpeed = 0f;
            fm.startSize = 3.2f;
            fm.startRotation = 0f;
            fm.startColor = new Color(1f, 0.9f, 0.5f);
            fm.maxParticles = 2;
            var fe = fps.emission;
            fe.rateOverTime = 0f;
            fe.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            var fsh = fps.shape;
            fsh.enabled = false;
            var fsz = fps.sizeOverLifetime;
            fsz.enabled = true;
            fsz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0.7f)));
            var fcol = fps.colorOverLifetime;
            fcol.enabled = true;
            var fg = new Gradient();
            fg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                       new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fcol.color = fg;
            var fr = flash.GetComponent<ParticleSystemRenderer>();
            fr.sharedMaterial = sparkMat;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fr.receiveShadows = false;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, BurstPrefabPath);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Material GlowMaterial()
        {
            var m = LoadOrCreate("M_GoldGlow", "MyGame/FX/GoldGlow");
            m.SetColor("_Color", new Color(1.0f, 0.70f, 0.18f, 1f));
            m.SetColor("_CoreColor", new Color(1.0f, 0.97f, 0.82f, 1f));
            m.SetFloat("_Intensity", 1.3f);
            m.SetFloat("_ViewOffset", -0.12f); // slightly behind the leaf so the leaf silhouette stays readable in front of the light
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SparkleMaterial()
        {
            var m = LoadOrCreate("M_GoldSparkle", "MyGame/FX/GoldSparkle");
            m.SetColor("_Color", new Color(1.0f, 0.82f, 0.30f, 1f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material LoadOrCreate(string name, string shaderName)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new System.InvalidOperationException("Shader not found: " + shaderName);
            if (m == null) { m = new Material(shader) { name = name }; AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            return m;
        }
    }
}
