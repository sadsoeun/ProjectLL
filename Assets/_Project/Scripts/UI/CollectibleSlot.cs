using UnityEngine;
using UnityEngine.UI;
namespace MyGame
{
    /// <summary>Golden leaf when collected, cream outline when missing; not color alone.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CollectibleSlot:MaskableGraphic
    {
        private bool filled;
        public void SetFilled(bool value){filled=value;color=value?HudColors.SpecialFull:GoldUiTheme.Cream;SetVerticesDirty();}
        public static void Set(Image image,bool value)
        {
            var rect=UiKit.Rect("SlotShape",image.transform);GoldUiTheme.Stretch(rect);
            // Legacy slots are rotated squares. Keep their layout but cancel that rotation.
            rect.localRotation=Quaternion.Inverse(image.transform.localRotation);
            var slot=UiKit.Ensure<CollectibleSlot>(rect);slot.raycastTarget=false;image.enabled=false;slot.SetFilled(value);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();float size=Mathf.Min(r.width,r.height);
            var p=GoldenLeafShape.Outline;var center=r.center+GoldenLeafShape.Center*size;
            if(filled)for(int i=0;i<p.Length;i++)
            {
                int n=vh.currentVertCount;var shade=Color.Lerp(color,new Color(1f,.91f,.53f,color.a),i<4?.25f:.05f);
                vh.AddVert(center,shade,Vector2.zero);vh.AddVert(r.center+p[i]*size,color,Vector2.zero);
                vh.AddVert(r.center+p[(i+1)%p.Length]*size,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);
            }
            var outline=filled?new Color(.63f,.36f,.08f,color.a):new Color(color.r,color.g,color.b,.6f*color.a);
            for(int i=0;i<p.Length;i++)Line(vh,r.center+p[i]*size,r.center+p[(i+1)%p.Length]*size,Mathf.Max(1.2f,size*.045f),outline);
            var veins=GoldenLeafShape.Veins;
            var veinColor=filled?new Color(1f,.96f,.69f,color.a):outline;
            for(int i=0;i<(filled?veins.Length:6);i+=2)
                Line(vh,r.center+veins[i]*size,r.center+veins[i+1]*size,Mathf.Max(1f,size*.035f),veinColor);
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c)
        {
            var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;int i=vh.currentVertCount;
            vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
