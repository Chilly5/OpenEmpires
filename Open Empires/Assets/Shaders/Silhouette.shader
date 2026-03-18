Shader "Custom/Silhouette"
{
    Properties
    {
        _SilhouetteColor ("Silhouette Color", Color) = (0.2, 0.4, 0.9, 0.5)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+451"
        }

        Pass
        {
            Name "Silhouette"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZTest Greater
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            Stencil
            {
                Ref 64
                ReadMask 64
                Comp NotEqual
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _SilhouetteColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_SilhouetteColor.rgb, _SilhouetteColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
