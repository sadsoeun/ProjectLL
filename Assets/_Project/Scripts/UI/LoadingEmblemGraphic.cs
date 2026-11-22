using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LoadingEmblemGraphic:MaskableGraphic
    {
        float time;
        public void Sample(float seconds){time=seconds;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();float r=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.48f;
            LoadingTrailGraphic.Disc(vh,Vector2.zero,r,new Color(.035f,.047f,.047f,.98f));
            LoadingTrailGraphic.Ring(vh,r,1.3f,new Color(.91f,.72f,.36f,.95f));
            LoadingTrailGraphic.Ring(vh,r-4,.8f,new Color(.91f,.72f,.36f,.4f));
            // Six small leaves orbit a fixed bright center, echoing the reference's unfolding seal.
            for(int i=0;i<6;i++)
            {
                float angle=(i*60+time*32)*Mathf.Deg2Rad;
                var d=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));var n=new Vector2(-d.y,d.x);
                float open=.88f+.12f*Mathf.Sin(time*1.5f+i*.5f);int k=vh.currentVertCount;
                var c=new Color(.96f,.77f,.34f,1);
                vh.AddVert(d*r*.13f,c,Vector2.zero);vh.AddVert(d*r*.44f+n*r*.17f*open,c,Vector2.zero);
                vh.AddVert(d*r*.76f,c,Vector2.zero);vh.AddVert(d*r*.44f-n*r*.12f*open,new Color(.72f,.49f,.17f,1),Vector2.zero);
                vh.AddTriangle(k,k+1,k+2);vh.AddTriangle(k,k+2,k+3);
            }
            LoadingTrailGraphic.Disc(vh,Vector2.zero,r*.065f,new Color(1,.94f,.66f));
        }
    }
}
