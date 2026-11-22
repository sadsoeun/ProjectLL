using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Receives the SD Unity-chan clips' "OnCallChangeFace" animation events and cross-fades the face layer
    /// to the state with that name (e.g. "smile@sd_hmd"). Lives on the model next to its Animator.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterFaceEvents : MonoBehaviour
    {
        [SerializeField] private string faceLayerName = "Face";
        [SerializeField, Min(0f)] private float crossFadeSeconds = 0.1f;

        private Animator animator;
        private int faceLayer = -1;
        private LeafExplorerFace explorerFace;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            explorerFace = GetComponentInChildren<LeafExplorerFace>(true); // Leaf Explorer: overlay expressions instead of a face layer
            faceLayer = animator.GetLayerIndex(faceLayerName);
            // A controller without a face layer (e.g. the title idle controller) is valid: face events are then ignored.
        }

        // Called by animation events.
        public void OnCallChangeFace(string faceName)
        {
            if (explorerFace != null)
            {
                explorerFace.SetExpression(faceName);
                return;
            }
            int hash = Animator.StringToHash(faceName);
            if (faceLayer >= 0 && animator.HasState(faceLayer, hash))
                animator.CrossFadeInFixedTime(hash, crossFadeSeconds, faceLayer);
        }
    }
}
