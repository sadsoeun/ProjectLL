using UnityEngine;
using UnityEngine.Assertions;

namespace MyGame
{
    /// <summary>Trigger volume at the end of a stage. Notifies the StageRoot when the player enters.</summary>
    [RequireComponent(typeof(Collider))]
    public sealed class GoalTrigger : MonoBehaviour
    {
        [SerializeField] private StageRoot stage;

        [Header("Stage clear walk-in")]
        [Tooltip("Optional child transform marking where the player stands for the clear pose (feet position). Overrides the offset.")]
        [SerializeField] private Transform celebrationPoint;
        [Tooltip("Feet position of the clear pose relative to this object (world units, no scaling). Default: the gate centre on the ground (the Goal root sits 1.5 above the surface).")]
        [SerializeField] private Vector3 celebrationOffset = new Vector3(0f, -1.5f, 0f);

        /// <summary>World position where the player's feet should stand for the stage-clear pose (Z = player lane).</summary>
        public Vector3 CelebrationPosition
        {
            get
            {
                Vector3 p = celebrationPoint != null ? celebrationPoint.position : transform.position + celebrationOffset;
                p.z = 0f;
                return p;
            }
        }

        private void Awake()
        {
            Assert.IsNotNull(stage, "GoalTrigger.stage is not assigned.");
            Assert.IsTrue(GetComponent<Collider>().isTrigger, "GoalTrigger collider must be a trigger.");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMotor>() != null)
                stage.NotifyGoal();
        }
    }
}
