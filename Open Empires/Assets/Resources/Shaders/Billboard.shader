Shader "OpenEmpires/Billboard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _FogOfWarTex ("Fog Of War", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "Billboard"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // Fog of war (global)
            TEXTURE2D(_FogOfWarTex); SAMPLER(sampler_FogOfWarTex);
            float4 _FogOfWarParams; // x=mapWidth, y=mapHeight

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
                half4 _Color;
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
                float fogCoord : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPivot = TransformObjectToWorld(float3(0, 0, 0));

                // Orthographic billboard: use view matrix vectors (uniform direction, no per-object rotation)
                float3 camRight = normalize(UNITY_MATRIX_V[0].xyz);
                float3 worldUp = float3(0, 1, 0);

                float3 scale;
                scale.x = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                scale.y = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                scale.z = length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z));

                float3 worldPos = worldPivot
                    + camRight * input.positionOS.x * scale.x
                    + worldUp * input.positionOS.y * scale.y;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                output.positionWS = worldPivot;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(col.a - _Cutoff);
                col.rgb = MixFog(col.rgb, input.fogCoord);

                // Fog of war: hide in unexplored, darken in explored
                float2 fogUV = input.positionWS.xz / _FogOfWarParams.xy;
                half fogAlpha = SAMPLE_TEXTURE2D(_FogOfWarTex, sampler_FogOfWarTex, fogUV).a;
                clip(0.95 - fogAlpha);
                col.rgb = lerp(col.rgb, half3(0, 0, 0), fogAlpha);

                return col;
            }
            ENDHLSL
        }

        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
                half4 _Color;
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vertShadow(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 worldPivot = TransformObjectToWorld(float3(0, 0, 0));
                float3 camRight = normalize(UNITY_MATRIX_V[0].xyz);
                float3 worldUp = float3(0, 1, 0);

                float3 scale;
                scale.x = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                scale.y = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                scale.z = length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z));

                float3 worldPos = worldPivot
                    + camRight * input.positionOS.x * scale.x
                    + worldUp * input.positionOS.y * scale.y;

                output.positionCS = TransformWorldToHClip(ApplyShadowBias(worldPos, float3(0, 1, 0), float3(0, 0, 0)));
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);

                return output;
            }

            half4 fragShadow(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                clip(col.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
