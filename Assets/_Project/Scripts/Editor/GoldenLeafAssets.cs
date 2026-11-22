using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MyGame.EditorTools
{
    /// <summary>
    /// Durable mesh assets shared by existing stages and map-tool rebuilds. The golden leaf is built procedurally:
    /// smooth Catmull-Rom outline (sharp base and tip), cupped and tip-curled blade, rounded rim, raised midrib and
    /// side veins that follow the surface, and a short stem. Submeshes: 0 blade, 1 rim, 2 veins/stem.
    /// </summary>
    public static class GoldenLeafAssets
    {
        public const string Folder = "Assets/_Project/Art/Environment/Common";
        public const string MeshPath = Folder + "/GoldenLeaf.asset";
        public const string HqModelPath = Folder + "/Models/GoldenLeaf_HQ.fbx";

        public static void Apply(GameObject visual)
        {
            // Blender model (ArtSource/Common/_build_scripts/build_pickups_hq.py): same silhouette and the same three
            // submeshes (blade, rim, veins/stem), far denser. The procedural mesh below is the fallback without it.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(HqModelPath);
            if (mesh != null && mesh.subMeshCount != 3) mesh = null;
            bool hq = mesh != null;
            if (hq) { }
            else if ((mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath)) == null) { mesh = BuildMesh(); AssetDatabase.CreateAsset(mesh, MeshPath); }
            else
            {
                // Rewrite in place (same asset + GUID). Mesh.Set* also refreshes the already-uploaded GPU copy, which CopySerialized did not.
                var updated = BuildMesh();
                mesh.Clear(); mesh.SetVertices(updated.vertices); mesh.subMeshCount = 3;
                for (int i = 0; i < 3; i++) mesh.SetTriangles(updated.GetTriangles(i), i);
                mesh.SetNormals(updated.normals); mesh.RecalculateBounds(); mesh.UploadMeshData(false);
                Object.DestroyImmediate(updated); EditorUtility.SetDirty(mesh);
            }
            var filter = visual.GetComponent<MeshFilter>(); if (filter == null) filter = visual.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = visual.GetComponent<MeshRenderer>(); if (renderer == null) renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { Material("M_GoldenLeaf", new Color(1, .84f, .16f), 2.1f), Material("M_GoldenLeafEdge", new Color(.95f, .58f, .06f), 1.1f), Material("M_GoldenLeafVeins", new Color(1, .97f, .62f), 1.8f) };
            visual.transform.localPosition = Vector3.zero;
            // the FBX mesh data is turned half a turn about Y relative to its node (axis conversion); undo it here
            visual.transform.localRotation = hq ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity; visual.transform.localScale = Vector3.one * 1.25f;
        }

        static Material Material(string name, Color color, float emission)
        {
            string path = Folder + "/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", .1f); mat.SetFloat("_Smoothness", .45f);
            mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.SetColor("_EmissionColor", new Color(color.r, color.g * .9f, color.b * .6f) * emission); EditorUtility.SetDirty(mat); return mat;
        }

        // ---------------------------------------------------------------- procedural mesh

        static readonly Vector2 Base = GoldenLeafShape.Outline[0];
        static readonly Vector2 Tip = GoldenLeafShape.Outline[4];
        static readonly Vector2 Stem = new Vector2(-.30f, -.49f);
        static Vector2[] outline;

        static float Thick(float f) => .03f + .04f * (1f - f * f);

        /// <summary>Shared macro shape of both faces: V-cup across the midrib plus a gentle tip curl.</summary>
        static float Macro(Vector2 q)
        {
            var axis = Tip - Base; float len = axis.magnitude; axis /= len;
            var perp = new Vector2(-axis.y, axis.x);
            float s = Mathf.Clamp01(Vector2.Dot(q - Base, axis) / len), c = Vector2.Dot(q - Base, perp);
            float sc = Mathf.Sqrt(c * c + .0004f);
            return .05f * Mathf.Pow(sc / .28f, 1.4f) + .07f * s * s * s;
        }

        /// <summary>Radial fraction (0 centre .. 1 outline) of a 2D point seen from the leaf centre.</summary>
        static float Radial(Vector2 q)
        {
            var c = GoldenLeafShape.Center; var d = q - c; if (d.sqrMagnitude < 1e-8f) return 0f;
            float best = float.MaxValue;
            for (int i = 0; i < outline.Length; i++)
            {
                var a = outline[i]; var e = outline[(i + 1) % outline.Length] - a;
                float den = d.x * e.y - d.y * e.x; if (Mathf.Abs(den) < 1e-9f) continue;
                var ac = a - c; float t = (ac.x * e.y - ac.y * e.x) / den, u = (ac.x * d.y - ac.y * d.x) / den;
                if (t > 0f && u >= 0f && u <= 1f && t < best) best = t;
            }
            return best == float.MaxValue ? 1f : Mathf.Clamp01(1f / best);
        }

        static float SurfaceZ(Vector2 q, int side) => Macro(q) + side * Thick(Radial(q));

        static Vector2[] SmoothOutline()
        {
            var p = GoldenLeafShape.Outline; var list = new List<Vector2>();
            Chain(new[] { p[0], p[1], p[2], p[3], p[4] }, 9, list);          // base -> tip, right side
            Chain(new[] { p[4], p[5], p[6], p[7], p[8], p[0] }, 7, list);    // tip -> base, left side
            return list.ToArray();
        }

        static void Chain(Vector2[] pts, int steps, List<Vector2> list)
        {
            for (int s = 0; s < pts.Length - 1; s++)
            {
                var p0 = pts[Mathf.Max(s - 1, 0)]; var p1 = pts[s]; var p2 = pts[s + 1]; var p3 = pts[Mathf.Min(s + 2, pts.Length - 1)];
                for (int j = 0; j < steps; j++)
                {
                    float t = j / (float)steps, t2 = t * t, t3 = t2 * t;
                    list.Add(.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3));
                }
            }
        }

        static void Tri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 want)
        {
            if (a == b || b == c || a == c) return;
            if (Vector3.Dot(Vector3.Cross(v[b] - v[a], v[c] - v[a]), want) < 0f) { int x = b; b = c; c = x; }
            t.Add(a); t.Add(b); t.Add(c);
        }

        static Mesh BuildMesh()
        {
            outline = SmoothOutline(); int n = outline.Length; var ctr = GoldenLeafShape.Center;
            var v = new List<Vector3>(); var sub = new[] { new List<int>(), new List<int>(), new List<int>() };

            // profile rows: top face (centre -> rim), rounded rim arc, bottom face (rim -> centre)
            var rowF = new List<float>(); var rowZ = new List<float>();
            float[] topF = { 0f, .2f, .4f, .58f, .72f, .83f, .9f, .92f };
            float e = Thick(.92f);
            foreach (var f in topF) { rowF.Add(f); rowZ.Add(Thick(f)); }
            for (int j = 1; j <= 5; j++) { float th = Mathf.PI * j / 6f; rowF.Add(.92f + .08f * Mathf.Sin(th)); rowZ.Add(e * Mathf.Cos(th)); }
            for (int k = topF.Length - 1; k >= 0; k--) { rowF.Add(topF[k]); rowZ.Add(-Thick(topF[k])); }
            int m = rowF.Count;
            var vi = new int[n, m];
            for (int k = 0; k < m; k++)
                for (int i = 0; i < n; i++)
                {
                    if ((k == 0 || k == m - 1) && i > 0) { vi[i, k] = vi[0, k]; continue; }
                    var q = ctr + (outline[i] - ctr) * rowF[k];
                    vi[i, k] = v.Count; v.Add(new Vector3(q.x, q.y, Macro(q) + rowZ[k]));
                }
            for (int k = 0; k < m - 1; k++)
            {
                bool arc = k >= 7 && k < 13; int smIdx = arc ? 1 : 0;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n; var rad = (outline[i] + outline[j]) * .5f - ctr; rad.Normalize();
                    Vector3 want;
                    if (arc) { float th = Mathf.PI * (k - 7 + .5f) / 6f; want = new Vector3(rad.x * Mathf.Sin(th), rad.y * Mathf.Sin(th), Mathf.Cos(th)); }
                    else want = new Vector3(0, 0, k < 7 ? 1f : -1f);
                    Tri(v, sub[smIdx], vi[i, k], vi[j, k], vi[j, k + 1], want);
                    Tri(v, sub[smIdx], vi[i, k], vi[j, k + 1], vi[i, k + 1], want);
                }
            }

            // midrib (both faces) and side veins (both faces)
            foreach (int side in new[] { 1, -1 })
            {
                Ribbon(v, sub[2], Line(Base, Tip, 28, 0f), .020f, .005f, .016f, .006f, side);
                var veins = GoldenLeafShape.Veins;
                for (int i = 6; i < veins.Length; i += 2)
                    Ribbon(v, sub[2], Line(veins[i], veins[i + 1], 14, .06f), .0085f, .0025f, .011f, .004f, side);
            }
            StemTube(v, sub[2]);

            var mesh = new Mesh { name = "GoldenLeaf" };
            mesh.SetVertices(v); mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(sub[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Slightly bowed polyline between two points (veins are not ruler-straight).</summary>
        static List<Vector2> Line(Vector2 a, Vector2 b, int segs, float bow)
        {
            var pts = new List<Vector2>(); var d = b - a; var perp = new Vector2(-d.y, d.x) * bow;
            for (int i = 0; i <= segs; i++) { float t = i / (float)segs; pts.Add(Vector2.Lerp(a, b, t) + perp * Mathf.Sin(t * Mathf.PI)); }
            return pts;
        }

        /// <summary>Three-vertex-wide ribbon with a raised centre, hugging the blade surface on the given side.</summary>
        static void Ribbon(List<Vector3> v, List<int> t, List<Vector2> pts, float w0, float w1, float lift0, float lift1, int side)
        {
            int start = v.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                float u = i / (float)(pts.Count - 1);
                var dir = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized; var perp = new Vector2(-dir.y, dir.x);
                float w = Mathf.Lerp(w0, w1, u), lift = Mathf.Lerp(lift0, lift1, u);
                var l = pts[i] + perp * w; var c = pts[i]; var r = pts[i] - perp * w;
                v.Add(new Vector3(l.x, l.y, SurfaceZ(l, side) + side * .002f));
                v.Add(new Vector3(c.x, c.y, SurfaceZ(c, side) + side * lift));
                v.Add(new Vector3(r.x, r.y, SurfaceZ(r, side) + side * .002f));
            }
            var want = new Vector3(0, 0, side);
            for (int i = 0; i < pts.Count - 1; i++)
            {
                int a = start + i * 3, b = a + 3;
                Tri(v, t, a, b, a + 1, want); Tri(v, t, a + 1, b, b + 1, want);
                Tri(v, t, a + 1, b + 1, a + 2, want); Tri(v, t, a + 2, b + 1, b + 2, want);
            }
        }

        /// <summary>Short curved stem: 8-sided tube from the base into the blade, closed with a rounded end.</summary>
        static void StemTube(List<Vector3> v, List<int> t)
        {
            const int rings = 8, sides = 8;
            int start = v.Count;
            var axis = (Base - Stem).normalized; var perp = new Vector2(-axis.y, axis.x);
            for (int r = 0; r <= rings; r++)
            {
                float u = r / (float)rings; var c = Vector2.Lerp(Stem, Base + axis * .04f, u) + perp * .015f * Mathf.Sin(u * Mathf.PI);
                float rad = Mathf.Lerp(.016f, .026f, u); float zc = Mathf.Lerp(-.02f, Macro(c) + .004f, u * u) + .0f;
                for (int s = 0; s < sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    v.Add(new Vector3(c.x + perp.x * Mathf.Cos(a) * rad, c.y + perp.y * Mathf.Cos(a) * rad, zc + Mathf.Sin(a) * rad));
                }
            }
            int cap = v.Count; v.Add(new Vector3(Stem.x - axis.x * .012f, Stem.y - axis.y * .012f, -.02f));
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < sides; s++)
                {
                    int s2 = (s + 1) % sides, a = start + r * sides + s, b = start + r * sides + s2, c = start + (r + 1) * sides + s, d = start + (r + 1) * sides + s2;
                    var mid = (v[a] + v[d]) * .5f; var ringCenter = (v[start + r * sides] + v[start + r * sides + sides / 2]) * .5f;
                    var want = mid - ringCenter;
                    Tri(v, t, a, b, d, want); Tri(v, t, a, d, c, want);
                }
            for (int s = 0; s < sides; s++)
            {
                int s2 = (s + 1) % sides; var want = v[cap] - v[start + s];
                Tri(v, t, start + s, start + s2, cap, want);
            }
        }
    }
}
