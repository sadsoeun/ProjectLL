using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// One cell of a boss Eruption attack: hidden for <c>delay</c>, then a glowing floor crack warns for <c>warn</c> seconds,
    /// then a lava column shoots up and is lethal for <c>active</c> seconds, then it sinks and the object removes itself.
    /// BossController spawns a row of these outward from the boss (each a little later than the previous one), so a cell that
    /// has just erupted is the safe place to step into. Pivot = floor centre of the cell; the trigger box is set by Init.
    /// Children (optional): "Warn" (floor crack, pulsed) and "Column" (scaled up from the floor).
    /// Moves on the fixed step, so pause freezes it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class BossEruption : MonoBehaviour
    {
        private enum Phase { Delay, Warn, Rise, Active, Sink }

        [SerializeField] private Transform warnVisual;
        [SerializeField] private Transform columnVisual;
        [Tooltip("Seconds the column takes to shoot up (harmless until 60 % up) and to sink back.")]
        [SerializeField, Min(0.02f)] private float riseSeconds = 0.1f;
        [SerializeField, Min(0.02f)] private float sinkSeconds = 0.18f;

        private StageRoot stage;
        private BoxCollider hitBox;
        private Phase phase = Phase.Delay;
        private float timer;
        private float delay, warn, active;
        private Vector3 warnScale = Vector3.one, columnScale = Vector3.one;

        private void Awake()
        {
            var body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            hitBox = GetComponent<BoxCollider>();
            hitBox.isTrigger = true;
            hitBox.enabled = false;
            if (warnVisual == null) warnVisual = transform.Find("Warn");
            if (columnVisual == null) columnVisual = transform.Find("Column");
            if (warnVisual != null) { warnScale = warnVisual.localScale; warnVisual.gameObject.SetActive(false); }
            if (columnVisual != null) { columnScale = columnVisual.localScale; columnVisual.gameObject.SetActive(false); }
        }

        public void Init(StageRoot owner, float delaySeconds, float warnSeconds, float activeSeconds, float width, float height)
        {
            stage = owner;
            delay = delaySeconds;
            warn = warnSeconds;
            active = activeSeconds;
            hitBox.size = new Vector3(width, height, 3f);
            hitBox.center = new Vector3(0f, height * 0.5f, 0f);
            phase = Phase.Delay;
            timer = 0f;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            timer += dt;
            switch (phase)
            {
                case Phase.Delay:
                    if (timer >= delay) { phase = Phase.Warn; timer = 0f; Show(warnVisual, true); }
                    break;
                case Phase.Warn:
                    if (warnVisual != null)
                    {
                        // pulse faster as the eruption gets closer
                        float k = timer / Mathf.Max(warn, 0.01f);
                        float pulse = 0.85f + 0.25f * Mathf.Sin(timer * Mathf.Lerp(10f, 32f, k));
                        warnVisual.localScale = new Vector3(warnScale.x * pulse, warnScale.y, warnScale.z * pulse);
                    }
                    if (timer >= warn)
                    {
                        phase = Phase.Rise; timer = 0f;
                        Show(columnVisual, true);
                        Sfx.Play(SfxId.BossSlam);
                    }
                    break;
                case Phase.Rise:
                    SetColumn(timer / riseSeconds);
                    if (timer >= riseSeconds * 0.6f) hitBox.enabled = true;
                    if (timer >= riseSeconds) { phase = Phase.Active; timer = 0f; SetColumn(1f); }
                    break;
                case Phase.Active:
                    SetColumn(1f + 0.06f * Mathf.Sin(timer * 40f));
                    if (timer >= active)
                    {
                        phase = Phase.Sink; timer = 0f;
                        hitBox.enabled = false;
                        Show(warnVisual, false);
                    }
                    break;
                case Phase.Sink:
                    SetColumn(1f - timer / sinkSeconds);
                    if (timer >= sinkSeconds) Destroy(gameObject);
                    break;
            }
        }

        private void SetColumn(float t)
        {
            if (columnVisual == null) return;
            t = Mathf.Max(0f, t);
            columnVisual.localScale = new Vector3(columnScale.x, columnScale.y * Mathf.Max(0.001f, t), columnScale.z);
        }

        private static void Show(Transform t, bool on)
        {
            if (t != null) t.gameObject.SetActive(on);
        }

        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);

        private void Hit(Collider other)
        {
            if (hitBox.enabled && stage != null && PlayerMotor.TryGetFromCollider(other, out _))
                stage.NotifyPlayerHit(dodgeable: true);
        }
    }
}
