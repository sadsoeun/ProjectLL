using UnityEngine;

namespace MyGame
{
    /// <summary>One of the three special collectibles of a stage (slot 0..2 = index in the save record).</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class SpecialCollectible : MonoBehaviour, IStageElement
    {
        [SerializeField, Range(0, 2)] private int slot;
        [SerializeField] private Transform spin;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 120f;

        [Tooltip("Optional one-shot burst spawned (detached) at the pickup position on collect.")]
        [SerializeField] private GameObject collectFxPrefab;

        private StageRoot stage;
        private bool collected;

        public int Slot => slot;

        public void Bind(StageRoot owner) => stage = owner;
        public void ResetForRespawn() { }

        public void ResetForAttempt()
        {
            collected = false;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            if (spin != null)
                spin.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.Self);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || stage == null || !PlayerMotor.TryGetFromCollider(other, out _))
                return;
            collected = true;
            if (collectFxPrefab != null)
            {
                // Detached one-shot (the pickup itself is deactivated below); the FX prefab self-destroys via its ParticleSystem stop action.
                Instantiate(collectFxPrefab, transform.position, Quaternion.identity);
            }
            gameObject.SetActive(false);
            stage.NotifySpecial(slot);
        }
    }
}
