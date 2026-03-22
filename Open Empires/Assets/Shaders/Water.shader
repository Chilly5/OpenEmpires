Shader "OpenEmpires/Water"
{
    Properties
    {
        _ColorLight ("Light Color", Color) = (0.35, 0.55, 0.75, 1)
        _ColorDark ("Dark Color", Color) = (0.08, 0.18, 0.38, 1)
        _ColorDeep ("Deep Accent", Color) = (0.04, 0.10, 0.25, 1)
        _CrestColor ("Crest Color", Color) = (0.75, 0.85, 0.92, 1)
        _WaveSpeed ("Wave Speed", Float) = 0.35
        _WaveStrength ("Wave Height", Float) = 0.20
        _WaveScale ("Wave Scale", Float) = 0.08
        _CrestThreshold ("Crest Threshold", Range(0.5, 0.98)) = 0.82
        _CrestSharpness ("Crest Sharpness", Float) = 8.0
        _CrestIntensity ("Crest Intensity", Range(0, 1)) = 0.4
        [HideInInspector] _FogOfWarTex ("Fog Of War", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry+2" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                float waveHeight : TEXCOORD2;
            };

            TEXTURE2D(_FogOfWarTex); SAMPLER(sampler_FogOfWarTex);
            float4 _FogOfWarParams;
            float4 _AerialFogColor;
            float4 _AerialFogParams;
            float2 _CameraFocusXZ;
            float4 _CameraFogDir;

            // Cloud shadows & reflections (global)
            float4 _CloudParams;  // x=scale, y=speed, z=shadowIntensity, w=coverage
            float4 _CloudParams2; // x=softness, y=reflectionIntensity
            float4 _CloudDirection; // xy = wind direction (normalized)

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorLight;
                float4 _ColorDark;
                float4 _ColorDeep;
                float4 _CrestColor;
                float _WaveSpeed;
                float _WaveStrength;
                float _WaveScale;
                float _CrestThreshold;
                float _CrestSharpness;
                float _CrestIntensity;
            CBUFFER_END

            float hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(hash(i), hash(i + float2(1, 0)), f.x),
                    lerp(hash(i + float2(0, 1)), hash(i + float2(1, 1)), f.x),
                    f.y);
            }

            float fbm(float2 p)
            {
                return vnoise(p) * 0.50 + vnoise(p * 2.1) * 0.30 + vnoise(p * 4.3) * 0.20;
            }

            float cloudFbm(float2 p)
            {
                return vnoise(p) * 0.5
                     + vnoise(p * 2.03) * 0.25
                     + vnoise(p * 4.01) * 0.125
                     + vnoise(p * 8.05) * 0.0625;
            }

            float sampleClouds(float2 worldXZ)
            {
                float2 windOffset = _CloudDirection.xy * _Time.y * _CloudParams.y * _CloudParams.x;
                float2 uv = worldXZ * _CloudParams.x + windOffset;
                float n = cloudFbm(uv);
                float coverage = _CloudParams.w;
                float softness = max(_CloudParams2.x, 0.01);
                return 1.0 - smoothstep(coverage - softness, coverage + softness, n);
            }

            // Directional wave function for rolling ocean waves
            float waveLayer(float2 pos, float2 dir, float freq, float speed, float t)
            {
                return sin(dot(pos, dir) * freq + t * speed);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y * _WaveSpeed;

                // Rolling directional waves — dominant direction with cross-waves
                float w1 = waveLayer(posWS.xz, float2(0.7, 0.5), 0.25, 1.0, t) * _WaveStrength;
                float w2 = waveLayer(posWS.xz, float2(-0.3, 0.8), 0.4, 1.3, t) * _WaveStrength * 0.5;
                float w3 = waveLayer(posWS.xz, float2(0.9, -0.2), 0.6, 0.9, t) * _WaveStrength * 0.25;
                float w4 = waveLayer(posWS.xz, float2(-0.5, -0.6), 0.8, 1.1, t) * _WaveStrength * 0.15;
                float totalWave = w1 + w2 + w3 + w4;
                posWS.y += totalWave;

                output.positionCS = TransformWorldToHClip(posWS);
                output.positionWS = posWS;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                output.waveHeight = totalWave;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 wpos = input.positionWS.xz;
                float t = _Time.y * _WaveSpeed;

                // === BASE COLOR — smooth rolling gradient ===

                // Large-scale slow color variation
                float large = fbm(wpos * _WaveScale + float2(t * 0.08, t * 0.05));

                // Medium ripple pattern
                float medium = fbm(wpos * _WaveScale * 3.0 + float2(-t * 0.12, t * 0.09));

                // Fine detail
                float detail = fbm(wpos * _WaveScale * 6.0 + float2(t * 0.15, -t * 0.1));

                // Blend: emphasize large-scale variation for broad rolling look
                float blend = large * 0.55 + medium * 0.30 + detail * 0.15;

                // Three-way color blend — deep to dark to light
                half3 color;
                float f2 = saturate(blend * 2.0);
                float f1 = saturate((blend - 0.5) * 2.0);
                color = lerp(_ColorDeep.rgb, _ColorDark.rgb, f2);
                color = lerp(color, _ColorLight.rgb, f1);

                // === WHITECAPS — tied to wave geometry ===

                // Use actual vertex wave height to place crests on wave peaks
                float normalizedWave = saturate((input.waveHeight / (_WaveStrength * 1.5)) * 0.5 + 0.5);

                // Add noise breakup so crests aren't uniform lines
                float crestNoise = fbm(wpos * 0.5 + float2(t * 0.15, t * 0.1));
                float crestBreakup = vnoise(wpos * 1.5 + float2(t * 0.2, -t * 0.12));

                // Crest appears only on high wave peaks with noise variation
                float crestMask = normalizedWave * (crestNoise * 0.6 + crestBreakup * 0.4);
                float crest = saturate((crestMask - _CrestThreshold) * _CrestSharpness) * _CrestIntensity;

                // Soften crest edges with additional breakup
                float frothDetail = vnoise(wpos * 3.0 + float2(t * 0.25, -t * 0.15));
                crest *= smoothstep(0.3, 0.6, frothDetail);

                color = lerp(color, _CrestColor.rgb, crest);

                // === CLOUD REFLECTION ===

                float cloud = sampleClouds(wpos);

                // Distort cloud reflection by wave surface normal for watery look
                float eps = 0.4;
                float gx = fbm(wpos * _WaveScale * 3.0 + float2(-t * 0.12 + eps, t * 0.09)) - medium;
                float gz = fbm(wpos * _WaveScale * 3.0 + float2(-t * 0.12, t * 0.09 + eps)) - medium;
                float3 surfNorm = normalize(float3(-gx * 2.0, 1.0, -gz * 2.0));

                // Sample clouds at distorted position for reflection
                float2 reflectOffset = surfNorm.xz * 3.0;
                float cloudReflect = sampleClouds(wpos + reflectOffset);

                // Blend cloud reflection — white-ish reflected clouds on water
                half3 cloudReflectColor = lerp(half3(0.7, 0.78, 0.88), half3(0.9, 0.93, 0.97), cloudReflect);
                color = lerp(color, cloudReflectColor, cloudReflect * _CloudParams2.y);

                // === LIGHTING — soft diffuse, minimal specular ===

                Light mainLight = GetMainLight();
                float lightFac = saturate(dot(float3(0, 1, 0), mainLight.direction)) * 0.25 + 0.75;

                // Cloud shadow on water (subtler than terrain)
                float cloudShadow = 1.0 - cloud * _CloudParams.z * 0.5;
                lightFac *= cloudShadow;

                color *= mainLight.color * lightFac;

                // Very subtle specular — just a gentle shimmer
                float3 camFwd = -normalize(UNITY_MATRIX_V[2].xyz);
                float3 halfDir = normalize(mainLight.direction + camFwd);
                float spec = pow(saturate(dot(surfNorm, halfDir)), 128.0) * 0.04;
                color += mainLight.color * spec;

                // === POST EFFECTS ===

                color = MixFog(color, input.fogFactor);

                // Aerial perspective
                float2 deltaXZ = wpos - _CameraFocusXZ;
                float dist = dot(deltaXZ, _CameraFogDir.xy);
                float aerialFog = saturate((dist - _AerialFogParams.x) * _AerialFogParams.y) * _AerialFogParams.z;
                color = lerp(color, _AerialFogColor.rgb, aerialFog);

                // Fog of war
                float2 fogUV = wpos / _FogOfWarParams.xy;
                half fogAlpha = SAMPLE_TEXTURE2D(_FogOfWarTex, sampler_FogOfWarTex, fogUV).a;
                color = lerp(color, half3(0, 0, 0), fogAlpha);

                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
