Shader "LeafAndLeap/ExplorerCel"
{
 Properties { _MainTex("Character texture",2D)="white" {} _Color("Tint",Color)=(1,1,1,1) }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass {
   Name "ExplorerForward"
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
   CBUFFER_START(UnityPerMaterial)
   float4 _MainTex_ST; half4 _Color;
   CBUFFER_END
   struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
   struct Varyings {float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float2 uv:TEXCOORD1;};
   Varyings vert(Attributes v){Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=TRANSFORM_TEX(v.uv,_MainTex);return o;}
   half4 frag(Varyings i):SV_Target {half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*_Color;half n=dot(normalize(i.normalWS),normalize(float3(.35,.75,.55)));half shade=lerp(.78,1.0,smoothstep(.05,.18,n));return half4(c.rgb*shade,1);}
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
 }
 SubShader {
  Tags { "RenderType"="Opaque" "Queue"="Geometry" }
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   sampler2D _MainTex; float4 _MainTex_ST; fixed4 _Color;
   struct input { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; };
   struct output { float4 pos:SV_POSITION; float2 uv:TEXCOORD0; float3 normal:TEXCOORD1; };
   output vert(input v){output o;o.pos=UnityObjectToClipPos(v.vertex);o.uv=TRANSFORM_TEX(v.uv,_MainTex);o.normal=UnityObjectToWorldNormal(v.normal);return o;}
   fixed4 frag(output i):SV_Target { fixed4 c=tex2D(_MainTex,i.uv)*_Color;float n=dot(normalize(i.normal),normalize(float3(.35,.75,.55)));float shade=lerp(.78,1.0,smoothstep(.05,.18,n));return fixed4(c.rgb*shade,1); }
   ENDCG
  }
 }
}

