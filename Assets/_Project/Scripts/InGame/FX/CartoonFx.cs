using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Short comic effect (monster attack telegraphs): pops, bursts, puffs, swipes and ground rings. The model is a
    /// child; this root only animates scale / rotation / position over its lifetime and destroys itself at the end.
    /// Flat effects face the camera when billboard is set (the model's front faces local -Z).
    /// </summary>
    public sealed class CartoonFx : MonoBehaviour
    {
        public enum Style
        {
            /// <summary>Pops in with an overshoot, wobbles, shrinks away at the end ("!!").</summary>
            Pop,
            /// <summary>Grows quickly from a small size, then shrinks (surprise lines).</summary>
            Burst,
            /// <summary>Grows in X/Z to the given radius while flattening (ground shockwave).</summary>
            Ring,
            /// <summary>Grows, drifts up and shrinks (dust).</summary>
            Puff,
            /// <summary>Sweeps through an arc and fades by shrinking (melee swoosh).</summary>
            Swipe,
        }

        [SerializeField] private Style style = Style.Pop;
        [SerializeField, Min(0.05f)] private float lifetime = 0.8f;
        [SerializeField] private bool billboard;
        [Tooltip("Pop: wobble angle in degrees.")]
        [SerializeField, Min(0f)] private float wobble = 8f;

        private float age;
        private Vector3 baseScale = Vector3.one;
        private Vector3 basePosition;
        private float ringRadius = 1f;
        private int side = 1;
        private Transform follow;
        private Vector3 followOffset;

        public float Lifetime => lifetime;
        public float Age => age;

        /// <summary>Spawns an effect. scale = uniform size; lifetime &lt;= 0 keeps the prefab value; side mirrors along X.</summary>
        public static CartoonFx Spawn(CartoonFx prefab, Vector3 position, float scale, float lifetime = -1f, int side = 1,
            Transform follow = null)
        {
            if (prefab == null)
                return null;
            var fx = Instantiate(prefab, position, Quaternion.identity);
            fx.Init(scale, lifetime, side, follow);
            return fx;
        }

        /// <summary>Ground ring that grows to <paramref name="radius"/> over its lifetime.</summary>
        public static CartoonFx SpawnRing(CartoonFx prefab, Vector3 position, float radius, float lifetime)
        {
            var fx = Spawn(prefab, position, 1f, lifetime);
            if (fx != null)
                fx.ringRadius = radius;
            return fx;
        }

        private void Init(float scale, float life, int mirror, Transform target)
        {
            if (life > 0f)
                lifetime = life;
            side = mirror >= 0 ? 1 : -1;
            baseScale = new Vector3(scale * side, scale, scale);
            basePosition = transform.position;
            follow = target;
            if (follow != null)
                followOffset = transform.position - follow.position;
            Apply(0f);
        }

        /// <summary>Ends the effect early (shrinks over the next 0.1 s).</summary>
        public void Finish() => age = Mathf.Max(age, lifetime - 0.1f);

        private void LateUpdate()
        {
            age += Time.deltaTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }
            if (follow != null)
                basePosition = follow.position + followOffset;
            Apply(age / lifetime);
        }

        private void Apply(float k)
        {
            float s;
            Vector3 pos = basePosition;
            Quaternion rot = Quaternion.identity;
            Vector3 scale = baseScale;
            switch (style)
            {
                case Style.Pop:
                {
                    float t = age;
                    if (t < 0.10f) s = Mathf.Lerp(0f, 1.3f, EaseOut(t / 0.10f));
                    else if (t < 0.18f) s = Mathf.Lerp(1.3f, 0.92f, (t - 0.10f) / 0.08f);
                    else if (t < 0.26f) s = Mathf.Lerp(0.92f, 1f, (t - 0.18f) / 0.08f);
                    else s = 1f + Mathf.Sin(t * 18f) * 0.04f;
                    float remain = lifetime - age;
                    if (remain < 0.12f) s *= Mathf.Clamp01(remain / 0.12f);
                    rot = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 14f) * wobble * Mathf.Exp(-t * 2f));
                    pos += Vector3.up * (Mathf.Sin(Mathf.Min(t, 0.2f) / 0.2f * Mathf.PI) * 0.08f);
                    scale *= s;
                    break;
                }
                case Style.Burst:
                    s = k < 0.35f ? Mathf.Lerp(0.55f, 1.15f, EaseOut(k / 0.35f)) : Mathf.Lerp(1.15f, 0f, EaseIn((k - 0.35f) / 0.65f));
                    scale *= s;
                    break;
                case Style.Puff:
                    s = k < 0.3f ? Mathf.Lerp(0.3f, 1.1f, EaseOut(k / 0.3f)) : Mathf.Lerp(1.1f, 0f, EaseIn((k - 0.3f) / 0.7f));
                    pos += Vector3.up * (0.35f * k * baseScale.y);
                    scale *= s;
                    break;
                case Style.Swipe:
                {
                    float sweep = EaseOut(Mathf.Clamp01(k / 0.45f));
                    rot = Quaternion.Euler(0f, 0f, Mathf.Lerp(55f, -25f, sweep) * side);
                    s = k < 0.6f ? Mathf.Lerp(0.6f, 1f, sweep) : Mathf.Lerp(1f, 0f, EaseIn((k - 0.6f) / 0.4f));
                    scale = new Vector3(baseScale.x * s, baseScale.y * Mathf.Max(s, 0.001f), baseScale.z);
                    break;
                }
                case Style.Ring:
                {
                    float r = Mathf.Lerp(0.15f, ringRadius, EaseOut(k));
                    float h = k < 0.75f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.75f) / 0.25f);
                    scale = new Vector3(r, Mathf.Max(h, 0.001f), r);
                    break;
                }
            }
            transform.position = pos;
            if (billboard && Camera.main != null)
                rot = Quaternion.LookRotation(Camera.main.transform.forward, Vector3.up) * rot;
            transform.rotation = rot;
            transform.localScale = scale;
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x);
        private static float EaseIn(float x) => x * x;
    }
}
