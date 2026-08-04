Shader "DoctorWhoVR/V4/DoorwayField"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.05, 0.35, 1, 0.72)
        _ColorB ("Color B", Color) = (0.40, 0.05, 1, 0.55)
        _Speed ("Speed", Range(0, 3)) = 0.65
        _Scale ("Scale", Range(1, 20)) = 7
        _EdgeGlow ("Edge Glow", Range(0, 8)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DoorwayField"

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA;
                half4 _ColorB;
                half _Speed;
                half _Scale;
                half _EdgeGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 centered = input.uv * 2.0 - 1.0;
                float radius = length(centered);
                float angle = atan2(centered.y, centered.x);

                float timeValue = _Time.y * _Speed;

                float waveA =
                    sin(angle * 5.0 +
                        radius * _Scale -
                        timeValue * 2.0);

                float waveB =
                    sin(centered.x * _Scale * 1.3 +
                        centered.y * _Scale * 0.8 +
                        timeValue);

                float blendValue =
                    saturate((waveA + waveB) * 0.25 + 0.5);

                half4 colorValue =
                    lerp(_ColorA, _ColorB, blendValue);

                float edgeDistance =
                    min(
                        min(input.uv.x, 1.0 - input.uv.x),
                        min(input.uv.y, 1.0 - input.uv.y));

                float edgeGlow =
                    1.0 - saturate(edgeDistance * _EdgeGlow);

                colorValue.rgb +=
                    edgeGlow *
                    half3(0.25, 0.45, 1.0);

                colorValue.a *=
                    0.82 + waveA * 0.08;

                return colorValue;
            }

            ENDHLSL
        }
    }

    FallBack Off
}
