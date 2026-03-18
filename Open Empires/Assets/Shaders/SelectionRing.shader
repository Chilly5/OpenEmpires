Shader "Custom/SelectionRing"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 0, 0.5)
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.85
        _Softness ("Edge Softness", Range(0, 0.2)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "SelectionRing"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _InnerRadius;
                half _Softness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 objectXZ  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.objectXZ = input.positionOS.xz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half dist = length(input.objectXZ) * 2.0;

                half outer = 1.0 - smoothstep(1.0 - _Softness, 1.0, dist);
                half inner = smoothstep(_InnerRadius - _Softness, _InnerRadius, dist);
                half ring = outer * inner;

                clip(ring - 0.01);

                half3 col = _Color.rgb * _Color.a * ring;
                return half4(col, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
