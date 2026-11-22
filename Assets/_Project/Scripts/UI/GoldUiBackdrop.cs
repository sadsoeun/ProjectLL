using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GoldUiBackdrop : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            var dark = new Color(.026f,.026f,.029f,.93f);
            var clear = new Color(.026f,.026f,.029f,.06f);
            if (transform.parent != null && transform.parent.name == "LanguageCanvas")
            {
                dark = new Color(.029f,.029f,.031f);
                clear = new Color(.057f,.055f,.052f);
            }
            vh.AddVert(new Vector3(r.xMin,r.yMin),dark,Vector2.zero);
            vh.AddVert(new Vector3(r.xMin,r.yMax),dark,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMax),clear,Vector2.zero);
            vh.AddVert(new Vector3(r.xMax,r.yMin),clear,Vector2.zero);
            vh.AddTriangle(0,1,2); vh.AddTriangle(0,2,3);
        }
    }
}
