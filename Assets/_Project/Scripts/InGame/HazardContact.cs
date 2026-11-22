using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Trigger that kills the player on contact (spikes, fallers). Reports to the StageRoot, which forwards
    /// to the InGameController death flow; the controller ignores hits outside the Playing state.
    /// Stay is handled too so a player standing inside the trigger after a state change is still hit.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class HazardContact : MonoBehaviour, IStageElement
    {
        private StageRoot stage;

        public void Bind(StageRoot owner) => stage = owner;
        public void ResetForRespawn() { }
        public void ResetForAttempt() { }

        private void OnTriggerEnter(Collider other) => Hit(other);
        private void OnTriggerStay(Collider other) => Hit(other);

        private void Hit(Collider other)
        {
            if (stage != null && PlayerMotor.TryGetFromCollider(other, out _))
                stage.NotifyPlayerHit();
        }
    }
}
