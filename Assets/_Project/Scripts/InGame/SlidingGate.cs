using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Solid wall that slides into the ground (or up) when its channel is raised (see FloorSwitch). While it moves it
    /// still blocks; once fully open its collider is switched off. `invert` flips the logic: the gate is open while
    /// the channel is NOT raised (used by boss arenas: the entrance closes when the fight starts).
    /// A gate that wants to close while the player stands in its footprint waits until the footprint is free.
    /// Retry closes it again; a death leaves it as it is (switches keep their channel for the attempt).
    /// Runs on the fixed step with a kinematic body, so pause freezes it and the player is carried correctly.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class SlidingGate : MonoBehaviour, IStageElement
    {
        [SerializeField] private int channel;
        [SerializeField] private bool invert;
        [Tooltip("Seconds for a full open / close slide.")]
        [SerializeField, Min(0.05f)] private float openSeconds = 0.8f;
        [Tooltip("How far it moves when open. 0 = the collider height (computed from the box).")]
        [SerializeField, Min(0f)] private float slideDistance;
        [Tooltip("Slide up instead of down.")]
        [SerializeField] private bool slideUp;
        [Tooltip("Extra distance beside the gate in which a standing player keeps it from closing.")]
        [SerializeField, Min(0f)] private float playerClearance = 0.6f;

        private StageRoot stage;
        private Rigidbody body;
        private BoxCollider box;
        private Vector3 closedPosition;
        private float amount;   // 0 closed .. 1 open
        private bool wantsOpen;

        public int Channel => channel;
        public bool IsFullyOpen => amount >= 1f;
        public bool IsClosed => amount <= 0f;
        public bool WantsOpen => wantsOpen;

        private float Distance => slideDistance > 0f ? slideDistance : (box != null ? box.size.y * Mathf.Abs(transform.lossyScale.y) + 0.05f : 3f);

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            box = GetComponent<BoxCollider>();
            closedPosition = transform.position;
            amount = invert ? 1f : 0f;     // an inverted gate starts open (channel not raised)
            wantsOpen = invert;
            Apply(true);
        }

        public void Bind(StageRoot owner) => stage = owner;

        public void ResetForRespawn()
        {
            // Switch gates keep their state; inverted (arena) gates snap to their channel: open again after the boss
            // released it, still closed while a fight keeps running through the death.
            if (invert)
                SetImmediate(stage == null || !stage.IsChannelRaised(channel));
        }

        public void ResetForAttempt() => SetImmediate(invert);

        private void SetImmediate(bool open)
        {
            amount = open ? 1f : 0f;
            wantsOpen = open;
            Apply(true);
        }

        private void FixedUpdate()
        {
            if (stage == null)
                return;
            bool raised = stage.IsChannelRaised(channel);
            bool open = invert ? !raised : raised;
            if (!open && wantsOpen && PlayerInFootprint())
                open = true; // do not crush the player: stay open until the footprint is free
            if (open != wantsOpen && PlayerNearby())
                Sfx.Play(SfxId.Gate);
            wantsOpen = open;

            float target = wantsOpen ? 1f : 0f;
            if (amount == target)
                return;
            amount = Mathf.MoveTowards(amount, target, Time.fixedDeltaTime / openSeconds);
            if (Mathf.Abs(amount - target) < 1e-4f)
                amount = target; // float drift left it at 0.9999992 (collider never disabled, IsFullyOpen false)
            Apply(false);
        }

        // Only audible gates near the player (all gates of a channel move together).
        private bool PlayerNearby() => stage.Player != null && Mathf.Abs(stage.Player.position.x - closedPosition.x) < 14f;

        private bool PlayerInFootprint()
        {
            if (stage.Player == null)
                return false;
            Vector3 p = stage.Player.position;
            Vector3 c = closedPosition;
            return Mathf.Abs(p.x - c.x) <= (box != null ? box.size.x * 0.5f : 0.5f) + playerClearance && p.y >= c.y - 0.5f && p.y <= c.y + (box != null ? box.size.y : 3f);
        }

        private void Apply(bool teleport)
        {
            Vector3 pos = closedPosition + (slideUp ? Vector3.up : Vector3.down) * (Distance * amount);
            if (teleport)
            {
                transform.position = pos;
                body.position = pos;
            }
            else
            {
                body.MovePosition(pos);
            }
            if (box != null)
                box.enabled = amount < 1f;
        }
    }
}
