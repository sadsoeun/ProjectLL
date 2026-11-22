using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Look of the Sun Guardian's floor shockwave: a sand gust rolling along the floor (model SD_FX_SandGust).
    /// Faces the travel direction of the BossHazard on the same object, spins its wind swirls, makes the sand crest
    /// surge and leaves sand puffs behind. Purely visual: the hit box stays the BossHazard collider.
    /// </summary>
    public sealed class SandGustFx : MonoBehaviour
    {
        [Tooltip("Model root (its +X is the leading edge).")]
        [SerializeField] private Transform model;
        [Tooltip("Wind swirls spun around the camera axis.")]
        [SerializeField] private Transform[] swirls;
        [Tooltip("Sand crest that surges up and down.")]
        [SerializeField] private Transform crest;
        [Tooltip("Speed lines that flicker in length.")]
        [SerializeField] private Transform[] streaks;
        [SerializeField] private CartoonFx puffPrefab;
        [SerializeField, Min(0.02f)] private float puffInterval = 0.09f;
        [SerializeField] private float swirlSpeed = 540f;
        [Tooltip("Model shift along the travel direction (m): puts the sand crest over the hit box.")]
        [SerializeField] private float leadOffset = -0.3f;

        private BossHazard hazard;
        private int side;
        private Vector3 modelScale;
        private Vector3 modelPosition;
        private Vector3 crestScale;
        private Vector3[] streakScales;
        private float clock;
        private float nextPuff;

        private void Awake()
        {
            hazard = GetComponent<BossHazard>();
            if (model != null)
            {
                modelScale = model.localScale;
                modelPosition = model.localPosition;
            }
            if (crest != null)
                crestScale = crest.localScale;
            streakScales = new Vector3[streaks != null ? streaks.Length : 0];
            for (int i = 0; i < streakScales.Length; i++)
                streakScales[i] = streaks[i] != null ? streaks[i].localScale : Vector3.one;
        }

        private void Update()
        {
            int dir = hazard != null && hazard.Direction != 0 ? hazard.Direction : 1;
            if (dir != side && model != null)
            {
                side = dir;
                model.localScale = new Vector3(Mathf.Abs(modelScale.x) * side, modelScale.y, modelScale.z);
                model.localPosition = modelPosition + Vector3.right * (leadOffset * side);
            }
            clock += Time.deltaTime;

            if (swirls != null)
            {
                foreach (var s in swirls)
                {
                    if (s != null)
                        s.Rotate(Vector3.forward, -swirlSpeed * side * Time.deltaTime, Space.World);
                }
            }
            if (crest != null)
            {
                float surge = 1f + 0.12f * Mathf.Sin(clock * 22f) + 0.06f * Mathf.Sin(clock * 37f);
                crest.localScale = new Vector3(crestScale.x * (1f - 0.5f * (surge - 1f)), crestScale.y * surge, crestScale.z);
            }
            for (int i = 0; i < streakScales.Length; i++)
            {
                if (streaks[i] == null)
                    continue;
                float k = 0.8f + 0.3f * Mathf.PerlinNoise(clock * 9f, i * 3.1f);
                streaks[i].localScale = new Vector3(streakScales[i].x * k, streakScales[i].y, streakScales[i].z);
            }
            if (puffPrefab != null && clock >= nextPuff)
            {
                nextPuff = clock + puffInterval;
                Vector3 at = transform.position + new Vector3(-side * Random.Range(0.6f, 1.2f), 0f, Random.Range(-0.6f, 0.4f));
                CartoonFx.Spawn(puffPrefab, at, Random.Range(0.7f, 1.1f), Random.Range(0.4f, 0.6f));
            }
        }
    }
}
