using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    /// <summary>Full-height backdrop: dark under result text, transparent toward the character.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ClearResultScrim:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            float[] stops={0,.36f,.55f,.72f,1};float[] alpha={.78f,.76f,.30f,.04f,.04f};
            for(int j=0;j<stops.Length-1;j++)
            {
                int i=vh.currentVertCount;float x0=r.xMin+r.width*stops[j],x1=r.xMin+r.width*stops[j+1];
                var a=new Color(.012f,.02f,.03f,alpha[j]);var b=new Color(.012f,.02f,.03f,alpha[j+1]);
                vh.AddVert(new Vector3(x0,r.yMin),a,Vector2.zero);vh.AddVert(new Vector3(x0,r.yMax),a,Vector2.zero);
                vh.AddVert(new Vector3(x1,r.yMax),b,Vector2.zero);vh.AddVert(new Vector3(x1,r.yMin),b,Vector2.zero);
                vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
            }
        }
    }
}
