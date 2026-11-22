using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Resolution-independent simple HUD symbols, with no font-glyph dependency.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudGlyph : MaskableGraphic
    {
        public enum Kind { Pause, Clock }
        public Kind kind;
        protected override void Awake() { base.Awake(); raycastTarget=false; color=new Color(1,.95f,.84f); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Rect r=rectTransform.rect;
            if(kind==Kind.Pause)
            {
                GoldUiFrame.Quad(vh,r.xMin,r.yMin,r.width*.28f,r.height,color);
                GoldUiFrame.Quad(vh,r.xMax-r.width*.28f,r.yMin,r.width*.28f,r.height,color);
                return;
            }
            float radius=Mathf.Min(r.width,r.height)*.39f;
            Vector2 center=r.center+Vector2.down*2;
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40,b=(i+1)*Mathf.PI*2/40;
                Line(vh,center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,2.5f);
            }
            Line(vh,center,center+Vector2.up*radius*.65f,2.5f);
            Line(vh,center,center+Vector2.right*radius*.45f,2.5f);
            GoldUiFrame.Quad(vh,center.x-4,r.yMax-2,8,2,color);
        }
        private void Line(VertexHelper vh,Vector2 a,Vector2 b,float width)
        {
            Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;int i=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);
            vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
