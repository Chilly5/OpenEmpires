Shader "OpenEmpires/Terrain_Splatmap"
{
    Properties
    {
        _SplatMap ("Splat Map", 2D) = "white" {}
        _TexGrass ("Grass Texture", 2D) = "white" {}
        _TexDirt ("Dirt Texture", 2D) = "white" {}
        _TexSand ("Sand Texture", 2D) = "white" {}
        _TexRock ("Rock Texture", 2D) = "white" {}
        _TintGrass ("Grass Tint", Color) = (0.35, 0.55, 0.2, 1)
        _TintDirt ("Dirt Tint", Color) = (0.45, 0.35, 0.2, 1)
        _TintSand ("Sand Tint", Color) = (0.76, 0.70, 0.50, 1)
        _TintRock ("Rock Tint", Color) = (0.5, 0.48, 0.44, 1)
        _ForestMask ("Forest Mask", 2D) = "black" {}
        _TexForestFloor ("Forest Floor Texture", 2D) = "white" {}
        _TintForestFloor ("Forest Floor Tint", Color) = (0.55, 0.52, 0.35, 1)
        _TexSnow ("Snow Texture", 2D) = "white" {}
        _TintSnow ("Snow Tint", Color) = (0.9, 0.92, 0.98, 1)
        _SnowStartHeight ("Snow Start Height", Float) = 6.8
        _SnowFullHeight ("Snow Full Height", Float) = 7.6
        _SnowNoiseScale ("Snow Noise Scale", Float) = 0.08
        _SnowNoiseStrength ("Snow Noise Strength", Float) = 0.8
        _TexScale ("Texture Tiling Scale", Float) = 8
        _DetailScale ("Detail Noise Scale", Float) = 32
        _DetailStrength ("Detail Noise Strength", Float) = 0.12
        [HideInInspector] _FogOfWarTex ("Fog Of War", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvTiled : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            TEXTURE2D(_SplatMap); SAMPLER(sampler_SplatMap);
            TEXTURE2D(_TexGrass); SAMPLER(sampler_TexGrass);
            TEXTURE2D(_TexDirt); SAMPLER(sampler_TexDirt);
            TEXTURE2D(_TexSand); SAMPLER(sampler_TexSand);
            TEXTURE2D(_TexRock); SAMPLER(sampler_TexRock);
            TEXTURE2D(_ForestMask); SAMPLER(sampler_ForestMask);
            TEXTURE2D(_TexForestFloor); SAMPLER(sampler_TexForestFloor);
            TEXTURE2D(_TexSnow); SAMPLER(sampler_TexSnow);

            // Fog of war (global)
            TEXTURE2D(_FogOfWarTex); SAMPLER(sampler_FogOfWarTex);
            float4 _FogOfWarParams; // x=mapWidth, y=mapHeight

            CBUFFER_START(UnityPerMaterial)
                float4 _SplatMap_ST;
                float4 _TintGrass;
                float4 _TintDirt;
                float4 _TintSand;
                float4 _TintRock;
                float4 _TintForestFloor;
                float4 _TintSnow;
                float _SnowStartHeight;
                float _SnowFullHeight;
                float _SnowNoiseScale;
                float _SnowNoiseStrength;
                float _TexScale;
                float _DetailScale;
                float _DetailStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = input.uv;
                output.uvTiled = posInputs.positionWS.xz / _TexScale;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            // Procedural value noise for detail variation
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

            half4 frag(Varyings input) : SV_Target
            {
                // Sample splatmap: R=grass, G=dirt, B=sand, A=rock
                half4 splat = SAMPLE_TEXTURE2D(_SplatMap, sampler_SplatMap, input.uv);

                // Sample tiled terrain textures
                half3 grass = SAMPLE_TEXTURE2D(_TexGrass, sampler_TexGrass, input.uvTiled).rgb * _TintGrass.rgb;
                half3 dirt = SAMPLE_TEXTURE2D(_TexDirt, sampler_TexDirt, input.uvTiled).rgb * _TintDirt.rgb;
                half3 sand = SAMPLE_TEXTURE2D(_TexSand, sampler_TexSand, input.uvTiled).rgb * _TintSand.rgb;
                half3 rock = SAMPLE_TEXTURE2D(_TexRock, sampler_TexRock, input.uvTiled).rgb * _TintRock.rgb;

                // Blend by splatmap weights
                half3 albedo = grass * splat.r + dirt * splat.g + sand * splat.b + rock * splat.a;

                // Forest floor blending
                half forestMask = SAMPLE_TEXTURE2D(_ForestMask, sampler_ForestMask, input.uv).r;
                half3 forestFloor = SAMPLE_TEXTURE2D(_TexForestFloor, sampler_TexForestFloor, input.uvTiled).rgb * _TintForestFloor.rgb;
                albedo = lerp(albedo, forestFloor, forestMask);

                // Height-based snow blending with slope falloff
                half3 snowTex = SAMPLE_TEXTURE2D(_TexSnow, sampler_TexSnow, input.uvTiled).rgb * _TintSnow.rgb;
                float snowNoise = valueNoise(input.positionWS.xz * _SnowNoiseScale) * 2.0 - 1.0;
                float snowHeight = input.positionWS.y + snowNoise * _SnowNoiseStrength;
                float snowFactor = saturate((snowHeight - _SnowStartHeight) / max(_SnowFullHeight - _SnowStartHeight, 0.001));
                // Slope falloff: steep cliffs get no snow, flat peaks get full snow
                float slopeFactor = saturate(input.normalWS.y * input.normalWS.y);
                snowFactor *= slopeFactor;
                albedo = lerp(albedo, snowTex, snowFactor);

                // Detail noise for micro-variation at close range
                float d1 = valueNoise(input.positionWS.xz * _DetailScale);
                float d2 = valueNoise(input.positionWS.xz * _DetailScale * 2.13);
                float detail = d1 * 0.6 + d2 * 0.4;
                albedo *= 1.0 + (detail - 0.5) * _DetailStrength * 2.0;

                // Simple Lambert lighting
                Light mainLight = GetMainLight();
                half3 normal = normalize(input.normalWS);
                half NdotL = saturate(dot(normal, mainLight.direction));
                half3 diffuse = mainLight.color * NdotL;

                // Ambient
                half3 ambient = SampleSH(normal);

                half3 color = albedo * (diffuse + ambient);
                color = MixFog(color, input.fogFactor);

                // Fog of war: darken terrain based on visibility
                float2 fogUV = input.positionWS.xz / _FogOfWarParams.xy;
                half fogAlpha = SAMPLE_TEXTURE2D(_FogOfWarTex, sampler_FogOfWarTex, fogUV).a;
                color = lerp(color, half3(0, 0, 0), fogAlpha);

                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            float3 _LightDirection;

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                positionWS = ApplyShadowBias(positionWS, normalWS, _LightDirection);
                output.positionCS = TransformWorldToHClip(positionWS);

                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif

                return output;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
