using UnityEngine;
using UnityEngine.UI;

namespace MyGame
{
    /// <summary>Subdivides the transparent explorer illustration; only hair and outer cape tips receive wind weights.</summary>
    [RequireComponent(typeof(RawImage))]
    public sealed class TitleHairWind : BaseMeshEffect
    {
        float time;
        [SerializeField, Range(0f, 1f)] float capeWindStrength = 1f;
        const int Columns = 80, Rows = 128;

        public void Sample(float seconds)
        {
            if (Mathf.Approximately(time, seconds)) return;
            time = seconds;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        // Coordinates use the original sprite, top-left origin. Face, eyes, shoulders, sleeves, torso and feet
        // intentionally have zero weights; eyelid overlays never need to follow this mesh.
        public static Vector2 Offset(Vector2 uvFromTop, float seconds, float capeStrength = 1f)
        {
            float x=uvFromTop.x, y=uvFromTop.y;
            float sway=Mathf.Sin(seconds*1.45f)+.25f*Mathf.Sin(seconds*2.17f-.7f);
            float tip=Smooth(.16f,.29f,y)*(1-Smooth(.325f,.350f,y));
            float left=Smooth(.215f,.27f,x)*(1-Smooth(.397f,.430f,x))*tip;
            float right=Smooth(.673f,.709f,x)*(1-Smooth(.80f,.84f,x))*tip;
            var offset=new Vector2(
                .010f*left*sway + .009f*right*(Mathf.Sin(seconds*1.45f-.55f)+.22f*Mathf.Sin(seconds*2.17f-1)),
                .0018f*(left+right)*Mathf.Sin(seconds*1.45f-.8f));
            float sprout=Smooth(.285f,.32f,x)*(1-Smooth(.54f,.57f,x))*(1-Smooth(.075f,.112f,y));
            Vector2 pivot=new Vector2(.505f,.107f);
            var q=new Vector2((x-pivot.x)*1024,(y-pivot.y)*1536);
            float angle=.055f*Mathf.Sin(seconds*1.45f-.32f);
            var rotated=new Vector2(Mathf.Cos(angle)*q.x-Mathf.Sin(angle)*q.y,Mathf.Sin(angle)*q.x+Mathf.Cos(angle)*q.y);
            var d=rotated-q;
            offset+=sprout*new Vector2(d.x/1024,d.y/1536);
            return offset + CapeOffset(uvFromTop, seconds) * capeStrength;
        }
        // Keep the moving mesh cells outside the cream sleeves. The cape fastening and
        // shoulder line are pinned; only the unsupported outer hems catch the breeze.
        public static Vector2 CapeOffset(Vector2 uvFromTop, float seconds)
        {
            float x=uvFromTop.x, y=uvFromTop.y;
            float hem=Smooth(.390f,.435f,y)*(1-Smooth(.458f,.480f,y));
            float left=(1-Smooth(.285f,.307f,x))*hem;
            float right=Smooth(.668f,.690f,x)*hem;
            float wave=Mathf.Sin(seconds*1.45f-.70f)+.18f*Mathf.Sin(seconds*2.17f-1.20f);
            float trailing=Mathf.Sin(seconds*1.45f-1.10f)+.18f*Mathf.Sin(seconds*2.17f-1.60f);
            return new Vector2(.008f*left*wave+.006f*right*trailing,
                .0028f*left*Mathf.Sin(seconds*1.45f-1.20f)
                +.0022f*right*Mathf.Sin(seconds*1.45f-1.60f));
        }
        static float Smooth(float a,float b,float x){float v=Mathf.Clamp01((x-a)/(b-a));return v*v*(3-2*v);}

        public override void ModifyMesh(VertexHelper vh)
        {
            if(!IsActive())return;
            var raw=(RawImage)graphic; var rect=graphic.GetPixelAdjustedRect(); var uv=raw.uvRect;
            vh.Clear();
            for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)
            {
                float u=x/(float)Columns,v=y/(float)Rows;
                var offset=Offset(new Vector2(u,1-v),time,capeWindStrength);
                var vertex=UIVertex.simpleVert;
                vertex.position=new Vector3(rect.xMin+(u+offset.x)*rect.width,rect.yMin+(v-offset.y)*rect.height,0);
                vertex.color=graphic.color;
                vertex.uv0=new Vector2(uv.x+u*uv.width,uv.y+v*uv.height);
                vh.AddVert(vertex);
            }
            for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
            {
                int i=y*(Columns+1)+x;
                vh.AddTriangle(i,i+Columns+1,i+1);
                vh.AddTriangle(i+1,i+Columns+1,i+Columns+2);
            }
        }
    }
}

