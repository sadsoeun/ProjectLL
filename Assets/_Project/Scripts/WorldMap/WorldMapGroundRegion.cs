using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Tells which world's terrain the map avatar is standing on, so world-specific feedback (BGM) follows the
    /// visible landscape instead of the nearest stage node. Region terrain colliders are named "Terrain_R0N_Walk"
    /// (art v2+); region N maps to the node-id prefix "WN". Bridges and sea crossings have no region terrain, so
    /// the last region is kept there.
    /// </summary>
    public sealed class WorldMapGroundRegion : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Collider names matching this pattern are region terrain; group 1 is the region number.")]
        [SerializeField] private string terrainNamePattern = @"^Terrain_R0*(\d+)_Walk$";
        [Tooltip("Node-id prefix format for a region number ({0} = number).")]
        [SerializeField] private string prefixFormat = "W{0}";
        [SerializeField, Min(0.1f)] private float probeHeight = 4f;
        [SerializeField, Min(0.1f)] private float probeDepth = 12f;
        [SerializeField, Min(0f)] private float probeInterval = 0.1f;
        [Tooltip("Seconds a new region must stay under the avatar before it counts (avoids flicker on seams).")]
        [SerializeField, Min(0f)] private float settleSeconds = 0.25f;

        private readonly Dictionary<Collider, string> regionOf = new Dictionary<Collider, string>();
        private readonly RaycastHit[] hits = new RaycastHit[16];
        private float nextProbeAt;
        private string candidate;
        private float candidateSince;

        /// <summary>Node-id prefix of the region under the avatar ("W1".."W5"), or null before any region was found.</summary>
        public string CurrentPrefix { get; private set; }
        public event Action<string> RegionChanged;

        private void Awake()
        {
            var regex = new Regex(terrainNamePattern);
            foreach (var c in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var m = regex.Match(c.name);
                if (m.Success)
                    regionOf[c] = string.Format(prefixFormat, int.Parse(m.Groups[1].Value));
            }
            if (regionOf.Count == 0)
                Debug.LogWarning("[WorldMapGroundRegion] No region terrain colliders found; region feedback falls back to nodes.");
        }

        public void SetTarget(Transform t) => target = t;

        /// <summary>Probes immediately and accepts the result without settling (scene start, teleports).</summary>
        public void Snap()
        {
            Physics.SyncTransforms();
            string found = Probe();
            candidate = found;
            if (found != null && found != CurrentPrefix)
            {
                CurrentPrefix = found;
                RegionChanged?.Invoke(found);
            }
        }

        private void Update()
        {
            if (target == null || regionOf.Count == 0 || Time.unscaledTime < nextProbeAt)
                return;
            nextProbeAt = Time.unscaledTime + probeInterval;

            string found = Probe();
            if (found == null || found == CurrentPrefix)
            {
                candidate = found;
                return;
            }
            if (found != candidate)
            {
                candidate = found;
                candidateSince = Time.unscaledTime;
            }
            if (Time.unscaledTime - candidateSince >= settleSeconds)
            {
                CurrentPrefix = found;
                RegionChanged?.Invoke(found);
            }
        }

        /// <summary>Region of the highest region-terrain surface under the avatar, or null (bridge, sea).</summary>
        private string Probe()
        {
            if (target == null)
                return null;
            Vector3 origin = target.position + Vector3.up * probeHeight;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, probeHeight + probeDepth, ~0, QueryTriggerInteraction.Ignore);
            string best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hits[i].distance < bestDistance && regionOf.TryGetValue(hits[i].collider, out var prefix))
                {
                    best = prefix;
                    bestDistance = hits[i].distance;
                }
            }
            return best;
        }
    }
}
