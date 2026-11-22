using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadingTrailGraphic:MaskableGraphic
    {
        float time,progress;
        public void Sample(float seconds,float value){time=seconds;progress=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;float x=Mathf.Lerp(r.xMin,r.xMax,progress);
            GoldUiFrame.Quad(vh,r.xMin,-2,r.width,4,new Color(.06f,.065f,.07f,.95f));
            GoldUiFrame.Quad(vh,r.xMin,2,r.width,1,new Color(.72f,.57f,.30f,.42f));
            GoldUiFrame.Quad(vh,r.xMin,-3,r.width,1,new Color(.72f,.57f,.30f,.28f));
            if(progress<=0)return;
            float end=Mathf.Max(r.xMin,x-31);
            GoldUiFrame.Quad(vh,r.xMin,-1.2f,end-r.xMin,2.4f,new Color(.97f,.73f,.28f,1));
            float glowX=Mathf.Max(r.xMin,x-36);
            for(int i=8;i>=1;i--)Disc(vh,new Vector2(glowX,0),i*2.7f,new Color(1,.72f,.20f,.025f));
            Disc(vh,new Vector2(glowX,0),3,new Color(1,.96f,.72f,.95f));
            for(int i=0;i<9;i++)
            {
                float age=Mathf.Repeat(time*.40f+i*.137f,1);
                float px=x-44-age*150;
                if(px<r.xMin||px>r.xMax)continue;
                float py=5+age*(12+i%3*9)+Mathf.Sin(i*2.4f+time)*3;
                float alpha=(1-age)*.65f;
                Disc(vh,new Vector2(px,py),i%3==0?2.2f:1.3f,new Color(1,.88f,.47f,alpha));
            }
        }
        internal static void Disc(VertexHelper vh,Vector2 p,float radius,Color c)
        {
            int n=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);
            for(int i=0;i<=24;i++){float a=i*Mathf.PI*2/24;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c,Vector2.zero);if(i>0)vh.AddTriangle(n,n+i,n+i+1);}
        }
        internal static void Ring(VertexHelper vh,float radius,float width,Color c)
        {
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI/32,b=(i+1)*Mathf.PI/32;var u=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var v=new Vector2(Mathf.Cos(b),Mathf.Sin(b));int n=vh.currentVertCount;
                vh.AddVert(u*radius,c,Vector2.zero);vh.AddVert(v*radius,c,Vector2.zero);vh.AddVert(v*(radius-width),c,Vector2.zero);vh.AddVert(u*(radius-width),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
            }
        }
    }
}
