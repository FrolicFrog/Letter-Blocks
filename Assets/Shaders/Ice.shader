Shader "Custom/URP/StylizedToonIce"
{
    Properties
    {
        [Header(Ice Colors)]
        _TopColor ("Top Surface Ice Color", Color) = (0.22, 0.78, 0.96, 1.0)
        _FrostPatternColor ("Ice Patch Highlight Color", Color) = (0.58, 0.92, 1.0, 1.0)
        _CreaseColor ("Bevel Rim Crease (Dark Blue)", Color) = (0.03, 0.28, 0.58, 1.0)
        _WallColor ("Side Wall Color", Color) = (0.08, 0.48, 0.82, 1.0)

        [Header(Bevel Rim Settings)]
        _BevelCreasePosition ("Crease Curve Position", Range(0.1, 0.95)) = 0.75
        _BevelCreaseWidth ("Crease Line Width", Range(0.01, 0.5)) = 0.2
        _CreaseStrength ("Crease Darkening Strength", Range(0.0, 1.0)) = 0.85

        [Header(Procedural Ice Crackles)]
        _NoiseScale ("Ice Crystal Pattern Scale", Float) = 6.0
        _NoiseSharpness ("Noise Edge Sharpness", Range(0.1, 5.0)) = 2.0
        _NoiseAmount ("Ice Patch Strength", Range(0.0, 1.0)) = 0.4

        [Header(Toon Gloss Specular)]
        _LightDir ("Fake Light Direction Offset", Vector) = (0.4, 0.8, -0.4, 0)
        _GlossThreshold ("Gloss Edge Threshold", Range(0.1, 0.99)) = 0.65
        _GlossIntensity ("Gloss Brightness", Range(0.0, 3.0)) = 1.2
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "StylizedToonIce"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _FrostPatternColor;
                float4 _CreaseColor;
                float4 _WallColor;
                float4 _LightDir;
                float _BevelCreasePosition;
                float _BevelCreaseWidth;
                float _CreaseStrength;
                float _NoiseScale;
                float _NoiseSharpness;
                float _NoiseAmount;
                float _GlossThreshold;
                float _GlossIntensity;
            CBUFFER_END

            // --- 3D Cellular Voronoi Noise ---
            float3 hash33(float3 p)
            {
                p = float3(dot(p, float3(127.1, 311.7, 74.7)),
                           dot(p, float3(269.5, 183.3, 246.1)),
                           dot(p, float3(113.5, 271.9, 124.6)));
                return frac(sin(p) * 43758.5453123);
            }

            float voronoi3D(float3 p)
            {
                float3 g = floor(p);
                float3 f = frac(p);
                float md = 8.0;

                for (int z = -1; z <= 1; z++)
                {
                    for (int y = -1; y <= 1; y++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            float3 lattice = float3(x, y, z);
                            float3 offset = hash33(g + lattice);
                            float3 dist = lattice + offset - f;
                            float d = dot(dist, dist);
                            md = min(md, d);
                        }
                    }
                }
                return sqrt(md);
            }

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, float4(0, 0, 0, 0));

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;

                return output;
            }

            half4 Fragment(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 lightDirWS = normalize(_LightDir.xyz);

                // 1. Calculate Top Face vs Side Wall transition using Vertical Normal
                float NdotUp = saturate(normalWS.y);
                
                // 2. Compute Bevel Rim Crease (Dark Rim exactly on the rounded bevel curve)
                float creaseMin = _BevelCreasePosition - _BevelCreaseWidth * 0.5;
                float creaseMax = _BevelCreasePosition + _BevelCreaseWidth * 0.5;
                float creaseMask = smoothstep(creaseMin, _BevelCreasePosition, NdotUp) * (1.0 - smoothstep(_BevelCreasePosition, creaseMax, NdotUp));

                // 3. World-Space Procedural Ice Patch Pattern
                float noiseVal = voronoi3D(input.positionWS * _NoiseScale);
                float icePatches = saturate(pow(noiseVal, _NoiseSharpness));
                float3 topBaseIce = lerp(_TopColor.rgb, _FrostPatternColor.rgb, icePatches * _NoiseAmount);

                // 4. Mix Top Ice, Side Walls, and Bevel Crease Rim
                float isTopFace = smoothstep(0.2, 0.8, NdotUp);
                float3 surfaceColor = lerp(_WallColor.rgb, topBaseIce, isTopFace);
                
                // Apply dark blue crease line on the bevel curve
                surfaceColor = lerp(surfaceColor, _CreaseColor.rgb, creaseMask * _CreaseStrength);

                // 5. Toon Specular Gloss Highlight
                float3 halfDir = normalize(lightDirWS + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float glossMask = step(_GlossThreshold, pow(NdotH, 16.0)) * _GlossIntensity;
                float3 glossColor = _FrostPatternColor.rgb * glossMask * isTopFace;

                float3 finalRGB = surfaceColor + glossColor;

                return half4(finalRGB, 1.0);
            }
            ENDHLSL
        }
    }
}