using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// One-shot golden burst for a newly mastered world-map node: flash, streaking sparks, a ground shock ring of
    /// sparkles and a rising glitter column. Built in code (no scene or prefab reference) with the additive
    /// MyGame/FX/GoldSparkle star shader used by the golden-leaf FX; unscaled time. Destroy the returned root when done.
    /// </summary>
    public static class MasterBurstFx
    {
        public const float Lifetime = 3.2f;
        private static Material material;

        /// <param name="scale">World size multiplier (1 = sized for a ~2.5 m node pad).</param>
        public static GameObject Spawn(Vector3 position, float scale = 1f)
        {
            var root = new GameObject("MasterBurstFx");
            root.transform.position = position;
            scale = Mathf.Max(0.05f, scale);

            // Big pop of light.
            var flash = NewSystem(root, "Flash", 0.3f);
            var fm = flash.main;
            fm.startLifetime = 0.45f; fm.startSpeed = 0f; fm.startSize = 4.5f * scale; fm.maxParticles = 2;
            fm.startColor = new Color(1f, 0.92f, 0.6f);
            Burst(flash, 1);
            Fade(flash, Color.white, Color.white, new Keyframe(0f, 0.3f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.8f));

            // Streaking sparks in every direction (stretched stars read as trails).
            var sparks = NewSystem(root, "Sparks", 0.4f);
            var sm = sparks.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.3f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(3.5f * scale, 8f * scale);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.22f * scale, 0.45f * scale);
            sm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.78f, 0.22f), new Color(1f, 0.97f, 0.8f));
            sm.gravityModifier = 0.6f;
            sm.maxParticles = 90;
            Burst(sparks, 70);
            var sshape = sparks.shape; sshape.enabled = true; sshape.shapeType = ParticleSystemShapeType.Hemisphere; sshape.radius = 0.25f * scale;
            sshape.rotation = new Vector3(-90f, 0f, 0f); // dome opening upward
            var limit = sparks.limitVelocityOverLifetime; limit.enabled = true; limit.dampen = 0.12f; limit.limit = 2.5f * scale;
            Fade(sparks, Color.white, new Color(1f, 0.62f, 0.15f), new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.09f; sr.lengthScale = 1.6f;

            // Ground shock ring of sparkles that sweeps across the pad.
            var ring = NewSystem(root, "Ring", 0.15f);
            var rm = ring.main;
            rm.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
            rm.startSpeed = new ParticleSystem.MinMaxCurve(3.2f * scale, 4f * scale);
            rm.startSize = new ParticleSystem.MinMaxCurve(0.3f * scale, 0.5f * scale);
            rm.startColor = new Color(1f, 0.85f, 0.35f);
            rm.maxParticles = 60;
            Burst(ring, 48);
            var rshape = ring.shape; rshape.enabled = true; rshape.shapeType = ParticleSystemShapeType.Circle; rshape.radius = 0.3f * scale;
            rshape.rotation = new Vector3(90f, 0f, 0f); rshape.radiusThickness = 0f;
            var rlimit = ring.limitVelocityOverLifetime; rlimit.enabled = true; rlimit.dampen = 0.2f; rlimit.limit = 1f * scale;
            Fade(ring, Color.white, new Color(1f, 0.7f, 0.2f), new Keyframe(0f, 1f), new Keyframe(1f, 0.2f));

            // Rising glitter column that lingers while the pad turns gold.
            var column = NewSystem(root, "Column", 1.1f);
            var cm = column.main;
            cm.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.9f);
            cm.startSpeed = new ParticleSystem.MinMaxCurve(1.2f * scale, 3f * scale);
            cm.startSize = new ParticleSystem.MinMaxCurve(0.16f * scale, 0.34f * scale);
            cm.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.3f), new Color(1f, 1f, 0.88f));
            cm.gravityModifier = -0.05f;
            cm.maxParticles = 80;
            var cem = column.emission; cem.rateOverTime = 55f;
            var cshape = column.shape; cshape.enabled = true; cshape.shapeType = ParticleSystemShapeType.Cone; cshape.angle = 12f;
            cshape.radius = 0.9f * scale; cshape.rotation = new Vector3(-90f, 0f, 0f);
            var csize = column.sizeOverLifetime; csize.enabled = true; // twinkle
            csize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0f), new Keyframe(0.2f, 1f), new Keyframe(0.45f, 0.35f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
            Fade(column, Color.white, new Color(1f, 0.75f, 0.25f), new Keyframe(0f, 1f), new Keyframe(1f, 1f));

            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>())
                ps.Play();
            return root;
        }

        private static ParticleSystem NewSystem(GameObject root, string name, float duration)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // duration can only be set while stopped
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = duration;
            main.useUnscaledTime = true; // the world map runs on unscaled time
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = Mat();
            pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.sortingOrder = 40;
            return ps;
        }

        private static void Burst(ParticleSystem ps, int count)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        /// <summary>Colour over lifetime start→end with alpha fading out, plus a size curve.</summary>
        private static void Fade(ParticleSystem ps, Color from, Color to, params Keyframe[] size)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            if (size == null || size.Length == 0) return;
            var sz = ps.sizeOverLifetime;
            if (sz.enabled) return; // keep a curve set by the caller
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(size));
        }

        private static Material Mat()
        {
            if (material != null) return material;
            var shader = Shader.Find("MyGame/FX/GoldSparkle");
            if (shader != null)
            {
                material = new Material(shader) { name = "MasterBurstSparkle", hideFlags = HideFlags.HideAndDontSave };
                material.SetColor("_Color", new Color(1.6f, 1.35f, 0.9f, 1f)); // HDR boost; hue comes from the particle colour
                return material;
            }
            // Fallback (shader stripped from a build): generated four-point star.
            material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit"))
                { name = "MasterBurstSparkle", hideFlags = HideFlags.HideAndDontSave };
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Sqrt(Mathf.Abs(u)) + Mathf.Sqrt(Mathf.Abs(v));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 3.5f)));
                }
            tex.Apply();
            material.mainTexture = tex;
            return material;
        }
    }
}
