using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Background seagulls for coast stages: a few simple "V" silhouettes gliding far behind the level.
    /// Purely decorative (no colliders, no shadows); built at runtime from one small procedural mesh and kept around
    /// the camera by wrapping along X, so a handful of birds covers the whole stage.
    /// </summary>
    public sealed class AmbientSeagulls : MonoBehaviour
    {
        [Min(0)] public int count = 5;
        [Tooltip("Distance behind the play lane (along the camera's view direction), min / max.")]
        public Vector2 depthRange = new Vector2(40f, 75f);
        [Tooltip("Height above the camera, min / max.")]
        public Vector2 heightRange = new Vector2(5f, 15f);
        [Tooltip("Flight speed in m/s, min / max.")]
        public Vector2 speedRange = new Vector2(1.5f, 3.5f);
        [Tooltip("Birds wrap around when they are this far from the camera along X.")]
        public float wrapHalfWidth = 60f;
        [Tooltip("Wing span in metres.")]
        public float wingSpan = 3f;
        [Tooltip("Wing beats per second while flapping.")]
        public float flapRate = 1.6f;
        [Tooltip("Flap angle in degrees.")]
        public float flapAngle = 32f;
        public Color bodyColor = new Color(0.97f, 0.98f, 1f, 1f);
        public Color tipColor = new Color(0.55f, 0.6f, 0.68f, 1f);

        private struct Gull
        {
            public Transform root, left, right;
            public float speed, height, depth, phase, bob;
        }

        private Gull[] gulls;
        private Mesh mesh;
        private Material material;
        private Transform cam;

        private void Start()
        {
            var main = Camera.main;
            if (main == null || count <= 0)
            {
                enabled = false;
                return;
            }
            cam = main.transform;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                enabled = false;
                return;
            }
            material = new Material(shader) { name = "AmbientSeagull" };
            mesh = BuildWingMesh();

            gulls = new Gull[count];
            for (int i = 0; i < count; i++)
            {
                var g = new Gull
                {
                    root = new GameObject("Seagull_" + i).transform,
                    speed = Random.Range(speedRange.x, speedRange.y) * (Random.value < 0.5f ? -1f : 1f),
                    height = Random.Range(heightRange.x, heightRange.y),
                    depth = Random.Range(depthRange.x, depthRange.y),
                    phase = Random.value * 20f,
                    bob = Random.Range(0.3f, 0.8f),
                };
                g.root.SetParent(transform, false);
                g.left = CreateWing(g.root, "WingL");
                g.right = CreateWing(g.root, "WingR");
                float scale = wingSpan * 0.5f * Random.Range(0.8f, 1.15f);
                g.root.localScale = new Vector3(scale, scale, scale);
                float side = cam.forward.z >= 0f ? 1f : -1f;
                g.root.position = new Vector3(cam.position.x + Random.Range(-wrapHalfWidth, wrapHalfWidth),
                    cam.position.y + g.height, cam.position.z + side * g.depth);
                gulls[i] = g;
            }
        }

        private Transform CreateWing(Transform parent, string wingName)
        {
            var go = new GameObject(wingName);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            return go.transform;
        }

        // One wing pointing along +X (length 1): wide at the body, bent and narrow at the tip.
        private Mesh BuildWingMesh()
        {
            var m = new Mesh { name = "SeagullWing" };
            m.vertices = new[]
            {
                new Vector3(0f, 0.09f, 0f), new Vector3(0f, -0.09f, 0f),
                new Vector3(0.55f, 0.1f, 0f), new Vector3(0.5f, -0.04f, 0f),
                new Vector3(1f, -0.08f, 0f),
            };
            m.colors = new[] { bodyColor, bodyColor, bodyColor, bodyColor, tipColor };
            m.triangles = new[] { 0, 2, 1, 1, 2, 3, 2, 4, 3 };
            m.RecalculateBounds();
            return m;
        }

        private void Update()
        {
            if (gulls == null || cam == null)
                return;
            float t = Time.time;
            float dt = Time.deltaTime;
            float side = cam.forward.z >= 0f ? 1f : -1f;
            for (int i = 0; i < gulls.Length; i++)
            {
                var g = gulls[i];
                Vector3 p = g.root.position;
                p.x += g.speed * dt;
                float dx = p.x - cam.position.x;
                if (dx > wrapHalfWidth) p.x -= wrapHalfWidth * 2f;
                else if (dx < -wrapHalfWidth) p.x += wrapHalfWidth * 2f;
                p.y = cam.position.y + g.height + Mathf.Sin(t * 0.5f + g.phase) * g.bob;
                p.z = cam.position.z + side * g.depth;
                g.root.position = p;

                // Flap in bursts, glide in between.
                float burst = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.1f, 0.5f, Mathf.Sin(t * 0.35f + g.phase * 1.7f)));
                float angle = 10f + Mathf.Sin(t * flapRate * Mathf.PI * 2f + g.phase) * flapAngle * (0.25f + 0.75f * burst);
                g.right.localRotation = Quaternion.Euler(0f, 0f, angle);
                g.left.localRotation = Quaternion.Euler(0f, 0f, 180f - angle);
            }
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
            if (material != null) Destroy(material);
        }
    }
}
