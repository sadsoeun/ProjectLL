using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame
{
    /// <summary>
    /// "Pop" of a defeated boss: a white flash, smoke puffs thrown outward and a few stars that arc away, played where the
    /// body was when it vanished. Generated at runtime like BossStunFx (procedural meshes + the shared star material), so
    /// every boss gets it without an extra prefab; the look is tuned in BossData. Destroys itself when it is over.
    /// </summary>
    public sealed class BossDefeatBurst : MonoBehaviour
    {
        private const float FlashPart = 0.3f; // share of the lifetime the flash is visible
        private const float StarGravity = 14f;
        private const float PuffDrag = 3.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh discMesh, starMesh;

        private struct Piece
        {
            public Transform transform;
            public Vector3 velocity;
            public float size;
            public float spin;
            public bool star;
        }

        private Piece[] pieces;
        private Transform flash;
        private MeshRenderer flashRenderer;
        private MaterialPropertyBlock block;
        private Color flashColor;
        private float flashSize;
        private float lifetime;
        private float age;

        /// <summary>Plays the burst at <paramref name="center"/>; <paramref name="radius"/> is about half the size of the body.</summary>
        public static BossDefeatBurst Spawn(Transform parent, Vector3 center, float radius, BossData data)
        {
            if (data == null || data.stunStarMaterial == null)
                return null;
            var go = new GameObject("BossDefeatBurst");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var fx = go.AddComponent<BossDefeatBurst>();
            fx.Init(Mathf.Max(0.5f, radius), data);
            return fx;
        }

        private void Init(float radius, BossData data)
        {
            if (discMesh == null)
                discMesh = BuildFan("BossBurstDisc", 24, 0.5f, 0.5f);
            if (starMesh == null)
                starMesh = BuildFan("BossBurstStar", 10, 0.5f, 0.22f);
            block = new MaterialPropertyBlock();
            lifetime = Mathf.Max(0.2f, data.defeatPopSeconds);
            flashColor = Color.white;
            flashSize = radius * 3.2f;

            int puffs = Mathf.Max(0, data.defeatPopPuffs);
            int stars = Mathf.Max(0, data.defeatPopStars);
            pieces = new Piece[puffs + stars];
            for (int i = 0; i < pieces.Length; i++)
            {
                bool star = i >= puffs;
                // Spread evenly around the body with a little jitter; the side camera sees the XY plane.
                int index = star ? i - puffs : i;
                int count = star ? stars : puffs;
                float a = (index + Random.Range(-0.3f, 0.3f)) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                float speed = radius * (star ? Random.Range(4.5f, 6.5f) : Random.Range(2.2f, 4.2f)) / lifetime * 0.8f;
                var t = NewPiece(star ? "Star" : "Puff", star ? starMesh : discMesh, data.stunStarMaterial,
                    star ? data.stunStarColor : data.defeatPopColor);
                // Puffs start inside the body, in front of it; stars start a little further out and behind the smoke.
                t.localPosition = dir * (radius * (star ? 0.5f : Random.Range(0.1f, 0.6f)))
                    + Vector3.forward * (star ? 0.2f : Random.Range(-0.6f, -0.1f));
                pieces[i] = new Piece
                {
                    transform = t,
                    velocity = dir * speed + (star ? Vector3.up * (radius * 2f) : Vector3.zero),
                    size = radius * (star ? Random.Range(0.35f, 0.55f) : Random.Range(0.9f, 1.5f)),
                    spin = star ? Random.Range(-540f, 540f) : 0f,
                    star = star,
                };
            }

            flash = NewPiece("Flash", discMesh, data.stunStarMaterial, flashColor);
            flash.localPosition = Vector3.forward * -0.8f;
            flashRenderer = flash.GetComponent<MeshRenderer>();
            Apply(0f);
        }

        private Transform NewPiece(string pieceName, Mesh mesh, Material material, Color color)
        {
            var go = new GameObject(pieceName);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            block.SetColor(BaseColorId, color);
            mr.SetPropertyBlock(block);
            return go.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                if (p.star)
                    p.velocity += Vector3.down * (StarGravity * dt);
                else
                    p.velocity *= Mathf.Exp(-PuffDrag * dt);
                p.transform.localPosition += p.velocity * dt;
                pieces[i] = p;
            }
            Apply(age / lifetime);
        }

        private void Apply(float k)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                var p = pieces[i];
                float s;
                if (p.star)
                {
                    s = 1f - k * k;
                    p.transform.localRotation = Quaternion.Euler(0f, 0f, p.spin * age);
                }
                else
                    s = k < 0.2f ? Mathf.Lerp(0.35f, 1f, EaseOut(k / 0.2f)) : Mathf.Lerp(1f, 0f, EaseIn((k - 0.2f) / 0.8f));
                p.transform.localScale = Vector3.one * Mathf.Max(0.001f, p.size * s);
            }

            float f = k / FlashPart;
            bool flashOn = f < 1f;
            if (flashRenderer.enabled != flashOn)
                flashRenderer.enabled = flashOn;
            if (flashOn)
            {
                flash.localScale = Vector3.one * Mathf.Lerp(flashSize * 0.35f, flashSize, EaseOut(f));
                var c = flashColor;
                c.a = 1f - f;
                block.SetColor(BaseColorId, c);
                flashRenderer.SetPropertyBlock(block);
            }
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
        private static float EaseIn(float x) => x * x;

        /// <summary>Flat fan in the XY plane facing -Z (the side camera): a disc when both radii match, else a star. Double-sided via the material.</summary>
        private static Mesh BuildFan(string meshName, int rim, float outer, float inner)
        {
            var verts = new Vector3[rim + 1];
            var colors = new Color[verts.Length];
            var tris = new int[rim * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < rim; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / rim;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            for (int i = 0; i < verts.Length; i++)
                colors[i] = Color.white;
            for (int i = 0; i < rim; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = 1 + (i + 1) % rim;
                tris[i * 3 + 2] = 1 + i;
            }
            var mesh = new Mesh { name = meshName };
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
