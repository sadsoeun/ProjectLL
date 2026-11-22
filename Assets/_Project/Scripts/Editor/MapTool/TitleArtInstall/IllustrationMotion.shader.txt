Shader "MyGame/TitleIllustrationMotion"
{
    Properties
    {
        [PerRendererData] _MainTex ("Illustration", 2D) = "white" {}
        _MotionTime ("Motion Time", Float) = 0
        _Wind ("Wind", Range(0,2)) = 1.3
        _ViewAspect ("View Aspect", Float) = 1.777778
        _ImageAspect ("Image Aspect", Float) = 1.777778
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex;
            float _MotionTime, _Wind, _ViewAspect, _ImageAspect;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float zone(float2 p,float4 z) { float2 d=(p-z.xy)/z.zw; float f=max(0,1-dot(d,d)); return f*f; }
            float2 turn(float2 p,float2 root,float angle)
            {
                float2 q=(p-root)*float2(_ImageAspect,1);
                float s=sin(angle),c=cos(angle);
                return root+float2(c*q.x-s*q.y,s*q.x+c*q.y)/float2(_ImageAspect,1);
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 p=float2(i.uv.x,1-i.uv.y);
                float rx=min(1,_ViewAspect/_ImageAspect), ry=min(1,_ImageAspect/_ViewAspect);
                p=p*float2(rx,ry)+float2((1-rx)*.76,(1-ry)*.5);
                float body=zone(p,float4(.707,.572,.105,.180));
                float t=_MotionTime;
                float2 uv=p;
                // Root-pinned angular swing: broad masks extend beyond the silhouette,
                // so the ends travel through space instead of rippling inside the hair.
                float right=smoothstep(.755,.80,p.x)*(1-smoothstep(.94,.975,p.x));
                right*=smoothstep(.345,.40,p.y)*(1-smoothstep(.72,.77,p.y));
                // Protect the foreground sleeve and hand from the rear-hair motion.
                float hand=1-smoothstep(.70,1.0,length((p-float2(.795,.565))/float2(.064,.095)));
                right*=1-hand;
                float angle=_Wind*(.060*sin(t*1.45)+.018*sin(t*2.17-.7));
                float tip=smoothstep(.80,.91,p.x);
                angle+=_Wind*.025*sin(t*1.45-.8)*tip;
                uv+=right*(turn(p,float2(.765,.345),angle)-p);
                float left=smoothstep(.577,.602,p.x)*(1-smoothstep(.642,.667,p.x));
                left*=smoothstep(.36,.405,p.y)*(1-smoothstep(.53,.565,p.y));
                uv+=left*(turn(p,float2(.677,.345),_Wind*.085*sin(t*1.45-.55))-p);
                uv.y-=_Wind*.0011*sin(t*1.6)*body;
                float grass=smoothstep(.77,.94,p.y)*smoothstep(.45,.62,p.x);
                uv.x-=_Wind*.0008*sin(t*1.15+p.x*26)*grass;
                return tex2D(_MainTex,float2(saturate(uv.x),1-saturate(uv.y)))*i.color;
            }
            ENDCG
        }
    }
}
