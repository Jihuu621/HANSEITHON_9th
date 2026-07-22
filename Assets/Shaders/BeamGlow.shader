Shader "HANSEITHON/BeamGlow"
{
    Properties
    {
        [HDR] _TintColor ("Tint Color", Color) = (1, 0.88, 0.55, 1)
        _BeamOpacity ("Beam Opacity", Range(0, 1)) = 0.42
        _Intensity ("Intensity", Float) = 2.2
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.49)) = 0.12
        [NoScaleOffset] _OcclusionTex ("Occlusion Profile", 2D) = "white" {}
        [NoScaleOffset] _ColorProfileTex ("Color Conversion Profile", 2D) = "black" {}
        _OcclusionSoftness ("Occlusion Softness", Range(0.001, 0.05)) = 0.003
        _ColorTransitionSoftness ("Color Transition Softness", Range(0.0001, 0.03)) = 0.002
        _ShadowGlow ("Shadow Glow", Range(0, 0.25)) = 0.08
        _ShadowGlowFalloff ("Shadow Glow Falloff", Range(0, 8)) = 2.5
        _DistanceFade ("Distance Fade", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "BeamGlow"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_OcclusionTex);
            SAMPLER(sampler_OcclusionTex);
            TEXTURE2D(_ColorProfileTex);
            SAMPLER(sampler_ColorProfileTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float transverse : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _BeamOpacity;
                float _Intensity;
                float _EdgeSoftness;
                float _OcclusionSoftness;
                float _ColorTransitionSoftness;
                float _ShadowGlow;
                float _ShadowGlowFalloff;
                float _DistanceFade;
                float _EndHalfWidth;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                output.transverse = input.positionOS.x;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float edgeDistance = min(input.uv.x, 1.0 - input.uv.x);
                float edgeFade = smoothstep(0.0, max(_EdgeSoftness, 0.001), edgeDistance);
                float profileU = saturate(input.transverse / max(_EndHalfWidth * 2.0, 0.0001) + 0.5);
                float cutoff = SAMPLE_TEXTURE2D(_OcclusionTex, sampler_OcclusionTex, float2(profileU, 0.5)).r;
                float fadeStart = max(0.0, cutoff - max(_OcclusionSoftness, 0.001));
                float directLight = 1.0 - smoothstep(fadeStart, max(cutoff, fadeStart + 0.0001), input.uv.y);
                directLight = lerp(directLight, 1.0, step(0.9995, cutoff));

                float isBlocked = 1.0 - step(0.9995, cutoff);
                float behindWall = smoothstep(cutoff, cutoff + max(fwidth(input.uv.y) * 1.5, 0.0005), input.uv.y);
                float shadowDistance = max(0.0, input.uv.y - cutoff);
                float scatteredLight = isBlocked * behindWall * _ShadowGlow * exp2(-_ShadowGlowFalloff * shadowDistance);
                float visualLight = max(directLight, scatteredLight);
                half4 colorProfile = SAMPLE_TEXTURE2D(_ColorProfileTex, sampler_ColorProfileTex, float2(saturate(input.uv.x), saturate(input.uv.y)));
                half3 resolvedLightColor = lerp(_TintColor.rgb, colorProfile.rgb, saturate(colorProfile.a));

                float longitudinalFade = lerp(1.0, _DistanceFade, saturate(input.uv.y));
                float alpha = saturate(_BeamOpacity * edgeFade * visualLight * input.color.a);
                half3 glow = input.color.rgb * resolvedLightColor * _Intensity * longitudinalFade;
                return half4(glow, alpha);
            }
            ENDHLSL
        }
    }
}