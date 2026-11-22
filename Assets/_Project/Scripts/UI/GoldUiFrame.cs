using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Lightweight gold linework shared by menus, cards and HUD. No texture allocation.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GoldUiFrame : MaskableGraphic
    {
        public bool diamond = true;
        public float inset = 3f;
        private float focus, sweep, press;
        public void SetMotion(float focusAmount, float sweepPosition, float pressAmount)
        {
            if(Mathf.Approximately(focus,focusAmount)&&Mathf.Approximately(sweep,sweepPosition)&&Mathf.Approximately(press,pressAmount))return;
            focus=Mathf.Clamp01(focusAmount);sweep=Mathf.Clamp01(sweepPosition);press=Mathf.Clamp01(pressAmount);SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = rectTransform.rect;
            float x = r.xMin + inset, y = r.yMin + inset;
            float w = r.width - inset * 2, h = r.height - inset * 2;
            if (w <= 0 || h <= 0) return;
            Color fine = color; fine.a *= .36f;
            Quad(vh, x, y, w, 1, fine); Quad(vh, x, y + h - 1, w, 1, fine);
            float arm = Mathf.Min(24, Mathf.Min(w,h)*.22f);
            for(int corner=0;corner<4;corner++)
            {
                bool left=(corner&1)==0, bottom=(corner&2)==0;
                Quad(vh,left?x:x+w-arm,bottom?y:y+h-1.5f,arm,1.5f,color);
                Quad(vh,left?x:x+w-1,bottom?y:y+h-arm*.65f,1,arm*.65f,color);
            }
            if (diamond && w > 250 && h > 100)
            {
                Gem(vh, new Vector2(r.center.x, y + h), 4, color);
                Gem(vh, new Vector2(r.center.x, y), 4, color);
            }
            if(focus>.001f)
            {
                Color accent=new Color(1f,.91f,.64f,focus*.9f);
                float length=(w-24)*focus;
                Quad(vh,r.center.x-length*.5f,y+5,length,2+press,accent);
                float glint=Mathf.Sin(sweep*Mathf.PI)*focus;
                if(glint>.001f)
                {
                    float tip=x+8+(w-16)*sweep;
                    for(int i=0;i<8;i++)
                    {
                        float start=Mathf.Max(x+4,tip-6*(i+1)),end=Mathf.Min(x+w-4,tip-6*i);
                        if(end>start)Quad(vh,start,y+h-3,end-start,3,new Color(1,.96f,.8f,glint*(1-i/8f)));
                    }
                }
                if(w>250){Gem(vh,new Vector2(x+13,r.center.y),3*focus,accent);Gem(vh,new Vector2(x+w-13,r.center.y),3*focus,accent);}
            }
        }

        internal static void Quad(VertexHelper vh, float x, float y, float w, float h, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector3(x, y), c, Vector2.zero);
            vh.AddVert(new Vector3(x, y + h), c, Vector2.zero);
            vh.AddVert(new Vector3(x + w, y + h), c, Vector2.zero);
            vh.AddVert(new Vector3(x + w, y), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }

        internal static void Gem(VertexHelper vh, Vector2 p, float s, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(p + Vector2.up * s, c, Vector2.zero);
            vh.AddVert(p + Vector2.right * s, c, Vector2.zero);
            vh.AddVert(p + Vector2.down * s, c, Vector2.zero);
            vh.AddVert(p + Vector2.left * s, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
