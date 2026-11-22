using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadingShadeGraphic:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            for(int i=0;i<12;i++)
            {
                float a=i/12f,b=(i+1)/12f;int n=vh.currentVertCount;
                var ca=new Color(.02f,.02f,.022f,Mathf.Lerp(.95f,.43f,a));var cb=new Color(.02f,.02f,.022f,Mathf.Lerp(.95f,.43f,b));
                vh.AddVert(new Vector2(r.xMin,Mathf.Lerp(r.yMin,r.yMax,a)),ca,Vector2.zero);vh.AddVert(new Vector2(r.xMax,Mathf.Lerp(r.yMin,r.yMax,a)),ca,Vector2.zero);
                vh.AddVert(new Vector2(r.xMax,Mathf.Lerp(r.yMin,r.yMax,b)),cb,Vector2.zero);vh.AddVert(new Vector2(r.xMin,Mathf.Lerp(r.yMin,r.yMax,b)),cb,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
