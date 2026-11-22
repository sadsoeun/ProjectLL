// Dodge afterimage: unlit translucent silhouette with a bright rim. A depth-only pre-pass keeps the ghost a clean
// silhouette (its own back/inner faces never show through). Tint alpha is driven per ghost by DodgeAfterimage.
Shader "MyGame/FX/Afterimage"
{
    Properties
    {
        [HDR] _BaseColor("Tint", Color) = (0.55, 0.85, 1, 0.5)
        [HDR] _RimColor("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower("Rim Power", Range(0.5, 8)) = 2.2
        _RimStrength("Rim Strength", Range(0, 2)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _RimColor;
            float _RimPower;
            float _RimStrength;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "DepthPrepass"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings Vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half4 Frag(Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float ndv = saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                half rim = pow(1.0 - ndv, _RimPower) * _RimStrength;
                half3 color = _BaseColor.rgb + _RimColor.rgb * rim;
                half alpha = saturate(_BaseColor.a * (0.8 + rim));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
