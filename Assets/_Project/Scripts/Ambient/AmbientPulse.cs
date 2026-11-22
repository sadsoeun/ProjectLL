using UnityEngine;

namespace MyGame
{
    /// <summary>Breathing emission glow (ice crystals, embers, treasure) through a MaterialPropertyBlock, so shared materials stay untouched.</summary>
    [RequireComponent(typeof(Renderer))]
    public sealed class AmbientPulse : MonoBehaviour
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        [SerializeField, Min(0f)] private float minScale = 0.55f;
        [SerializeField, Min(0f)] private float maxScale = 1.35f;
        [SerializeField, Min(0.1f)] private float period = 2.4f;

        private Renderer target;
        private MaterialPropertyBlock block;
        private Color[] baseEmission;
        private float phase;

        private void Awake()
        {
            target = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            var mats = target.sharedMaterials;
            baseEmission = new Color[mats.Length];
            for (int i = 0; i < mats.Length; i++)
                baseEmission[i] = mats[i] != null && mats[i].HasProperty(EmissionId) ? mats[i].GetColor(EmissionId) : Color.black;
            phase = Random.value * Mathf.PI * 2f;
        }

        private void Update()
        {
            if (block == null)
                Awake(); // fields are not serialized: re-init after a domain reload in Play Mode
            float k = Mathf.Lerp(minScale, maxScale, Mathf.Sin(Time.time * Mathf.PI * 2f / period + phase) * 0.5f + 0.5f);
            for (int i = 0; i < baseEmission.Length; i++)
            {
                if (baseEmission[i].maxColorComponent <= 0f)
                    continue;
                target.GetPropertyBlock(block, i);
                block.SetColor(EmissionId, baseEmission[i] * k);
                target.SetPropertyBlock(block, i);
            }
        }
    }
}
