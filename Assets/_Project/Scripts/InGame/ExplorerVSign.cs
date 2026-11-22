using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Swaps the Leaf Explorer's right fist for the V-sign hand (LeafExplorer_VHandR) during the stage-clear pose.
    /// The fist and the V hand are separate skinned renderers weighted to Hand.R, so the swap is a renderer toggle.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ExplorerVSign : MonoBehaviour
    {
        [SerializeField] private Renderer fist;
        [SerializeField] private Renderer vHand;

        private void Awake()
        {
            if (fist == null || vHand == null)
            {
                foreach (var r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "LeafExplorer_FistR") fist = r;
                    else if (r.name == "LeafExplorer_VHandR") vHand = r;
                }
            }
            Set(false);
        }

        public void Set(bool vSign)
        {
            if (vHand != null) vHand.enabled = vSign;
            if (fist != null) fist.enabled = !vSign;
        }
    }
}
