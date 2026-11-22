using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame
{
    /// <summary>
    /// "Dizzy" feedback above a stunned boss: a few stars orbit the head, each leaving a fading trail (afterimage).
    /// Generated at runtime (procedural star mesh + TrailRenderer) so no extra prefab is needed; the look is tuned in BossData.
    /// The orbit is drawn as a tilted ellipse so the side camera reads it as a ring around the head, not a line.
    /// </summary>
    public sealed class BossStunFx : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh starMesh;

        private Transform anchor;
        private float fallbackHeight;
        private BossData data;
        private Transform[] stars;
        private TrailRenderer[] trails;
        private float angle;
        private bool visible;

        public static BossStunFx Create(Transform owner, Transform anchor, BossData data)
        {
            var go = new GameObject("StunStars");
            go.transform.SetParent(owner, false);
            var fx = go.AddComponent<BossStunFx>();
            fx.Init(anchor, data);
            return fx;
        }

        private void Init(Transform headAnchor, BossData bossData)
        {
            anchor = headAnchor;
            data = bossData;
            fallbackHeight = bossData.bodyHeight;
            if (starMesh == null)
                starMesh = BuildStarMesh(0.5f, 0.22f);

            int count = Mathf.Max(1, data.stunStarCount);
            stars = new Transform[count];
            trails = new TrailRenderer[count];
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(data.stunStarColor, 0f), new GradientColorKey(data.stunStarColor, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) });

            for (int i = 0; i < count; i++)
            {
                var star = new GameObject($"Star{i}");
                star.transform.SetParent(transform, false);
                star.transform.localScale = Vector3.one * data.stunStarSize;
                star.AddComponent<MeshFilter>().sharedMesh = starMesh;
                var mr = star.AddComponent<MeshRenderer>();
                mr.sharedMaterial = data.stunStarMaterial;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                var block = new MaterialPropertyBlock();
                block.SetColor(BaseColorId, data.stunStarColor); // the trail gets its colour from the gradient instead
                mr.SetPropertyBlock(block);

                var trail = star.AddComponent<TrailRenderer>();
                trail.sharedMaterial = data.stunStarMaterial;
                trail.time = data.stunTrailSeconds;
                trail.minVertexDistance = 0.03f;
                trail.widthMultiplier = data.stunStarSize * 0.55f;
                trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                trail.colorGradient = gradient;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                trail.emitting = false;

                stars[i] = star.transform;
                trails[i] = trail;
            }
            visible = true; // force the hide below (SetVisible skips a call that matches the current flag)
            SetVisible(false);
        }

        public void SetVisible(bool on)
        {
            if (on == visible)
                return;
            visible = on;
            if (on)
                Place(); // move to the head before the trails start, so no streak from the old position
            for (int i = 0; i < stars.Length; i++)
            {
                stars[i].gameObject.SetActive(on);
                trails[i].Clear();
                trails[i].emitting = on;
            }
        }

        private void LateUpdate()
        {
            if (!visible)
                return;
            angle += data.stunStarSpinDegrees * Time.deltaTime;
            Place();
        }

        private void Place()
        {
            Vector3 center = anchor != null
                ? anchor.position + Vector3.up * data.stunStarHeight
                : transform.parent.position + Vector3.up * (fallbackHeight + data.stunStarHeight);
            float step = 360f / stars.Length;
            for (int i = 0; i < stars.Length; i++)
            {
                float a = (angle + step * i) * Mathf.Deg2Rad;
                // Ellipse around the head: wide on X, shallow on Z, tilted on Y so the near side dips toward the camera.
                var offset = new Vector3(Mathf.Cos(a) * data.stunStarRadius,
                                         -Mathf.Sin(a) * data.stunStarRadius * 0.28f,
                                         Mathf.Sin(a) * data.stunStarRadius * 0.5f);
                stars[i].position = center + offset;
                // Stars face the side camera (mesh lies in XY) and twirl on their own axis.
                stars[i].rotation = Quaternion.Euler(0f, 0f, angle * 1.5f + i * 40f);
            }
        }

        /// <summary>Flat five-pointed star in the XY plane, facing -Z (the side camera). Double-sided via the material.</summary>
        private static Mesh BuildStarMesh(float outer, float inner)
        {
            const int points = 5;
            var verts = new Vector3[points * 2 + 1];
            var colors = new Color[verts.Length];
            var tris = new int[points * 2 * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < points * 2; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = Mathf.PI * 0.5f + i * Mathf.PI / points;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
            }
            for (int i = 0; i < verts.Length; i++)
                colors[i] = Color.white;
            for (int i = 0; i < points * 2; i++)
            {
                tris[i * 3] = 0;
                tris[i * 3 + 1] = 1 + (i + 1) % (points * 2);
                tris[i * 3 + 2] = 1 + i;
            }
            var mesh = new Mesh { name = "BossStunStar" };
            mesh.vertices = verts;
            mesh.colors = colors;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
