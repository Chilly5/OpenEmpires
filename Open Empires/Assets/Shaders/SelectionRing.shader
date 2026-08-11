Shader "Custom/SelectionRing"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.45)
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.92
        _Softness ("Edge Softness", Range(0, 0.2)) = 0.02
        _SquareOutline ("Square Outline", Float) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("Depth Test", Float) = 4
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

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
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
                half _SquareOutline;
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

                half2 normalizedXZ = abs(input.objectXZ) * 2.0;
                half dist = _SquareOutline > 0.5
                    ? max(normalizedXZ.x, normalizedXZ.y)
                    : length(input.objectXZ) * 2.0;

                half outer = 1.0 - smoothstep(1.0 - _Softness, 1.0, dist);
                half inner = smoothstep(_InnerRadius - _Softness, _InnerRadius, dist);
                half ring = outer * inner;

                clip(ring - 0.01);

                return half4(_Color.rgb, _Color.a * ring);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
