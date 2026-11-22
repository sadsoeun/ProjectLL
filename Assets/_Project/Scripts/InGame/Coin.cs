using UnityEngine;

namespace MyGame
{
    /// <summary>Collectible coin. Disappears on touch and stays collected after deaths; Retry brings it back.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class Coin : MonoBehaviour, IStageElement
    {
        [SerializeField] private Transform spin;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 180f;

        private StageRoot stage;
        private bool collected;

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
            gameObject.SetActive(false);
            stage.NotifyCoin();
        }
    }
}
