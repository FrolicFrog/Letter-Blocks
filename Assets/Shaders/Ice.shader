Shader "Custom/URP/ToonFrostTile_ShapeAdaptive"
{
    Properties
    {
        [Header(Base Ice Body)]
        _TopColor ("Top Color", Color) = (0.75, 0.93, 1.0, 1.0)
        _TopBrightness ("Top Brightness", Range(0, 2)) = 1.3
        _TopVariation ("Top Variation", Range(0, 1)) = 0.15

        [Header(Side Walls)]
        _SideColor ("Side Color", Color) = (0.55, 0.85, 0.98, 1.0)
        _SideDarkness ("Side Darkness", Range(0, 2)) = 1.0

        [Header(Outer Dark Bevel Border  Disabled by default)]
        _BevelColor ("Dark Border Color", Color) = (0.12, 0.52, 0.78, 1.0)
        _BorderThickness ("Border Width (Outer Rim)", Range(0.0, 0.5)) = 0.0
        _BorderIrregularity ("Border Waviness", Range(0, 1)) = 0.45
        _BorderDetail ("Border Noise Detail", Range(0.1, 10)) = 2.5
        _BevelSoftness ("Border Edge Softness", Range(0.001, 0.1)) = 0.015

        [Header(Interior Grown Ice Ring  Disabled by default)]
        _IceColor ("Ice Border Color", Color) = (0.92, 0.98, 1.0, 1.0)
        _IceGrowthWidth ("Ice Growth Width (Inward)", Range(0.0, 0.4)) = 0.0
        _IceNoiseScale ("Ice Noise Scale", Float) = 8.0
        _IceJaggedness ("Ice Edge Jaggedness", Range(0, 1)) = 0.55
        _IceBumpStrength ("Ice Surface Bump Strength", Range(0.0, 5.0)) = 0.6

        [Header(Ice Cracks  Bright Facet Lines)]
        _CrackColor ("Crack Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _CrackScale ("Crack Scale", Range(0.1, 20.0)) = 3.0
        _CrackWidth ("Crack Width", Range(0.001, 0.1)) = 0.02
        _CrackDistortion ("Crack Distortion", Range(0.0, 2.0)) = 0.4
        _CrackOpacity ("Crack Opacity", Range(0.0, 1.0)) = 0.35

        [Header(Ice Cracks 3D Depth  Subtle Facets)]
        _CrackDepthStrength ("Crack Depth Recess", Range(0.0, 5.0)) = 0.5
        _CrackInnerColor ("Crack Inner Shadow Color", Color) = (0.55, 0.82, 0.95, 1.0)
        _CrackBevelWidth ("Bevel Width", Range(0.001, 0.15)) = 0.08

        [Header(Shard Edge Shine)]
        _ShineColor ("Shine Color", Color) = (1.0, 1.0, 1.0, 0.9)
        _ShineSpread ("Shine Spread from Crack", Range(0.001, 0.3)) = 0.08
        _ShineSoftness ("Shine Edge Softness", Range(0.001, 0.2)) = 0.08
        _ShineBreakup ("Shine Noise Breakup", Range(0.0, 1.0)) = 0.5

        [Header(Frost Detail Top Only)]
        _FrostScale ("Frost Scale", Float) = 5.0
        _FrostContrast ("Frost Contrast", Range(0.1, 3)) = 1.2
        _FrostAmount ("Frost Amount", Range(0, 1)) = 0.1

        [Header(Toon Lighting  Softened)]
        _ToonSteps ("Toon Steps", Range(1, 5)) = 3
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.7
        _RimHighlight ("Rim Highlight", Range(0, 1)) = 0.6

        [Header(Inner Glow  Subsurface Look)]
        _GlowColor ("Glow Color", Color) = (0.85, 0.97, 1.0, 1.0)
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.6
        _GlowPower ("Glow Falloff Power", Range(0.5, 8)) = 2.0

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
                float _IceBumpStrength;

                // Crack Properties
                float4 _CrackColor;
                float _CrackScale;
                float _CrackWidth;
                float _CrackDistortion;
                float _CrackOpacity;

                // 3D Depth Properties
                float _CrackDepthStrength;
                float4 _CrackInnerColor;
                float _CrackBevelWidth;

                // Shine Properties
                float4 _ShineColor;
                float _ShineSpread;
                float _ShineSoftness;
                float _ShineBreakup;

                float _FrostScale;
                float _FrostContrast;
                float _FrostAmount;
                float _ToonSteps;
                float _ShadowStrength;
                float _RimHighlight;

                // Glow Properties
                float4 _GlowColor;
                float _GlowStrength;
                float _GlowPower;

                float _TopFaceNormalThreshold;
                float _TopAreaSpread;
            CBUFFER_END

            float _BoundaryCount;
            float4 _Boundaries[64];

            // ============================================================
            // NOISE UTILITIES
            // ============================================================
            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
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

            // ============================================================
            // VORONOI EDGE DISTANCE
            // ============================================================
            float voronoiEdgeDist(float2 x)
            {
                float2 n = floor(x);
                float2 f = frac(x);
                float2 mg, mr;
                float md = 8.0;

                for (int j = -1; j <= 1; j++) {
                    for (int i = -1; i <= 1; i++) {
                        float2 g = float2(float(i), float(j));
                        float2 o = hash22(n + g);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        if (d < md) {
                            md = d;
                            mr = r;
                            mg = g;
                        }
                    }
                }

                md = 8.0;
                for (int j = -2; j <= 2; j++) {
                    for (int i = -2; i <= 2; i++) {
                        float2 g = mg + float2(float(i), float(j));
                        float2 o = hash22(n + g);
                        float2 r = g + o - f;

                        if (dot(mr - r, mr - r) > 0.00001) {
                            md = min(md, dot(0.5 * (mr + r), normalize(r - mr)));
                        }
                    }
                }
                return md;
            }

            // ============================================================
            // VERTEX & FRAGMENT
            // ============================================================
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
                float3 normalOS = normalize(input.normalOS);
                float3 posOS = input.positionOS;
                float3 posWS = input.positionWS; // Used for seamless tiling

                float isTopFace = step(_TopFaceNormalThreshold, normalOS.y);
                float distInward = 0.0;

                if (isTopFace > 0.5)
                {
                    float minDist = 9999.0;
                    float2 pt = posOS.xz; // Keep boundary distance local to the tile shape
                    int count = (int)_BoundaryCount;

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

                // Use World Space for all noise so it aligns perfectly across borders
                float3 borderNoisePos = posWS * _BorderDetail + float3(12.3, 45.6, 78.9);
                float borderWiggle = (fbm(borderNoisePos) - 0.5) * _BorderIrregularity * 0.15;
                float borderThreshold = _BorderThickness + borderWiggle;
                float borderSoftness = max(0.001, _BevelSoftness);
                // Border is disabled by default (_BorderThickness = 0), keeping the mask near-zero
                float borderMask = _BorderThickness > 0.0001
                    ? smoothstep(borderThreshold + borderSoftness, borderThreshold - borderSoftness, distInward) * isTopFace
                    : 0.0;

                float3 iceNoisePos = posWS * _IceNoiseScale + float3(34.1, 67.8, 91.2);
                float iceNoiseBase = fbm(iceNoisePos);
                float iceSpikes = (iceNoiseBase - 0.25) * _IceJaggedness * 0.25;
                float iceStart = borderThreshold - borderSoftness;
                float iceEnd = iceStart + _IceGrowthWidth + iceSpikes;
                float iceMask = _IceGrowthWidth > 0.0001
                    ? smoothstep(iceEnd + 0.01, iceStart, distInward) * (1.0 - borderMask) * isTopFace
                    : 0.0;

                // --- Base Colors (bright, glassy, low-contrast) ---
                float3 sideCol = _SideColor.rgb * _SideDarkness;
                float topNoiseVar = fbm(posWS * 2.0);
                float3 topBaseCol = _TopColor.rgb * _TopBrightness;
                topBaseCol = lerp(topBaseCol, topBaseCol * (1.0 - _TopVariation * 0.35), topNoiseVar);

                float3 finalCol = lerp(sideCol, topBaseCol, isTopFace);
                finalCol = lerp(finalCol, _IceColor.rgb, iceMask);
                finalCol = lerp(finalCol, _BevelColor.rgb, borderMask);

                // --- Frost Details (very subtle) ---
                float frostNoise = fbm(posWS * _FrostScale);
                frostNoise = pow(saturate(frostNoise), _FrostContrast);
                float centerFrostMask = frostNoise * _FrostAmount * (1.0 - borderMask) * (1.0 - iceMask);
                finalCol = lerp(finalCol, _IceColor.rgb, centerFrostMask);

                // --- Large Ice Facets / Cracks (seamless world space, works on ALL faces now) ---
                float2 crackUVBase = (abs(normalOS.y) > 0.5) ? posWS.xz : (abs(normalOS.x) > 0.5 ? posWS.zy : posWS.xy);
                float2 crackOffset = float2(fbm(posWS * 2.0), fbm(posWS * 2.0 + float3(1.2, 3.4, 5.6))) * _CrackDistortion;
                float2 uvCrack = crackUVBase * _CrackScale + crackOffset;

                float rawCrackDist = voronoiEdgeDist(uvCrack);
                float2 eps = float2(0.01, 0.0);
                float distX = voronoiEdgeDist(uvCrack + eps.xy);
                float distZ = voronoiEdgeDist(uvCrack + eps.yx);
                float2 grad = float2(distX - rawCrackDist, distZ - rawCrackDist) / eps.x;

                // --- Ice Ring Derivatives for Bump (only relevant when ice ring enabled) ---
                float2 epsIce = float2(0.02, 0.0);
                float iceNoiseX = fbm(iceNoisePos + float3(epsIce.x, 0.0, 0.0));
                float iceNoiseZ = fbm(iceNoisePos + float3(0.0, 0.0, epsIce.x));
                float2 iceGrad = float2(iceNoiseX - iceNoiseBase, iceNoiseZ - iceNoiseBase) / epsIce.x;

                // --- NORMAL PERTURBATION (gentle facet bumps, not deep grooves) ---
                float3 localNormal = normalOS;

                localNormal.x -= iceGrad.x * _IceBumpStrength * iceMask;
                localNormal.z -= iceGrad.y * _IceBumpStrength * iceMask;

                float bevelMask = smoothstep(_CrackBevelWidth, 0.0, rawCrackDist) * (1.0 - borderMask) * _CrackOpacity;
                localNormal.x += grad.x * _CrackDepthStrength * bevelMask * 0.3;
                localNormal.z += grad.y * _CrackDepthStrength * bevelMask * 0.3;

                localNormal = normalize(localNormal);
                float3 modifiedNormalWS = normalize(TransformObjectToWorldNormal(localNormal));

                // --- Cracks read as BRIGHT facet seams, not dark lines ---
                float crackMask = smoothstep(_CrackWidth, _CrackWidth * 0.25, rawCrackDist) * _CrackOpacity * (1.0 - borderMask);
                float shadowMask = smoothstep(_CrackBevelWidth, _CrackWidth, rawCrackDist);
                float depthBlend = (1.0 - shadowMask) * bevelMask * (1.0 - crackMask);

                // Subtle facet shading instead of a hard dark inner crack color
                finalCol = lerp(finalCol, _CrackInnerColor.rgb, depthBlend * _CrackOpacity * 0.5);
                finalCol = lerp(finalCol, _CrackColor.rgb, crackMask);

                // --- Toon Lighting (soft, few steps, high ambient floor) ---
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction.y == 0.0 ? float3(-0.35, 0.8, -0.25) : mainLight.direction);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                float NdotL = saturate(dot(modifiedNormalWS, lightDir));
                float steps = max(1.0, _ToonSteps);
                float toonStep = floor(NdotL * steps) / steps;
                float toonLight = lerp(_ShadowStrength, 1.0, toonStep);
                finalCol *= toonLight;

                // --- Shard Edge Shine (Restricted to Cracks) ---
                float edgeProximity = smoothstep(_CrackWidth + _ShineSpread + _ShineSoftness, _CrackWidth, rawCrackDist);

                float shineNoise = fbm(posWS * 12.0 + float3(11.1, 22.2, 33.3));
                float shineBreakup = lerp(1.0, shineNoise, _ShineBreakup);

                float lightFacing = saturate(dot(modifiedNormalWS, lightDir));
                float shineIntensity = smoothstep(0.5, 0.5 + _ShineSoftness, lightFacing * shineBreakup);

                float shine = shineIntensity * edgeProximity * _CrackOpacity;
                float shineMask = (1.0 - borderMask) * (1.0 - crackMask);

                finalCol += _ShineColor.rgb * shine * _ShineColor.a * shineMask;

                // --- Fresnel Rim + Inner Glow (subsurface-scatter look) ---
                float NdotV = saturate(dot(modifiedNormalWS, viewDirWS));
                float rim = 1.0 - NdotV;
                float rimShaped = pow(saturate(rim), 3.0) * _RimHighlight;
                finalCol += _IceColor.rgb * rimShaped * 0.6;

                // Soft inward glow: brightest facing the viewer, fading at grazing angles,
                // gives the ball a lit-from-within translucent feel like the reference.
                float glow = pow(NdotV, _GlowPower);
                finalCol += _GlowColor.rgb * glow * _GlowStrength * 0.5;

                // Gentle overall lift so nothing reads as flat dark blue
                finalCol = saturate(finalCol);

                return half4(finalCol, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
