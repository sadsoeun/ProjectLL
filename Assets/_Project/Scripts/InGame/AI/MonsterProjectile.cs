using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Shot fired by a monster (Monster.FireAt). Flies in a straight line on the XY plane and kills the player on
    /// contact; it cannot be stomped. It is destroyed when it hits solid geometry or its lifetime runs out.
    /// The firing monster owns its live shots and clears them on respawn, retry and defeat.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class MonsterProjectile : MonoBehaviour
    {
        [Tooltip("Seconds before the shot disappears on its own.")]
        [SerializeField, Min(0.1f)] private float lifetime = 4f;
        [Tooltip("Solid layers that stop the shot. Triggers are always ignored.")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [Tooltip("Optional model: turned so its nose points along the flight, and spun around that axis.")]
        [SerializeField] private Transform visual;
        [Tooltip("Local axis of the model that is its nose.")]
        [SerializeField] private Vector3 visualNoseAxis = Vector3.right;
        [Tooltip("Roll around the flight axis (degrees per second).")]
        [SerializeField] private float visualSpin = 720f;

        private Rigidbody body;
        private Monster owner;
        private StageRoot stage;
        private Vector3 velocity;
        private float expiresAt;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            GetComponent<Collider>().isTrigger = true;
        }

        /// <summary>Called once right after Instantiate.</summary>
        public void Launch(Monster from, StageRoot owner, Vector3 direction, float speed)
        {
            this.owner = from;
            stage = owner;
            direction.z = 0f;
            velocity = direction.sqrMagnitude > 0f ? direction.normalized * speed : Vector3.zero;
            expiresAt = Time.time + lifetime;
            OrientVisual();
        }

        private void FixedUpdate()
        {
            if (Time.time >= expiresAt)
            {
                Despawn();
                return;
            }
            Vector3 step = velocity * Time.fixedDeltaTime;
            // Swept test so a fast shot cannot pass through a thin wall between two steps.
            if (step != Vector3.zero
                && Physics.Raycast(body.position, step.normalized, out RaycastHit hit, step.magnitude, obstacleMask, QueryTriggerInteraction.Ignore)
                && !PlayerMotor.TryGetFromCollider(hit.collider, out _)
                && !IsOwner(hit.collider))
            {
                Despawn();
                return;
            }
            body.MovePosition(body.position + step);
        }

        private void Update() => OrientVisual();

        private void OrientVisual()
        {
            if (visual == null || velocity == Vector3.zero || visualNoseAxis == Vector3.zero)
                return;
            Quaternion along = Quaternion.FromToRotation(visualNoseAxis.normalized, velocity.normalized);
            visual.rotation = Quaternion.AngleAxis(Time.time * visualSpin, velocity.normalized) * along;
        }

        private bool IsOwner(Collider c) => owner != null && c.transform.IsChildOf(owner.transform);

        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);

        private void Hit(Collider other)
        {
            if (stage == null || !PlayerMotor.TryGetFromCollider(other, out _))
                return;
            stage.NotifyPlayerHit(dodgeable: true);
            Despawn();
        }

        public void Despawn()
        {
            if (owner != null)
                owner.ForgetProjectile(this);
            Destroy(gameObject);
        }
    }
}
