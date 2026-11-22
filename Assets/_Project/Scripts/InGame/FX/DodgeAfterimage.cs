using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Dodge afterimages: while <see cref="PlayerMotor.IsDodging"/>, snapshots of the character's pose (baked skinned meshes)
    /// are left behind at a fixed interval and fade out. Ghosts live in world space so they stay where they were dropped.
    /// Runs on unscaled time, so it also works during a perfect-dodge <see cref="TimeStop"/>. Pooled: no per-dodge allocation
    /// after the first few dodges.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public sealed class DodgeAfterimage : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Afterimage material (MyGame/FX/Afterimage). Falls back to finding the shader when empty.")]
        [SerializeField] private Material ghostMaterial;
        [Tooltip("Ghost colour; alpha = starting opacity.")]
        [SerializeField] private Color tint = new Color(0.3f, 0.78f, 1f, 0.85f);
        [Tooltip("Seconds between ghosts while dodging (real time).")]
        [SerializeField, Min(0.01f)] private float interval = 0.04f;
        [Tooltip("Seconds a ghost takes to fade out (real time).")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.42f;
        [Tooltip("Ghosts kept alive at most (older ones are reused).")]
        [SerializeField, Min(1)] private int maxGhosts = 10;

        private sealed class Ghost
        {
            public GameObject root;
            public MeshFilter[] filters;
            public MeshRenderer[] renderers;
            public float age;
            public bool alive;
        }

        private PlayerMotor motor;
        private SkinnedMeshRenderer[] skins;
        private readonly List<Ghost> ghosts = new List<Ghost>();
        private MaterialPropertyBlock block;
        private Transform container;
        private float spawnTimer;
        private bool wasDodging;

        private void Awake()
        {
            motor = GetComponent<PlayerMotor>();
            skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            block = new MaterialPropertyBlock();
            if (ghostMaterial == null)
            {
                var shader = Shader.Find("MyGame/FX/Afterimage");
                if (shader != null)
                    ghostMaterial = new Material(shader) { name = "DodgeAfterimage (runtime)" };
            }
        }

        private void OnDestroy()
        {
            foreach (var g in ghosts)
                foreach (var f in g.filters)
                    if (f != null && f.sharedMesh != null)
                        Destroy(f.sharedMesh);
            if (container != null)
                Destroy(container.gameObject);
        }

        private void LateUpdate() // after the animator posed the character this frame
        {
            float dt = Time.unscaledDeltaTime;
            if (Time.timeScale == 0f && !TimeStop.IsActive)
                dt = 0f; // paused (menu): ghosts hold still

            bool dodging = motor.IsDodging && ghostMaterial != null && skins.Length > 0;
            if (dodging)
            {
                if (!wasDodging)
                    spawnTimer = 0f; // first ghost right away
                spawnTimer -= dt;
                if (spawnTimer <= 0f)
                {
                    Spawn();
                    spawnTimer = interval;
                }
            }
            wasDodging = dodging;

            // During a perfect-dodge time stop the ghosts freeze with the world and fade once time flows again.
            float fadeDt = TimeStop.IsActive ? 0f : dt;
            for (int i = 0; i < ghosts.Count; i++)
            {
                var g = ghosts[i];
                if (!g.alive)
                    continue;
                g.age += fadeDt;
                float k = 1f - g.age / lifetime;
                if (k <= 0f)
                {
                    g.alive = false;
                    g.root.SetActive(false);
                    continue;
                }
                var c = tint;
                c.a *= k; // linear fade: each ghost stays readable for most of its life
                block.SetColor(BaseColorId, c);
                foreach (var r in g.renderers)
                    r.SetPropertyBlock(block);
            }
        }

        private void Spawn()
        {
            var g = Acquire();
            for (int i = 0; i < skins.Length; i++)
            {
                var skin = skins[i];
                bool show = skin != null && skin.enabled && skin.gameObject.activeInHierarchy;
                g.renderers[i].enabled = show;
                if (!show)
                    continue;
                skin.BakeMesh(g.filters[i].sharedMesh, true); // world scale baked in
                var t = g.filters[i].transform;
                t.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);
            }
            g.age = 0f;
            g.alive = true;
            g.root.SetActive(true);
        }

        private Ghost Acquire()
        {
            foreach (var g in ghosts)
                if (!g.alive)
                    return g;
            if (ghosts.Count >= maxGhosts)
            {
                Ghost oldest = ghosts[0];
                foreach (var g in ghosts)
                    if (g.age > oldest.age)
                        oldest = g;
                return oldest;
            }
            return Create();
        }

        private Ghost Create()
        {
            if (container == null)
                container = new GameObject("DodgeAfterimages").transform; // world space, not under the moving player
            var g = new Ghost
            {
                root = new GameObject("Ghost"),
                filters = new MeshFilter[skins.Length],
                renderers = new MeshRenderer[skins.Length],
            };
            g.root.transform.SetParent(container, false);
            for (int i = 0; i < skins.Length; i++)
            {
                var part = new GameObject(skins[i] != null ? skins[i].name : "Part");
                part.transform.SetParent(g.root.transform, false);
                g.filters[i] = part.AddComponent<MeshFilter>();
                g.filters[i].sharedMesh = new Mesh { name = "AfterimageBake" };
                var r = part.AddComponent<MeshRenderer>();
                // Cover every submesh, including the explorer's separate glove and skin materials.
                int submeshCount = skins[i] != null && skins[i].sharedMesh != null ? skins[i].sharedMesh.subMeshCount : 1;
                var ghostMaterials = new Material[Mathf.Max(1, submeshCount)];
                for (int materialIndex = 0; materialIndex < ghostMaterials.Length; materialIndex++)
                    ghostMaterials[materialIndex] = ghostMaterial;
                r.sharedMaterials = ghostMaterials;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                g.renderers[i] = r;
            }
            g.root.SetActive(false);
            ghosts.Add(g);
            return g;
        }
    }
}
