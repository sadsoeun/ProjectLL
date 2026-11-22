using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Road between two nodes. Visible only when neither end is locked (spec 5.5).
    /// Route points run from the "from" node anchor to the "to" node anchor (world space, ground height baked in
    /// by the world map builder); the avatar walks them in order, or in reverse when travelling back.
    /// </summary>
    public sealed class WorldMapPath : MonoBehaviour
    {
        [SerializeField] private string fromNodeId;
        [SerializeField] private string toNodeId;
        [SerializeField] private Vector3[] routePoints = new Vector3[0];

        public string FromNodeId => fromNodeId;
        public string ToNodeId => toNodeId;
        public int PointCount => routePoints != null ? routePoints.Length : 0;

        public bool Connects(string a, string b) =>
            (fromNodeId == a && toNodeId == b) || (fromNodeId == b && toNodeId == a);

        /// <summary>Route points in travel order starting at <paramref name="startNodeId"/>. Empty when the path has no route.</summary>
        public Vector3[] GetRoute(string startNodeId)
        {
            if (routePoints == null || routePoints.Length == 0)
                return new Vector3[0];
            var route = (Vector3[])routePoints.Clone();
            if (startNodeId == toNodeId)
                System.Array.Reverse(route);
            return route;
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
                gameObject.SetActive(visible);
        }
    }
}
