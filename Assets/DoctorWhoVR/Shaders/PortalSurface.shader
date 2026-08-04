Shader "DoctorWhoVR/PortalSurface"
{
    Properties
    {
        _PortalTex ("Portal View", 2D) = "black" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Brightness ("Brightness", Range(0.25, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry+10"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "StablePortalWindow"

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PortalTex);
            SAMPLER(sampler_PortalTex);

            float4 _PortalTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenPosition : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS =
                    TransformObjectToHClip(
                        input.positionOS.xyz);

                output.screenPosition =
                    ComputeScreenPos(
                        output.positionCS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv =
                    input.screenPosition.xy /
                    max(
                        input.screenPosition.w,
                        0.00001);

                #if UNITY_UV_STARTS_AT_TOP
                if (_PortalTex_TexelSize.y < 0)
                    uv.y = 1.0 - uv.y;
                #endif

                half4 portalColor =
                    SAMPLE_TEXTURE2D(
                        _PortalTex,
                        sampler_PortalTex,
                        uv);

                portalColor.rgb *=
                    _Tint.rgb *
                    _Brightness;

                portalColor.a = 1;
                return portalColor;
            }

            ENDHLSL
        }
    }

    FallBack Off
}
