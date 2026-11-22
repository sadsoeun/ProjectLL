using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Activates when the player touches it: the banner changes colour and rises, and the attempt's
    /// respawn point moves here (only if it is further along the stage than the current one).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Checkpoint : MonoBehaviour, IStageElement
    {
        [SerializeField] private string id;
        [SerializeField] private Transform respawnPoint;
        [SerializeField] private Renderer banner;
        [SerializeField] private Material inactiveMaterial;
        [SerializeField] private Material activeMaterial;
        [SerializeField] private float bannerLowY = 1.4f;
        [SerializeField] private float bannerHighY = 2.5f;

        private StageRoot stage;
        private bool active;

        public string Id => id;
        public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position + Vector3.up * 1.2f;

        public void Bind(StageRoot owner) => stage = owner;
        public void ResetForRespawn() { }

        public void ResetForAttempt() => SetActive(false);

        private void Awake() => SetActive(false);

        private void OnTriggerEnter(Collider other)
        {
            if (active || stage == null || !PlayerMotor.TryGetFromCollider(other, out _))
                return;
            SetActive(true);
            stage.NotifyCheckpoint(id, transform.position.x);
        }

        private void SetActive(bool value)
        {
            active = value;
            if (banner == null)
                return;
            banner.sharedMaterial = value ? activeMaterial : inactiveMaterial;
            var t = banner.transform;
            var p = t.localPosition;
            p.y = value ? bannerHighY : bannerLowY;
            t.localPosition = p;
        }
    }
}
