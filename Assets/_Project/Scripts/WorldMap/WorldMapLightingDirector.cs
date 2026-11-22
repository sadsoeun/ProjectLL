using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyGame
{
    /// <summary>
    /// Region-based world map lighting. Every node belongs to a region preset (longest node-id prefix wins, so
    /// "W1_B" overrides "W1"). Each frame the two regions closest to the map avatar are mixed by their relative
    /// distance (pure preset on a node, 50/50 halfway between regions), the weights are eased over time, and the
    /// result drives the sun, ambient probe, fog, sky colour and one global Volume per preset.
    /// Volumes are layered by priority with stick-breaking weights, which gives an exact weighted average of the
    /// profiles (all profiles override the same parameters).
    /// </summary>
    [DefaultExecutionOrder(100)] // after MapPlayer / CameraFollow movement
    public sealed class WorldMapLightingDirector : MonoBehaviour
    {
        [Serializable]
        public struct Region
        {
            [Tooltip("Node ids starting with this prefix use the preset (e.g. \"W1\", or \"W1_B\" for the boss node).")]
            public string nodeIdPrefix;
            public WorldMapLightingPreset preset;
        }

        [SerializeField] private MapPlayer player;
        [SerializeField] private Light sun;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Region[] regions = new Region[0];

        [Header("Blending")]
        [Tooltip("Shape of the transition between two regions. X: 0 = on a node of the nearest region, 1 = halfway to the next region.")]
        [SerializeField] private AnimationCurve blendCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Seconds the weights take to settle (smooths region switches; 0 = instant).")]
        [SerializeField, Min(0f)] private float transitionTime = 0.35f;
        [Tooltip("An avatar jump longer than this (metres per frame) snaps the lighting instead of easing.")]
        [SerializeField, Min(0.1f)] private float snapDistance = 4f;
        [Tooltip("Priority of the lowest region volume; the others stack above it.")]
        [SerializeField] private float basePriority = 10f;

        /// <summary>A node (a == b) or a path segment between two nodes of the same region, on the XZ plane.</summary>
        private struct RegionSegment
        {
            public Vector2 a, b;
            public int preset;
        }

        private readonly List<WorldMapLightingPreset> presets = new List<WorldMapLightingPreset>();
        private readonly List<Volume> volumes = new List<Volume>();
        private RegionSegment[] segments = new RegionSegment[0];
        private float[] target = new float[0];
        private float[] weights = new float[0];
        private float[] minDist = new float[0];
        private bool initialized;
        private bool hasLastFocus;
        private Vector3 lastFocus;

        private bool saved;
        private bool oldFog;
        private FogMode oldFogMode;
        private Color oldFogColor;
        private float oldFogStart, oldFogEnd;
        private SphericalHarmonicsL2 oldProbe;
        private AmbientMode oldAmbientMode;

        private void Start() => Initialize();

        private void LateUpdate()
        {
            if (player != null)
                ApplyAt(player.transform.position, false);
        }

        /// <summary>
        /// Evaluates and applies the lighting for an avatar position. snap = skip easing (scene start, captures).
        /// Works in Edit Mode too (used by capture tools).
        /// </summary>
        public void ApplyAt(Vector3 focus, bool snap)
        {
            Initialize();
            if (presets.Count == 0)
                return;
            if (hasLastFocus && (focus - lastFocus).sqrMagnitude > snapDistance * snapDistance)
                snap = true;
            if (!hasLastFocus)
                snap = true;
            hasLastFocus = true;
            lastFocus = focus;

            ComputeTargetWeights(focus);
            float dt = Application.isPlaying ? Time.unscaledDeltaTime : 1f;
            float k = snap || transitionTime <= 0f ? 1f : 1f - Mathf.Exp(-dt * 4.6f / transitionTime); // ~99% in transitionTime
            float sum = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.Lerp(weights[i], target[i], k);
                if (weights[i] < 0.0005f)
                    weights[i] = 0f;
                sum += weights[i];
            }
            if (sum <= 0f)
            {
                Array.Copy(target, weights, weights.Length);
                sum = 1f;
            }
            for (int i = 0; i < weights.Length; i++)
                weights[i] /= sum;

            ApplyLighting(focus);
            ApplyVolumes();
        }

        /// <summary>Current normalized weight of each preset (for debugging / tools).</summary>
        public IReadOnlyList<WorldMapLightingPreset> Presets => presets;
        public float GetWeight(int presetIndex) => weights[presetIndex];

        private void Initialize()
        {
            if (initialized)
                return;
            initialized = true;
            if (targetCamera == null)
                targetCamera = Camera.main;

            presets.Clear();
            var nodes = FindObjectsByType<WorldMapNode>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var list = new List<RegionSegment>(nodes.Length * 8);
            var presetOfNode = new Dictionary<string, int>();
            foreach (var node in nodes)
            {
                if (node.Data == null)
                    continue;
                var preset = Resolve(node.Data.NodeId);
                if (preset == null)
                {
                    Debug.LogWarning($"[WorldMapLighting] No lighting region matches node '{node.Data.NodeId}'.");
                    continue;
                }
                int index = presets.IndexOf(preset);
                if (index < 0)
                {
                    index = presets.Count;
                    presets.Add(preset);
                }
                var p = node.transform.position;
                var xz = new Vector2(p.x, p.z);
                list.Add(new RegionSegment { a = xz, b = xz, preset = index });
                presetOfNode[node.Data.NodeId] = index;
            }
            // Paths inside one region belong to it, so walking between its nodes stays pure. Paths that join two
            // regions are left out: along them the avatar is measured to both end nodes and the mix follows its progress.
            foreach (var path in FindObjectsByType<WorldMapPath>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!presetOfNode.TryGetValue(path.FromNodeId, out int from) || !presetOfNode.TryGetValue(path.ToNodeId, out int to)
                    || from != to)
                    continue;
                var route = path.GetRoute(path.FromNodeId);
                for (int i = 1; i < route.Length; i++)
                {
                    list.Add(new RegionSegment
                    {
                        a = new Vector2(route[i - 1].x, route[i - 1].z),
                        b = new Vector2(route[i].x, route[i].z),
                        preset = from,
                    });
                }
            }
            segments = list.ToArray();
            target = new float[presets.Count];
            weights = new float[presets.Count];
            minDist = new float[presets.Count];
            BindVolumes();
            SaveRenderSettings();
        }

        private WorldMapLightingPreset Resolve(string nodeId)
        {
            WorldMapLightingPreset best = null;
            int bestLength = -1;
            foreach (var r in regions)
            {
                if (r.preset == null || string.IsNullOrEmpty(r.nodeIdPrefix))
                    continue;
                if (nodeId.StartsWith(r.nodeIdPrefix, StringComparison.Ordinal) && r.nodeIdPrefix.Length > bestLength)
                {
                    best = r.preset;
                    bestLength = r.nodeIdPrefix.Length;
                }
            }
            return best;
        }

        /// <summary>One global Volume per preset: reuses child volumes that already use the profile, adds missing ones.</summary>
        private void BindVolumes()
        {
            volumes.Clear();
            var existing = GetComponentsInChildren<Volume>(true);
            for (int i = 0; i < presets.Count; i++)
            {
                var profile = presets[i].profile;
                Volume v = null;
                if (profile != null)
                {
                    foreach (var e in existing)
                    {
                        if (e.sharedProfile == profile)
                        {
                            v = e;
                            break;
                        }
                    }
                    if (v == null)
                    {
                        var go = new GameObject("Volume_" + presets[i].name);
                        go.transform.SetParent(transform, false);
                        v = go.AddComponent<Volume>();
                        v.sharedProfile = profile;
                    }
                    v.isGlobal = true;
                    v.priority = basePriority + i;
                    v.weight = 0f;
                }
                volumes.Add(v);
            }
        }

        private void ComputeTargetWeights(Vector3 focus)
        {
            var f = new Vector2(focus.x, focus.z);
            for (int i = 0; i < minDist.Length; i++)
                minDist[i] = float.MaxValue;
            foreach (var s in segments)
            {
                float d = DistanceToSegment(f, s.a, s.b);
                if (d < minDist[s.preset])
                    minDist[s.preset] = d;
            }

            int a = -1, b = -1;
            for (int i = 0; i < minDist.Length; i++)
            {
                if (a < 0 || minDist[i] < minDist[a])
                {
                    b = a;
                    a = i;
                }
                else if (b < 0 || minDist[i] < minDist[b])
                    b = i;
            }

            Array.Clear(target, 0, target.Length);
            if (b < 0 || minDist[b] == float.MaxValue)
            {
                target[a] = 1f;
                return;
            }
            float d1 = minDist[a], d2 = minDist[b];
            float ratio = d1 + d2 > 0.0001f ? d1 / (d1 + d2) : 0.5f; // 0 on a node of A, 0.5 halfway
            float t = 0.5f * Mathf.Clamp01(blendCurve.Evaluate(Mathf.Clamp01(ratio * 2f)));
            target[a] = 1f - t;
            target[b] = t;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private void ApplyLighting(Vector3 focus)
        {
            // Accumulate from transparent black (Color.black has alpha 1 and would push the summed alpha above 1).
            Color sunColor = Color.clear, sky = Color.clear, fogColor = Color.clear;
            Color amSky = Color.clear, amEq = Color.clear, amGround = Color.clear;
            float intensity = 0f, shadow = 0f, fogStart = 0f, fogEnd = 0f;
            Vector3 dir = Vector3.zero;
            for (int i = 0; i < presets.Count; i++)
            {
                float w = weights[i];
                if (w <= 0f)
                    continue;
                var p = presets[i];
                sunColor += p.sunColor * w;
                intensity += p.sunIntensity * w;
                shadow += p.shadowStrength * w;
                dir += Quaternion.Euler(p.sunPitch, p.sunYaw, 0f) * Vector3.forward * w;
                amSky += p.ambientSky * w;
                amEq += p.ambientEquator * w;
                amGround += p.ambientGround * w;
                fogColor += p.fogColor * w;
                fogStart += p.fogStartBeyondAvatar * w;
                fogEnd += p.fogEndBeyondAvatar * w;
                sky += p.skyColor * w;
            }

            if (sun != null)
            {
                sun.color = sunColor;
                sun.intensity = intensity;
                sun.shadowStrength = shadow;
                if (dir.sqrMagnitude > 0.0001f)
                    sun.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = amSky;
            RenderSettings.ambientEquatorColor = amEq;
            RenderSettings.ambientGroundColor = amGround;
            RenderSettings.ambientProbe = GradientProbe(amSky, amEq, amGround);

            float camDist = targetCamera != null ? Vector3.Distance(targetCamera.transform.position, focus) : 20f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = camDist + fogStart;
            RenderSettings.fogEndDistance = Mathf.Max(camDist + fogStart + 1f, camDist + fogEnd);

            if (targetCamera != null)
                targetCamera.backgroundColor = sky;
        }

        private void ApplyVolumes()
        {
            // Stick-breaking: volume i lerps the stack toward its profile by w_i / (w_0 + .. + w_i), in priority order.
            float cumulative = 0f;
            for (int i = 0; i < volumes.Count; i++)
            {
                cumulative += weights[i];
                var v = volumes[i];
                if (v == null)
                    continue;
                v.weight = cumulative > 0f ? weights[i] / cumulative : 0f;
            }
        }

        // ---- Ambient probe from a sky / horizon / ground gradient ----

        private static bool basisReady;
        private static SphericalHarmonicsL2 basisAmbient, basisUp, basisDown;
        private static float[,] basisInverse;

        /// <summary>
        /// SH probe whose irradiance equals the given colours straight up, at the horizon and straight down.
        /// Built from an ambient term plus up / down lobes, solved per channel (the probe is what the toon shaders sample).
        /// </summary>
        private static SphericalHarmonicsL2 GradientProbe(Color sky, Color equator, Color ground)
        {
            EnsureBasis();
            var result = new SphericalHarmonicsL2();
            for (int c = 0; c < 3; c++)
            {
                float s = sky[c], e = equator[c], g = ground[c];
                float x0 = basisInverse[0, 0] * s + basisInverse[0, 1] * e + basisInverse[0, 2] * g;
                float x1 = basisInverse[1, 0] * s + basisInverse[1, 1] * e + basisInverse[1, 2] * g;
                float x2 = basisInverse[2, 0] * s + basisInverse[2, 1] * e + basisInverse[2, 2] * g;
                for (int k = 0; k < 9; k++)
                    result[c, k] = basisAmbient[0, k] * x0 + basisUp[0, k] * x1 + basisDown[0, k] * x2;
            }
            return result;
        }

        private static void EnsureBasis()
        {
            if (basisReady)
                return;
            basisAmbient = new SphericalHarmonicsL2();
            basisAmbient.AddAmbientLight(Color.white);
            basisUp = new SphericalHarmonicsL2();
            basisUp.AddDirectionalLight(Vector3.up, Color.white, 1f);
            basisDown = new SphericalHarmonicsL2();
            basisDown.AddDirectionalLight(Vector3.down, Color.white, 1f);

            var dirs = new[] { Vector3.up, Vector3.right, Vector3.down };
            var m = new float[3, 3];
            var tmp = new Color[3];
            var basis = new[] { basisAmbient, basisUp, basisDown };
            for (int col = 0; col < 3; col++)
            {
                basis[col].Evaluate(dirs, tmp);
                for (int row = 0; row < 3; row++)
                    m[row, col] = tmp[row].r;
            }
            basisInverse = Invert3(m);
            basisReady = true;
        }

        private static float[,] Invert3(float[,] m)
        {
            float a = m[0, 0], b = m[0, 1], c = m[0, 2];
            float d = m[1, 0], e = m[1, 1], f = m[1, 2];
            float g = m[2, 0], h = m[2, 1], i = m[2, 2];
            float A = e * i - f * h, B = -(d * i - f * g), C = d * h - e * g;
            float det = a * A + b * B + c * C;
            if (Mathf.Abs(det) < 1e-8f)
                throw new InvalidOperationException("World map ambient basis is singular.");
            float inv = 1f / det;
            return new[,]
            {
                { A * inv, -(b * i - c * h) * inv, (b * f - c * e) * inv },
                { B * inv, (a * i - c * g) * inv, -(a * f - c * d) * inv },
                { C * inv, -(a * h - b * g) * inv, (a * e - b * d) * inv },
            };
        }

        // ---- Restore (the next scene normally replaces these anyway) ----

        private void SaveRenderSettings()
        {
            if (saved || !Application.isPlaying)
                return;
            saved = true;
            oldFog = RenderSettings.fog;
            oldFogMode = RenderSettings.fogMode;
            oldFogColor = RenderSettings.fogColor;
            oldFogStart = RenderSettings.fogStartDistance;
            oldFogEnd = RenderSettings.fogEndDistance;
            oldProbe = RenderSettings.ambientProbe;
            oldAmbientMode = RenderSettings.ambientMode;
        }

        private void OnDestroy()
        {
            if (!saved)
                return;
            RenderSettings.fog = oldFog;
            RenderSettings.fogMode = oldFogMode;
            RenderSettings.fogColor = oldFogColor;
            RenderSettings.fogStartDistance = oldFogStart;
            RenderSettings.fogEndDistance = oldFogEnd;
            RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientProbe = oldProbe;
        }
    }
}
