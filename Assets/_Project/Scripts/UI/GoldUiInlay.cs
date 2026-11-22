using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Charcoal panels, engraved borders and the cut-brass primary action.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GoldUiInlay : MaskableGraphic
    {
        public enum Surface { Window, Illustration, Monitor, Primary }
        private static readonly Color Brass = new Color(.88f,.68f,.37f,.68f);
        [SerializeField] private Surface surface;
        private float reveal = 1;

        public static GoldUiInlay Add(Transform parent, Surface surface)
        {
            var rect = UiKit.Rect(surface + "Inlay", parent);
            var existing = rect.GetComponent<GoldUiInlay>();
            if (existing != null) return existing;
            GoldUiTheme.Stretch(rect);
            var art = rect.gameObject.AddComponent<GoldUiInlay>();
            art.surface = surface;
            art.raycastTarget = false;
            return art;
        }

        public void SetReveal(float value)
        {
            if (Mathf.Approximately(reveal, value)) return;
            reveal = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            var r = rectTransform.rect;
            if (surface == Surface.Primary)
            {
                // Use the button's own ColorTint on the entire cut-brass plate.
                float cut = Mathf.Min(14, Mathf.Min(r.width,r.height)*.18f);
                var bright = Color.white;
                var dark = new Color(.76f,.68f,.54f);
                Strip(mesh, r.xMin, r.xMin+cut, r.yMin+cut, r.yMin, r.yMax-cut, r.yMax, dark, bright);
                Strip(mesh, r.xMin+cut, r.xMax-cut, r.yMin, r.yMin, r.yMax, r.yMax, bright, new Color(.88f,.83f,.73f));
                Strip(mesh, r.xMax-cut, r.xMax, r.yMin, r.yMin+cut, r.yMax, r.yMax-cut, new Color(.88f,.83f,.73f), dark);
                var ink = new Color(.32f,.24f,.12f,.7f);
                foreach (int side in Sides)
                {
                    if (r.width < 360) break;
                    var end = new Vector2(r.center.x + side*(r.width*.5f-28), r.center.y);
                    GoldUiFrame.Gem(mesh, end, 4, ink);
                    GoldUiFrame.Quad(mesh, side < 0 ? end.x+11 : end.x-43, end.y, 32, 1, ink);
                }
                return;
            }
            if (surface == Surface.Illustration)
            {
                var shade = new Color(.064f,.065f,.067f,.98f);
                var middle = new Color(shade.r,shade.g,shade.b,.88f);
                Strip(mesh,r.xMin,r.xMin+r.width*.40f,r.yMin,r.yMin,r.yMax,r.yMax,shade,middle);
                Strip(mesh,r.xMin+r.width*.40f,r.xMin+r.width*.75f,r.yMin,r.yMin,r.yMax,r.yMax,middle,Color.clear);
                if (reveal < 1)
                {
                    float x = Mathf.Lerp(r.xMin+35,r.xMax-35,reveal);
                    var gleam = new Color(1,.86f,.50f,Mathf.Sin(reveal*Mathf.PI)*.13f);
                    Strip(mesh,x-30,x,r.yMin,r.yMin,r.yMax,r.yMax,Color.clear,gleam);
                    Strip(mesh,x,x+30,r.yMin,r.yMin,r.yMax,r.yMax,gleam,Color.clear);
                }
            }
            else
            {
                bool window = surface == Surface.Window;
                var left = window ? new Color(.081f,.082f,.083f) : new Color(.091f,.089f,.084f);
                var right = window ? new Color(.061f,.063f,.067f) : new Color(.048f,.05f,.052f);
                Strip(mesh,r.xMin,r.xMax,r.yMin,r.yMin,r.yMax,r.yMax,left,right);
            }
            var fine = Brass;
            fine.a = surface == Surface.Window ? .42f : .57f;
            foreach (int side in Sides)
            {
                float y = side < 0 ? r.yMin+1 : r.yMax-2;
                GoldUiFrame.Quad(mesh, r.xMin+5, y, r.width-10, 1.5f, fine);
                foreach (int end in Sides)
                {
                    float edge = end < 0 ? r.xMin+2 : r.xMax-3;
                    GoldUiFrame.Quad(mesh, edge, side < 0 ? y : y-18, 1.4f, 18, Brass);
                    GoldUiFrame.Quad(mesh, end < 0 ? edge : edge-30, y, 30, 2, Brass);
                }
            }
        }

        private static readonly int[] Sides = { -1, 1 };

        private static void Strip(VertexHelper mesh, float x0, float x1, float bottom0, float bottom1, float top0, float top1, Color left, Color right)
        {
            int n = mesh.currentVertCount;
            mesh.AddVert(new Vector2(x0,bottom0),left,Vector2.zero);
            mesh.AddVert(new Vector2(x0,top0),left,Vector2.zero);
            mesh.AddVert(new Vector2(x1,top1),right,Vector2.zero);
            mesh.AddVert(new Vector2(x1,bottom1),right,Vector2.zero);
            mesh.AddTriangle(n,n+1,n+2);
            mesh.AddTriangle(n,n+2,n+3);
        }
    }
}
