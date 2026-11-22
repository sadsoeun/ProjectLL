using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>
    /// A basalt pillar that rises out of the arena floor and carries the player like a lift (it is an
    /// <see cref="IPlatformMotion"/>, so PlayerMotor works in its frame while it rises or sinks).
    /// Created and owned by BossController (stun staircase, Eruption safe islands). Life cycle:
    /// Telegraph (cracks + shake + rumble, no collider) -> Rise -> Hold -> [Warn (shake)] -> Sink -> removed.
    /// Pivot = centre of the TOP surface. The code owns the collider (one solid box from the top surface 7 m downward, default layer),
    /// so the art prefab only needs renderers: same contract as EF_BasaltPillar (top ~2.4 m wide (X) x 2.0 m deep (Z), column below the pivot).
    /// While rising, a player standing inside the footprint is lifted with the top (never crushed, never left inside the column).
    /// Everything runs on the fixed step, so pause freezes it.
    /// </summary>
    [DefaultExecutionOrder(-100)] // before PlayerMotor: the player reads this step's velocity
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class BossPillar : MonoBehaviour, IPlatformMotion
    {
        private enum Stage { Telegraph, Rise, Hold, Warn, Sink }

        /// <summary>Width of the art prefab's top surface; the art is scaled in X to the requested width.</summary>
        public const float ArtTopWidth = 2.4f;
        private const float ColumnDepth = 7f;
        private const float HiddenBelowFloor = 0.3f;
        private const float ColliderDepthZ = 3f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Rigidbody body;
        private BoxCollider box;
        private Transform visual;
        private Transform crack;
        private Transform artTelegraph;   // optional child "Telegraph" of the art prefab (floor glow, inactive in the prefab)
        private Vector3 artTelegraphScale = Vector3.one;
        private PlayerMotor player;
        private GameObject dustPrefab;
        private Stage stage = Stage.Telegraph;
        private float timer;
        private float telegraphSeconds, riseSeconds, warnSeconds, sinkSeconds;
        private float width, floorY, topY, hiddenTopY, sinkFromY;
        private Vector3 crackScale = Vector3.one;
        private MaterialPropertyBlock block;

        /// <summary>Movement of this physics step (what the player standing on it is carried by).</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>True once the pillar has started to sink (it removes itself when it is down).</summary>
        public bool IsSinking => stage == Stage.Warn || stage == Stage.Sink;

        /// <summary>World Y of the top surface right now.</summary>
        public float CurrentTopY => body != null ? body.position.y : transform.position.y;

        /// <summary>
        /// Creates a pillar at (<paramref name="x"/>, floor) and starts its telegraph. <paramref name="art"/> may be null
        /// (primitive placeholder). <paramref name="playerMotor"/> may be null (then nobody is lifted).
        /// </summary>
        public static BossPillar Create(Transform parent, GameObject art, GameObject dust, PlayerMotor playerMotor,
            float x, float floorY, float width, float height, float telegraphSeconds, float riseSeconds)
        {
            Assert.IsTrue(width > 0f && height > 0f, "BossPillar: width and height must be positive.");
            var go = new GameObject("BossPillar");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, floorY - HiddenBelowFloor, 0f);
            var pillar = go.AddComponent<BossPillar>(); // Awake adds nothing: RequireComponent supplied Rigidbody + BoxCollider
            pillar.Setup(art, dust, playerMotor, floorY, width, height, telegraphSeconds, riseSeconds);
            return pillar;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            box = GetComponent<BoxCollider>();
            box.isTrigger = false;
            box.enabled = false; // solid only once it rises
            block = new MaterialPropertyBlock();
        }

        private void Setup(GameObject art, GameObject dust, PlayerMotor playerMotor, float floor, float pillarWidth, float height,
            float telegraph, float rise)
        {
            player = playerMotor;
            dustPrefab = dust;
            floorY = floor;
            width = pillarWidth;
            topY = floor + height;
            hiddenTopY = floor - HiddenBelowFloor;
            telegraphSeconds = Mathf.Max(0.05f, telegraph);
            riseSeconds = Mathf.Max(0.05f, rise);

            box.size = new Vector3(width, ColumnDepth, ColliderDepthZ);
            box.center = new Vector3(0f, -ColumnDepth * 0.5f, 0f);
            BuildVisual(art);
            if (artTelegraph != null)
                artTelegraph.gameObject.SetActive(true);
            else
                BuildCrack();
            stage = Stage.Telegraph;
            timer = 0f;
            body.position = new Vector3(transform.position.x, hiddenTopY, 0f);
        }

        private void BuildVisual(GameObject art)
        {
            if (art != null)
            {
                var go = Instantiate(art, transform);
                go.name = "Visual";
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = new Vector3(width / ArtTopWidth, 1f, 1f);
                StripColliders(go);
                visual = go.transform;
                artTelegraph = visual.Find("Telegraph");
                if (artTelegraph != null)
                    artTelegraphScale = artTelegraph.localScale;
                return;
            }
            // Placeholder: a dark basalt block, top surface at the pivot.
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Visual";
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(transform, false);
            cube.transform.localPosition = new Vector3(0f, -ColumnDepth * 0.5f, 0f);
            cube.transform.localScale = new Vector3(width, ColumnDepth, 2f);
            var mr = cube.GetComponent<MeshRenderer>();
            block.SetColor(BaseColorId, new Color(0.2f, 0.19f, 0.22f));
            mr.SetPropertyBlock(block);
            visual = cube.transform;
        }

        private static void StripColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                Destroy(c); // the code owns the collider; art must not add its own
        }

        /// <summary>Glowing floor crack that shows where the pillar is about to rise.</summary>
        private void BuildCrack()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Crack";
            Destroy(quad.GetComponent<Collider>());
            // Unparented: the crack must stay on the floor while the pillar moves.
            quad.transform.SetParent(transform.parent, false);
            quad.transform.position = new Vector3(transform.position.x, floorY + 0.04f, 0f);
            quad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            crackScale = new Vector3(width * 1.1f, 1.6f, 1f);
            quad.transform.localScale = crackScale * 0.3f;
            var mr = quad.GetComponent<MeshRenderer>();
            block.SetColor(BaseColorId, new Color(1f, 0.45f, 0.1f));
            mr.SetPropertyBlock(block);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            crack = quad.transform;
        }

        /// <summary>Starts the sink. A short shake of <paramref name="warn"/> seconds comes first (0 = sink at once).</summary>
        public void BeginSink(float warn, float sink)
        {
            if (IsSinking)
                return;
            warnSeconds = Mathf.Max(0f, warn);
            sinkSeconds = Mathf.Max(0.05f, sink);
            DestroyCrack();
            if (stage == Stage.Telegraph)
            {
                Destroy(gameObject); // never rose: nothing to carry
                return;
            }
            stage = Stage.Warn;
            timer = 0f;
            if (warnSeconds <= 0f)
                StartSink();
        }

        private void StartSink()
        {
            stage = Stage.Sink;
            timer = 0f;
            sinkFromY = body.position.y;
            Puff();
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            timer += dt;
            Vector3 current = body.position;
            float nextY = current.y;
            switch (stage)
            {
                case Stage.Telegraph:
                    {
                        float k = Mathf.Clamp01(timer / telegraphSeconds);
                        Shake(0.04f + 0.05f * k);
                        float pulse = 0.8f + 0.2f * Mathf.Sin(timer * Mathf.Lerp(14f, 40f, k));
                        if (crack != null)
                            crack.localScale = crackScale * Mathf.Lerp(0.3f, 1f, k) * pulse;
                        if (artTelegraph != null)
                        {
                            // the pillar is still hidden below the floor: the art's glow is held on the floor surface
                            artTelegraph.position = new Vector3(transform.position.x, floorY + 0.03f, 0f);
                            artTelegraph.localScale = artTelegraphScale * Mathf.Lerp(0.5f, 1f, k) * pulse;
                            var tr = artTelegraph.GetComponent<Renderer>();
                            if (tr != null)
                            {
                                tr.GetPropertyBlock(block);
                                block.SetColor(EmissionColorId, Color.white * (0.6f + 2.4f * k * pulse));
                                tr.SetPropertyBlock(block);
                            }
                        }
                        if (timer >= telegraphSeconds)
                        {
                            stage = Stage.Rise;
                            timer = 0f;
                            box.enabled = true;
                            DestroyCrack();
                            Puff();
                            Sfx.Play(SfxId.BossSlam);
                        }
                        break;
                    }
                case Stage.Rise:
                    {
                        float t = Mathf.Clamp01(timer / riseSeconds);
                        nextY = Mathf.Lerp(hiddenTopY, topY, t);
                        LiftPlayer(nextY);
                        if (t >= 1f)
                        {
                            stage = Stage.Hold;
                            timer = 0f;
                        }
                        break;
                    }
                case Stage.Hold:
                    Shake(0f);
                    break;
                case Stage.Warn:
                    Shake(0.07f);
                    if (timer >= warnSeconds)
                        StartSink();
                    break;
                case Stage.Sink:
                    {
                        Shake(0f);
                        float t = Mathf.Clamp01(timer / sinkSeconds);
                        nextY = Mathf.Lerp(sinkFromY, hiddenTopY, t);
                        if (t >= 1f)
                        {
                            Velocity = Vector3.zero;
                            Destroy(gameObject);
                        }
                        break;
                    }
            }
            Velocity = new Vector3(0f, (nextY - current.y) / dt, 0f);
            if (nextY != current.y)
                body.MovePosition(new Vector3(current.x, nextY, 0f));
        }

        /// <summary>
        /// A player whose centre is inside the footprint and whose feet are below the rising top is lifted onto it:
        /// the pillar never rises into the player and never leaves them embedded in the column.
        /// </summary>
        private void LiftPlayer(float newTopY)
        {
            if (player == null)
                return;
            float dx = Mathf.Abs(player.transform.position.x - body.position.x);
            if (dx <= width * 0.5f && player.FeetY < newTopY && player.FeetY > hiddenTopY - 1f)
                player.LiftFeetTo(newTopY + 0.01f);
        }

        private void Shake(float amplitude)
        {
            if (visual == null)
                return;
            var p = visual.localPosition;
            p.x = amplitude > 0f ? Mathf.Sin(Time.time * 70f) * amplitude : 0f;
            visual.localPosition = p;
        }

        private void Puff()
        {
            if (dustPrefab == null)
                return;
            var fx = Instantiate(dustPrefab, new Vector3(body.position.x, floorY, 0f), Quaternion.identity, transform.parent);
            Destroy(fx, 3f);
        }

        private void DestroyCrack()
        {
            if (crack != null)
                Destroy(crack.gameObject);
            crack = null;
            if (artTelegraph != null)
                artTelegraph.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            Velocity = Vector3.zero;
            DestroyCrack();
        }
    }
}
