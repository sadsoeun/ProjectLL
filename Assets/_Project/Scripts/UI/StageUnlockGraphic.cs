using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class StageUnlockGraphic:MaskableGraphic
    {
        float time;static readonly Vector2 Center=new Vector2(0,66);
        public void Sample(float seconds){time=seconds;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float reveal=Mathf.SmoothStep(0,1,time/.65f);float burst=Mathf.Exp(-Mathf.Pow((time-.30f)/.16f,2));
            for(int i=10;i>=1;i--)Disc(vh,Center,i*(13+burst*3),new Color(1,.66f,.19f,.007f+burst*.014f));
            float radius=Mathf.Lerp(16,116,1-Mathf.Pow(1-reveal,3));
            Disc(vh,Center,radius-6,new Color(.025f,.032f,.035f,.96f*reveal));
            Arc(vh,radius,1.8f,0,360*reveal,new Color(.96f,.77f,.39f,reveal));
            Arc(vh,radius-9,.8f,0,360*reveal,new Color(.76f,.56f,.27f,reveal));
            Arc(vh,radius+13,1.1f,time*24,112*reveal,new Color(1,.88f,.61f,.75f*reveal));
            Arc(vh,radius+13,1.1f,180+time*24,112*reveal,new Color(1,.88f,.61f,.75f*reveal));
            Arc(vh,radius+24,.6f,-time*18+35,70*reveal,new Color(.84f,.65f,.32f,.35f*reveal));
            Arc(vh,radius+24,.6f,-time*18+215,70*reveal,new Color(.84f,.65f,.32f,.35f*reveal));
            for(int side=-1;side<=1;side+=2)
            {
                Line(vh,Center+new Vector2(side*158,0),Center+new Vector2(side*(158+230*reveal),0),.7f,new Color(.86f,.65f,.30f,.46f*reveal));
                Diamond(vh,Center+new Vector2(side*160,0),4,new Color(1,.84f,.48f,reveal));
            }
            Diamond(vh,Center+Vector2.up*(radius+15),5,new Color(1,.88f,.58f,reveal));
            float spread=Mathf.SmoothStep(0,1,(time-.2f)/1.1f);
            for(int i=0;i<10;i++)
            {
                float a=(i*36+time*8)*Mathf.Deg2Rad;float rr=145+spread*(65+i%3*32);
                var p=Center+new Vector2(Mathf.Cos(a)*rr*1.25f,Mathf.Sin(a)*rr*.68f);
                p.y=Mathf.Max(p.y,-58f);
                Leaf(vh,p,13+i%3*3,a+.6f,new Color(1,.79f,.36f,(.35f+i%3*.13f)*reveal));
            }
            for(int i=0;i<28;i++)
            {
                float a=i*2.39996f;float rr=135+(i%7)*31+spread*25;
                var p=Center+new Vector2(Mathf.Cos(a)*rr*1.3f,Mathf.Sin(a)*rr*.70f+Mathf.Sin(time+i)*6);
                p.y=Mathf.Max(p.y,-66f);
                float alpha=(.16f+.34f*(.5f+.5f*Mathf.Sin(time*1.5f+i)))*reveal;
                Diamond(vh,p,i%5==0?2.8f:1.2f,new Color(1,.90f,.61f,alpha));
            }
        }
        static void Leaf(VertexHelper vh,Vector2 p,float size,float angle,Color color)
        {
            var points=GoldenLeafShape.Outline;int n=vh.currentVertCount;vh.AddVert(p,color,Vector2.zero);
            for(int i=0;i<points.Length;i++){var q=points[i]*size;var rotated=new Vector2(q.x*Mathf.Cos(angle)-q.y*Mathf.Sin(angle),q.x*Mathf.Sin(angle)+q.y*Mathf.Cos(angle));vh.AddVert(p+rotated,color,Vector2.zero);}
            for(int i=0;i<points.Length;i++)vh.AddTriangle(n,n+i+1,n+(i+1)%points.Length+1);
        }
        static void Diamond(VertexHelper vh,Vector2 p,float r,Color c)
        {int n=vh.currentVertCount;vh.AddVert(p+Vector2.up*r,c,Vector2.zero);vh.AddVert(p+Vector2.right*r*.6f,c,Vector2.zero);vh.AddVert(p+Vector2.down*r,c,Vector2.zero);vh.AddVert(p+Vector2.left*r*.6f,c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        static void Disc(VertexHelper vh,Vector2 p,float radius,Color c)
        {int n=vh.currentVertCount;vh.AddVert(p,c,Vector2.zero);for(int i=0;i<=64;i++){float a=i*Mathf.PI/32;vh.AddVert(p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,c,Vector2.zero);if(i>0)vh.AddTriangle(n,n+i,n+i+1);}}
        static void Arc(VertexHelper vh,float radius,float width,float start,float span,Color c)
        {for(int i=0;i<96;i++){float a=(start+span*i/96)*Mathf.Deg2Rad,b=(start+span*(i+1)/96)*Mathf.Deg2Rad;Line(vh,Center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,Center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,c);}}
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {var d=(b-a).normalized;var o=new Vector2(-d.y,d.x)*width*.5f;int n=vh.currentVertCount;vh.AddVert(a-o,c,Vector2.zero);vh.AddVert(a+o,c,Vector2.zero);vh.AddVert(b+o,c,Vector2.zero);vh.AddVert(b-o,c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
    }
}
