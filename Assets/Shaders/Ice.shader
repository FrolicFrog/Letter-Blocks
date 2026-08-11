Shader "Custom/URP/ToonFrostTile_ShapeAdaptive"
{
    Properties
    {
        [Header(Top Surface Center Cap)]
        _TopColor ("Top Color", Color) = (0.68, 0.90, 1.0, 1.0)
        _TopBrightness ("Top Brightness", Range(0, 2)) = 1.25
        _TopVariation ("Top Variation", Range(0, 1)) = 0.25

        [Header(Side Walls)]
        _SideColor ("Side Color", Color) = (0.08, 0.33, 0.62, 1.0)
        _SideDarkness ("Side Darkness", Range(0, 2)) = 1.25

        [Header(Outer Dark Bevel Border)]
        _BevelColor ("Dark Border Color", Color) = (0.12, 0.52, 0.78, 1.0)
        _BorderThickness ("Border Width (Outer Rim)", Range(0.01, 0.5)) = 0.12
        _BorderIrregularity ("Border Waviness", Range(0, 1)) = 0.45
        _BorderDetail ("Border Noise Detail", Range(0.1, 10)) = 2.5
        _BevelSoftness ("Border Edge Softness", Range(0.001, 0.1)) = 0.015

        [Header(Interior Grown Ice Ring)]
        _IceColor ("Ice Border Color", Color) = (0.88, 0.96, 1.0, 1.0)
        _IceGrowthWidth ("Ice Growth Width (Inward)", Range(0.0, 0.4)) = 0.14
        _IceNoiseScale ("Ice Noise Scale", Float) = 8.0
        _IceJaggedness ("Ice Edge Jaggedness", Range(0, 1)) = 0.55

        [Header(Frost Detail Top Only)]
        _FrostScale ("Frost Scale", Float) = 6.0
        _FrostContrast ("Frost Contrast", Range(0.1, 3)) = 1.4
        _FrostAmount ("Frost Amount", Range(0, 1)) = 0.18

        [Header(Toon Lighting)]
        _ToonSteps ("Toon Steps", Range(1, 5)) = 2
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.45
        _RimHighlight ("Rim Highlight", Range(0, 1)) = 0.35

        [Header(Shape Settings)]
        _TopFaceNormalThreshold ("Top Face Normal.y Threshold", Range(0.5, 0.999)) = 0.9
        _TopAreaSpread ("Global Frost Spread", Range(0.2, 3.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ToonFrostGrownIce"
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 normalOS     : TEXCOORD3;
                float2 uv           : TEXCOORD4;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float _TopBrightness;
                float _TopVariation;
                float4 _SideColor;
                float _SideDarkness;
                float4 _BevelColor;
                float _BorderThickness;
                float _BorderIrregularity;
                float _BorderDetail;
                float _BevelSoftness;
                float4 _IceColor;
                float _IceGrowthWidth;
                float _IceNoiseScale;
                float _IceJaggedness;
                float _FrostScale;
                float _FrostContrast;
                float _FrostAmount;
                float _ToonSteps;
                float _ShadowStrength;
                float _RimHighlight;
                float _TopFaceNormalThreshold;
                float _TopAreaSpread;
            CBUFFER_END

            // Arrays passed via C# MaterialPropertyBlock to define the exact shape
            float _BoundaryCount;
            float4 _Boundaries[64]; 

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float n000 = hash31(i);
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));
                float x00 = lerp(n000, n100, f.x);
                float x10 = lerp(n010, n110, f.x);
                float x01 = lerp(n001, n101, f.x);
                float x11 = lerp(n011, n111, f.x);
                float y0 = lerp(x00, x10, f.y);
                float y1 = lerp(x01, x11, f.y);
                return lerp(y0, y1, f.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0;
                v += noise3D(p) * 0.5;
                p *= 2.0;
                v += noise3D(p) * 0.25;
                p *= 2.0;
                v += noise3D(p) * 0.125;
                return v;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.normalOS = input.normalOS;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 normalOS = normalize(input.normalOS);
                float3 posOS = input.positionOS;

                float isTopFace = step(_TopFaceNormalThreshold, normalOS.y);
                float distInward = 0.0;

                // Only perform the heavy distance calculation on the upward-facing surface
                if (isTopFace > 0.5)
                {
                    float minDist = 9999.0;
                    float2 pt = posOS.xz;
                    int count = (int)_BoundaryCount;

                    // Evaluate distance to the true procedural shape boundary
                    for (int i = 0; i < count && i < 64; i++)
                    {
                        float2 a = _Boundaries[i].xy;
                        float2 b = _Boundaries[i].zw;
                        float2 pa = pt - a;
                        float2 ba = b - a;
                        float h = saturate(dot(pa, ba) / max(dot(ba, ba), 0.0001));
                        float d = length(pa - ba * h);
                        minDist = min(minDist, d);
                    }
                    distInward = minDist / max(_TopAreaSpread, 0.001);
                }

                float3 borderNoisePos = posOS * _BorderDetail + float3(12.3, 45.6, 78.9);
                float borderWiggle = (fbm(borderNoisePos) - 0.5) * _BorderIrregularity * 0.15;
                float borderThreshold = _BorderThickness + borderWiggle;
                float borderSoftness = max(0.001, _BevelSoftness);
                float borderMask = smoothstep(borderThreshold + borderSoftness, borderThreshold - borderSoftness, distInward) * isTopFace;

                float3 iceNoisePos = posOS * _IceNoiseScale + float3(34.1, 67.8, 91.2);
                float iceSpikes = (fbm(iceNoisePos) - 0.25) * _IceJaggedness * 0.25;
                float iceStart = borderThreshold - borderSoftness;
                float iceEnd = iceStart + _IceGrowthWidth + iceSpikes;
                float iceMask = smoothstep(iceEnd + 0.01, iceStart, distInward) * (1.0 - borderMask) * isTopFace;

                float3 sideCol = _SideColor.rgb * _SideDarkness;
                float topNoiseVar = fbm(posOS * 2.5);
                float3 topBaseCol = _TopColor.rgb * _TopBrightness;
                topBaseCol = lerp(topBaseCol, topBaseCol * (1.0 - _TopVariation * 0.35), topNoiseVar);

                float3 finalCol = lerp(sideCol, topBaseCol, isTopFace);
                finalCol = lerp(finalCol, _IceColor.rgb, iceMask);
                finalCol = lerp(finalCol, _BevelColor.rgb, borderMask);

                float frostNoise = fbm(posOS * _FrostScale);
                frostNoise = pow(saturate(frostNoise), _FrostContrast);
                float centerFrostMask = frostNoise * _FrostAmount * isTopFace * (1.0 - borderMask) * (1.0 - iceMask);
                finalCol = lerp(finalCol, _IceColor.rgb, centerFrostMask);

                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction.y == 0.0 ? float3(-0.35, 0.8, -0.25) : mainLight.direction);
                float NdotL = saturate(dot(normalWS, lightDir));
                float steps = max(1.0, _ToonSteps);
                float toonStep = floor(NdotL * steps) / steps;
                float toonLight = lerp(_ShadowStrength, 1.0, toonStep);
                finalCol *= toonLight;

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = 1.0 - saturate(dot(normalWS, viewDirWS));
                rim = pow(saturate(rim), 3.0) * _RimHighlight;
                finalCol += _IceColor.rgb * rim * 0.5;

                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}