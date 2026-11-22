using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Projectile of a boss fight. Two modes, both kill the player on contact (trigger Enter and Stay):
    /// Wave   - travels along the floor in X at a fixed speed until it leaves the arena (a jumpable shockwave).
    /// Falling - drops from above onto a landing height (the shadow marker shows where); lethal only while falling.
    /// Created by BossController, which owns and clears it. Moves on the fixed step, so pause freezes it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class BossHazard : MonoBehaviour
    {
        private enum Mode { Idle, Wave, Falling, Landed }

        [Tooltip("Seconds a fallen object stays (harmless) before it disappears.")]
        [SerializeField, Min(0f)] private float landedLinger = 0.35f;

        private Rigidbody body;
        private Collider hitBox;
        private StageRoot stage;
        private Mode mode = Mode.Idle;
        private float speed;
        private float direction;
        private float minX, maxX;
        private float landY;
        private float timer;
        private GameObject marker;

        /// <summary>Travel direction of a wave (-1 / +1); 0 before InitWave.</summary>
        public int Direction => mode == Mode.Wave ? (direction >= 0f ? 1 : -1) : 0;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            hitBox = GetComponent<Collider>();
            hitBox.isTrigger = true;
        }

        public void InitWave(StageRoot owner, int dir, float waveSpeed, float arenaMinX, float arenaMaxX)
        {
            stage = owner;
            mode = Mode.Wave;
            direction = dir >= 0 ? 1f : -1f;
            speed = waveSpeed;
            minX = arenaMinX - 1f;
            maxX = arenaMaxX + 1f;
        }

        public void InitFall(StageRoot owner, float groundY, float fallSpeed, GameObject shadowMarker)
        {
            stage = owner;
            mode = Mode.Falling;
            landY = groundY;
            speed = fallSpeed;
            marker = shadowMarker;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 p = body.position;
            switch (mode)
            {
                case Mode.Wave:
                    p.x += direction * speed * dt;
                    body.MovePosition(p);
                    if (p.x < minX || p.x > maxX)
                        Destroy(gameObject);
                    break;

                case Mode.Falling:
                    p.y -= speed * dt;
                    if (p.y <= landY)
                    {
                        p.y = landY;
                        mode = Mode.Landed;
                        timer = 0f;
                        hitBox.enabled = false;   // harmless once it has landed
                        if (marker != null)
                            Destroy(marker);
                    }
                    body.MovePosition(p);
                    break;

                case Mode.Landed:
                    timer += dt;
                    if (timer >= landedLinger)
                        Destroy(gameObject);
                    break;
            }
        }

        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);

        private void Hit(Collider other)
        {
            if ((mode == Mode.Wave || mode == Mode.Falling) && stage != null && PlayerMotor.TryGetFromCollider(other, out _))
                stage.NotifyPlayerHit(dodgeable: true);
        }

        private void OnDestroy()
        {
            if (marker != null)
                Destroy(marker);
        }
    }
}
