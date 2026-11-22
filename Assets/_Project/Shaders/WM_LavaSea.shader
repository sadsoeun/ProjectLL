// Stylised cel lava sea matching the Ember Fortress key art: bright orange molten surface split into rounded, slowly
// churning cells; amber-red seams between cells, pale-yellow hot cores, a few cooled plum crust islands with glowing rims.
// Self-lit (emission only, so shadows and the sun never dull it); distance fog still applies. World-space, needs no UVs.
Shader "MyGame/Env/LavaSea"
{
    Properties
    {
        _SeamColor("Seam (cell border)", Color) = (0.86, 0.27, 0.05, 1)
        _MoltenColor("Molten", Color) = (1, 0.52, 0.1, 1)
        _HotColor("Hot Core", Color) = (1, 0.8, 0.3, 1)
        _CrustColor("Cooled Crust", Color) = (0.36, 0.22, 0.27, 1)
        _Brightness("Brightness", Range(0.2, 2)) = 1
        _CellScale("Cell Scale (cells per m)", Range(0.05, 2)) = 0.42
        _SeamWidth("Seam Width", Range(0.01, 0.4)) = 0.09
        _HotSize("Hot Core Size", Range(0, 1)) = 0.32
        _SwirlScale("Swirl Scale", Range(0.01, 1)) = 0.09
        _SwirlStrength("Swirl Strength", Range(0, 3)) = 1.1
        _CrustAmount("Crust Islands", Range(0, 1)) = 0.18
        _FlowDir("Flow Direction (xz)", Vector) = (1, 0.25, 0, 0)
        _FlowSpeed("Flow Speed", Range(0, 2)) = 0.12
        _ChurnSpeed("Cell Churn Speed", Range(0, 2)) = 0.35
        _PulseSpeed("Glow Pulse Speed", Range(0, 6)) = 1.3
        _HeaveHeight("Heave Height (m)", Range(0, 0.3)) = 0.03
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "WMAmbientCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _SeamColor;
            half4 _MoltenColor;
            half4 _HotColor;
            half4 _CrustColor;
            float _Brightness;
            float _CellScale;
            float _SeamWidth;
            float _HotSize;
            float _SwirlScale;
            float _SwirlStrength;
            float _CrustAmount;
            float4 _FlowDir;
            float _FlowSpeed;
            float _ChurnSpeed;
            float _PulseSpeed;
            float _HeaveHeight;
        CBUFFER_END

        void WMDisplace(inout float3 positionWS, inout float3 normalWS, float3 positionOS, float4 color, float2 uv)
        {
            positionWS.y += sin(_Time.y * 0.7 + positionWS.x * 0.35 + positionWS.z * 0.27) * _HeaveHeight;
        }

        // F1 distance and F2 - F1 border distance of animated cells, plus the nearest cell's random id.
        void LavaCells(float2 p, float time, out float f1, out float edge, out float id)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f1 = 8.0;
            float f2 = 8.0;
            id = 0.0;
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                float2 g = float2(x, y);
                float2 o = WMHash22(i + g);
                float2 c = 0.5 + 0.38 * sin(time + 6.2831 * o);
                float d = length(g + c - f);
                if (d < f1) { f2 = f1; f1 = d; id = o.x; }
                else if (d < f2) { f2 = d; }
            }
            edge = f2 - f1;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #define WM_PASS_FORWARD
            #include "WMAmbientPasses.hlsl"

            void WMSurface(WMVaryings input, out half3 albedo, out half alpha, out half3 emission, inout half3 normalWS)
            {
                float t = _Time.y;
                float2 flow = normalize(_FlowDir.xy + 1e-4) * t * _FlowSpeed;
                float2 w = input.positionWS.xz;

                // large slow swirl warps the cell field so the cells stretch along lazy flowing currents
                float2 sp = w * _SwirlScale + flow * 0.4;
                float2 swirl = float2(WMFbm(sp), WMFbm(sp + 7.3)) - 0.5;
                float2 p = (w + swirl * _SwirlStrength / max(_SwirlScale, 1e-3) * 0.12) * _CellScale + flow * _CellScale;

                float f1, edge, id;
                LavaCells(p, t * _ChurnSpeed, f1, edge, id);

                // anti-aliased cel bands: seam -> molten -> hot core
                float aa = fwidth(edge) * 1.2 + 1e-4;
                float seam = 1.0 - smoothstep(_SeamWidth - aa, _SeamWidth + aa, edge);
                float aaC = fwidth(f1) * 1.2 + 1e-4;
                float hotSize = _HotSize * (0.75 + 0.5 * id);
                float hot = 1.0 - smoothstep(hotSize - aaC, hotSize + aaC, f1);
                // thin bright rim just inside each seam (the "amber outline" of the key art cells)
                float rim = (1.0 - smoothstep(_SeamWidth * 2.2 - aa, _SeamWidth * 2.2 + aa, edge)) * (1.0 - seam);

                half3 col = _MoltenColor.rgb;
                col = lerp(col, lerp(_MoltenColor.rgb, _HotColor.rgb, 0.45), rim * 0.6);
                col = lerp(col, _HotColor.rgb, hot);
                col = lerp(col, _SeamColor.rgb, seam);

                // sparse cooled crust islands (low-frequency mask), with a glowing orange rim
                float crustN = WMFbm(w * 0.045 + flow * 0.05 + 31.7);
                float crustT = 1.0 - _CrustAmount * 0.55;
                float aaK = fwidth(crustN) * 1.5 + 1e-4;
                float crust = smoothstep(crustT - aaK, crustT + aaK, crustN);
                float crustRim = smoothstep(crustT - 0.05, crustT, crustN) * (1.0 - crust);
                col = lerp(col, _HotColor.rgb, crustRim * 0.7);
                half3 crustCol = _CrustColor.rgb * (0.85 + 0.3 * WMValueNoise(w * 1.7));
                col = lerp(col, crustCol, crust);

                // gentle breathing glow, out of phase per cell
                float pulse = 0.92 + 0.08 * sin(t * _PulseSpeed + id * 6.2831);
                albedo = half3(0, 0, 0);
                alpha = 1.0;
                emission = col * _Brightness * lerp(pulse, 1.0, crust);
            }
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile_instancing
            #define WM_PASS_DEPTH
            #include "WMAmbientPasses.hlsl"
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex WMVert
            #pragma fragment WMFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #define WM_PASS_DEPTHNORMALS
            #include "WMAmbientPasses.hlsl"
            #include "WMAmbientFrag.hlsl"
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
