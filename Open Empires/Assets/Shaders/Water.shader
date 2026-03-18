Shader "OpenEmpires/Water"
{
    Properties
    {
        _Color ("Water Color", Color) = (0.1, 0.25, 0.5, 0.6)
        _Smoothness ("Smoothness", Float) = 0.9
        [HideInInspector] _FogOfWarTex ("Fog Of War", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            TEXTURE2D(_FogOfWarTex); SAMPLER(sampler_FogOfWarTex);
            float4 _FogOfWarParams;

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Smoothness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normal = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half NdotL = saturate(dot(normal, mainLight.direction));
                half3 ambient = SampleSH(normal);
                half3 color = _Color.rgb * (mainLight.color * NdotL + ambient);
                color = MixFog(color, input.fogFactor);

                // Fog of war: darken water in unexplored/explored areas
                float2 fogUV = input.positionWS.xz / _FogOfWarParams.xy;
                half fogAlpha = SAMPLE_TEXTURE2D(_FogOfWarTex, sampler_FogOfWarTex, fogUV).a;
                color = lerp(color, half3(0, 0, 0), fogAlpha);

                // In fully unexplored areas, make water fully opaque black
                half alpha = lerp(_Color.a, 1.0, fogAlpha);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
