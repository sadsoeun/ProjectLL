using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StageLockGraphic:MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var gold=GoldUiTheme.Gold;
            GoldUiFrame.Quad(vh,-25,-28,50,39,gold);
            GoldUiFrame.Quad(vh,-20,-23,40,29,new Color(.10f,.09f,.065f));
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;
                Segment(vh,new Vector2(Mathf.Cos(a)*17,12+Mathf.Sin(a)*17),new Vector2(Mathf.Cos(b)*17,12+Mathf.Sin(b)*17),5,gold);
            }
            GoldUiFrame.Quad(vh,-19.5f,5,5,8,gold);GoldUiFrame.Quad(vh,14.5f,5,5,8,gold);
            GoldUiFrame.Gem(vh,new Vector2(0,-4),5,GoldUiTheme.Cream);
            GoldUiFrame.Quad(vh,-2,-16,4,11,GoldUiTheme.Cream);
        }
        static void Segment(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {
            var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;int i=vh.currentVertCount;
            vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
