using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Pressure plate. While the player stands on it (overlaps its trigger with the feet near the plate top) it is
    /// pressed: the plate sinks and the emblem glows. A `once` switch keeps its channel raised for the rest of the
    /// attempt (also after a death); a momentary one holds the channel only while pressed.
    /// Retry releases everything. Lights come from a MaterialPropertyBlock, so shared materials are not touched.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class FloorSwitch : MonoBehaviour, IStageElement
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Channel raised while / after this switch is pressed. Gates with the same channel open.")]
        [SerializeField] private int channel;
        [Tooltip("On: stays pressed for the rest of the attempt. Off: while the player stands on it plus releaseDelay.")]
        [SerializeField] private bool once = true;
        [Tooltip("Moves down when pressed (local Y).")]
        [SerializeField] private Transform plate;
        [SerializeField, Min(0f)] private float pressDepth = 0.08f;
        [SerializeField, Min(0.01f)] private float pressSeconds = 0.12f;
        [Tooltip("Renderers that glow when pressed (the emblem).")]
        [SerializeField] private Renderer[] glow;
        [Tooltip("Also tint _BaseColor (primitive fallback without an emission map).")]
        [SerializeField] private bool tintBase;
        [SerializeField] private Color idleBase = new Color(0.55f, 0.45f, 0.18f);
        [SerializeField] private Color pressedBase = new Color(1f, 0.85f, 0.25f);
        [SerializeField, Min(0f)] private float idleGlow = 0.3f;
        [SerializeField, Min(0f)] private float pressedGlow = 2.4f;
        [Tooltip("The feet must be at most this far below the plate top to press it (side entries from below are ignored).")]
        [SerializeField, Min(0f)] private float feetTolerance = 0.15f;
        [Tooltip("Momentary switch: the channel stays raised this long after the player steps off (time to reach the gate).")]
        [SerializeField, Min(0f)] private float releaseDelay = 2f;

        private StageRoot stage;
        private MaterialPropertyBlock block;
        private Vector3 plateRest;
        private float pressAmount;      // 0..1 visual
        private bool pressed;           // logical state
        private bool holding;           // currently counted in the channel (momentary or once)
        private bool touchingThisStep;
        private float lastTouchTime;
        private float topY;

        public int Channel => channel;
        public bool IsPressed => pressed;

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (plate != null)
                plateRest = plate.localPosition;
            topY = transform.position.y;
            ApplyVisual(0f);
        }

        public void Bind(StageRoot owner) => stage = owner;

        public void ResetForRespawn()
        {
            // A once-switch stays pressed after a death; a momentary one is released (the player is gone).
            if (!once)
                Release();
        }

        public void ResetForAttempt()
        {
            if (holding && stage != null)
                stage.RemoveChannelHold(channel); // harmless after StageRoot cleared the holds (no-op at 0)
            holding = false;
            pressed = false;
            pressAmount = 0f;
            ApplyVisual(0f);
        }

        private void FixedUpdate()
        {
            // Trigger callbacks of this step ran before FixedUpdate of the next one: release when none arrived.
            if (pressed && !once && !touchingThisStep && Time.time - lastTouchTime > releaseDelay)
                Release();
            touchingThisStep = false;
        }

        private void Update()
        {
            float target = pressed ? 1f : 0f;
            if (!Mathf.Approximately(pressAmount, target))
            {
                pressAmount = Mathf.MoveTowards(pressAmount, target, Time.deltaTime / pressSeconds);
                ApplyVisual(pressAmount);
            }
        }

        private void OnTriggerEnter(Collider other) => Touch(other);
        private void OnTriggerStay(Collider other) => Touch(other);

        private void Touch(Collider other)
        {
            if (stage == null || !PlayerMotor.TryGetFromCollider(other, out var player))
                return;
            if (player.FeetY < topY - feetTolerance)
                return;
            touchingThisStep = true;
            lastTouchTime = Time.time;
            if (pressed)
                return;
            pressed = true;
            Sfx.Play(SfxId.Switch);
            if (!holding)
            {
                holding = true;
                stage.AddChannelHold(channel);
            }
        }

        private void Release()
        {
            pressed = false;
            if (holding && stage != null)
                stage.RemoveChannelHold(channel);
            holding = false;
        }

        private void ApplyVisual(float amount)
        {
            if (plate != null)
                plate.localPosition = plateRest + Vector3.down * (pressDepth * amount);
            if (glow == null)
                return;
            float g = Mathf.Lerp(idleGlow, pressedGlow, amount);
            for (int i = 0; i < glow.Length; i++)
            {
                if (glow[i] == null)
                    continue;
                glow[i].GetPropertyBlock(block);
                block.SetColor(EmissionColorId, Color.white * g);
                if (tintBase)
                    block.SetColor(BaseColorId, Color.Lerp(idleBase, pressedBase, amount));
                glow[i].SetPropertyBlock(block);
            }
        }
    }
}
