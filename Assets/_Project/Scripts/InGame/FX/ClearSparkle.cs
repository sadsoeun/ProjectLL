using UnityEngine;

namespace MyGame
{
    /// <summary>One-shot burst of little star sparkles (stage-clear V sign). Self-destroys; uses a generated star texture.</summary>
    public static class ClearSparkle
    {
        private static Material material;

        public static void Spawn(Vector3 position)
        {
            var go = new GameObject("ClearSparkle");
            go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // AddComponent starts it; durations can only be set while stopped
            var main = ps.main;
            main.playOnAwake = false;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.3f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.95f, 0.6f), new Color(1f, 1f, 1f));
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 7), new ParticleSystem.Burst(0.18f, 5) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.25f, 1f), new Keyframe(1f, 0f)));
            var rot = ps.rotationOverLifetime;
            rot.enabled = true;
            rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Mat();
            renderer.sortingOrder = 50;
            ps.Play();
        }

        private static Material Mat()
        {
            if (material != null)
                return material;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            const int n = 48;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    // four-point star: |u|^0.5 + |v|^0.5 < 1
                    float d = Mathf.Sqrt(Mathf.Abs(u)) + Mathf.Sqrt(Mathf.Abs(v));
                    float a = Mathf.Clamp01((1f - d) * 3.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            material.mainTexture = tex;
            return material;
        }
    }
}
