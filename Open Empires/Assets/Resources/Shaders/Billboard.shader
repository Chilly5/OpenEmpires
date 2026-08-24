Shader "OpenEmpires/Billboard"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _FlashAmount ("Flash Amount", Range(0, 1)) = 0
        [HideInInspector] _FogOfWarTex ("Fog Of War", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
        }

        // Pre-pass: at every sprite pixel whose underlying pixel is terrain (stencil bit 0 = 1),
        // overwrite depth with the sprite's pivot depth. This lets the color pass win LEqual
        // over terrain without affecting unit pixels (unit shader clears stencil bit 0).
        Pass
        {
            Name "BillboardDepthPunch"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Off
            ZWrite On
            ZTest Always
            ColorMask 0

            Stencil
            {
                Ref 1
                ReadMask 1
                Comp Equal
                Pass Keep
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
                half4 _Color;
                half4 _FlashColor;
                half _FlashAmount;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 worldPivot = TransformObjectToWorld(float3(0, 0, 0));
                float3 camRight = normalize(UNITY_MATRIX_V[0].xyz);
                float3 camUp = normalize(UNITY_MATRIX_V[1].xyz);
                float3 scale;
                scale.x = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                scale.y = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                float3 worldPos = worldPivot
                    + camRight * input.positionOS.x * scale.x
                    + camUp * input.positionOS.y * scale.y;
                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Color.a;
                clip(a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Billboard"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            // Clear terrain stencil bit 0 where this sprite draws, so that a subsequent
            // sprite's depth-punch pass will NOT overwrite this sprite's depth —
            // leaving the standard ZTest LEqual to handle sprite-vs-sprite ordering.
            Stencil
            {
                WriteMask 1
                Ref 0
                Comp Always
                Pass Replace
            }

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

            // Aerial perspective (global)
            float4 _AerialFogColor;
            float4 _AerialFogParams; // x=start, y=1/(end-start), z=maxAmount
            float2 _CameraFocusXZ;
            float4 _CameraFogDir;

            // Cloud shadows (global)
            float4 _CloudParams;  // x=scale, y=speed, z=shadowIntensity, w=coverage
            float4 _CloudParams2; // x=softness
            float4 _CloudDirection;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
                half4 _Color;
                half4 _FlashColor;
                half _FlashAmount;
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

                // Orthographic billboard: use camera right & up so sprite faces the camera plane
                float3 camRight = normalize(UNITY_MATRIX_V[0].xyz);
                float3 camUp = normalize(UNITY_MATRIX_V[1].xyz);

                float3 scale;
                scale.x = length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x));
                scale.y = length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y));
                scale.z = length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z));

                float3 worldPos = worldPivot
                    + camRight * input.positionOS.x * scale.x
                    + camUp * input.positionOS.y * scale.y;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                output.positionWS = worldPivot;

                return output;
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float sampleClouds(float2 worldXZ)
            {
                float2 windOffset = _CloudDirection.xy * _Time.y * _CloudParams.y * _CloudParams.x;
                float2 uv = worldXZ * _CloudParams.x + windOffset;
                float n = valueNoise(uv) * 0.5 + valueNoise(uv * 2.03) * 0.25
                        + valueNoise(uv * 4.01) * 0.125 + valueNoise(uv * 8.05) * 0.0625;
                float coverage = _CloudParams.w;
                float softness = max(_CloudParams2.x, 0.01);
                return 1.0 - smoothstep(coverage - softness, coverage + softness, n);
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                // Alpha-tested cutout: discard below cutoff so we can write depth cleanly
                // (prevents soft-edge halo in depth buffer and gives proper occlusion with 3D units)
                clip(col.a - _Cutoff);

                // Cloud shadow
                float cloud = sampleClouds(input.positionWS.xz);
                float cloudShadow = 1.0 - cloud * _CloudParams.z;
                col.rgb *= cloudShadow;

                col.rgb = MixFog(col.rgb, input.fogCoord);

                // Aerial perspective: directional depth fog (further from camera = hazier)
                float2 deltaXZ = input.positionWS.xz - _CameraFocusXZ;
                float dirDist = dot(deltaXZ, _CameraFogDir.xy);
                float radialDist = length(deltaXZ);
                float dist = max(dirDist, radialDist * _AerialFogParams.w);
                float aerialFog = saturate((dist - _AerialFogParams.x) * _AerialFogParams.y);
                aerialFog *= _AerialFogParams.z;
                col.rgb = lerp(col.rgb, _AerialFogColor.rgb, aerialFog);
                col.rgb = lerp(col.rgb, _FlashColor.rgb, saturate(_FlashAmount));

                // Fog of war: hide in unexplored, darken in explored
                float2 fogUV = input.positionWS.xz / _FogOfWarParams.xy;
                half fogAlpha = SAMPLE_TEXTURE2D(_FogOfWarTex, sampler_FogOfWarTex, fogUV).a;
                clip(0.95 - fogAlpha);
                col.rgb = lerp(col.rgb, half3(0, 0, 0), fogAlpha);

                return col;
            }
            ENDHLSL
        }

    }
}
