Shader "Custom/URPStylizedToonIce"
{
    Properties
    {
        [Header(Base Toon Colors)]
        _BaseColor("Ice Base Color", Color) = (0.5, 0.85, 1.0, 0.5)
        _ShadowColor("Ice Shadow Color", Color) = (0.2, 0.4, 0.75, 0.7)
        
        [Header(Cel Shading Settings)]
        _LightStep("Toon Light Threshold", Range(-1, 1)) = 0.1
        _LightFeather("Toon Light Softness", Range(0.001, 0.5)) = 0.02

        [Header(Stylized Specular)]
        _SpecularColor("Specular Highlight Color", Color) = (1, 1, 1, 1)
        _SpecularSize("Specular Size", Range(0.7, 0.999)) = 0.96
        _SpecularFeather("Specular Softness", Range(0.001, 0.1)) = 0.01

        [Header(Graphic Crystal Cracks)]
        _CrackColor("Crack Line Color", Color) = (1, 1, 1, 0.9)
        _CrackScale("Crack Scale/Density", Float) = 6.0
        _CrackCutoff("Crack Thickness", Range(0, 1)) = 0.65
        _CrackParallax("Inner Depth Shift", Range(0, 0.1)) = 0.03

        [Header(Anime Rim Outline)]
        _RimColor("Outer Rim Glow", Color) = (0.8, 0.95, 1.0, 1.0)
        _RimPower("Rim Width/Falloff", Range(0.5, 6)) = 3.0

        [Header(Stylized Distortion)]
        _RefractionStrength("Screen Distortion", Range(0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD3;
                float3 normalWS     : TEXCOORD4;
                float3 positionWS   : TEXCOORD7;
                float4 screenPos    : TEXCOORD8;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float _LightStep;
                float _LightFeather;
                float4 _SpecularColor;
                float _SpecularSize;
                float _SpecularFeather;
                float4 _CrackColor;
                float _CrackScale;
                float _CrackCutoff;
                float _CrackParallax;
                float4 _RimColor;
                float _RimPower;
                float _RefractionStrength;
            CBUFFER_END

            TEXTURE2D(_CameraOpaqueTexture);
            SAMPLER(sampler_CameraOpaqueTexture);

            // Fast procedural math to generate blocky, sharp vector-like stylized lines
            float cleanNoise(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453123);
            }

            float getStylizedCracks(float2 uv)
            {
                // Generate sharp crystalline grid lines using sine variations
                float2 g = sin(uv * 2.0 + cos(uv.yx * 1.4));
                float linePattern = abs(g.x + g.y);
                return linePattern;
            }

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 viewDirWS = normalize(input.viewDirWS);
                float3 normalWS = normalize(input.normalWS);

                // Get URP Main Light data (Shadows included)
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 lightDir = normalize(mainLight.direction);
                half shadowFactor = mainLight.shadowAttenuation;

                // 1. Stylized Cel-Lighting (Hard step transition)
                half NdotL = dot(normalWS, lightDir) * shadowFactor;
                // smoothstep creates a perfectly controlled toon gradient band
                half toonLight = smoothstep(_LightStep, _LightStep + _LightFeather, NdotL);
                half3 LitIceColor = lerp(_ShadowColor.rgb, _BaseColor.rgb, toonLight);

                // 2. Graphic Sharp Specular Highlight (The "Anime Shine")
                float3 halfDir = normalize(lightDir + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half specMask = smoothstep(_SpecularSize, _SpecularSize + _SpecularFeather, NdotH);
                half3 finalSpecular = _SpecularColor.rgb * specMask * mainLight.shadowAttenuation;

                // 3. Flat / Stylized Screen Refraction
                float2 screenUV = input.screenPos.xy / (input.screenPos.w + 0.00001);
                // Offset screen UVs slightly using object normal direction for fake glass bending
                float2 refractOffset = normalWS.xy * _RefractionStrength;
                half3 backgroundScene = SAMPLE_TEXTURE2D(_CameraOpaqueTexture, sampler_CameraOpaqueTexture, screenUV + refractOffset).rgb;

                // 4. Parallax Vector-Style Inner Cracks
                // Shift inner crack UVs based on the angle the player looks at it
                float2 crackOffset = viewDirWS.xy * _CrackParallax;
                float2 crackUV = (input.uv * _CrackScale) - crackOffset;
                float rawCracks = getStylizedCracks(crackUV);
                // Hard step turns the smooth sinewaves into crisp vector paint lines
                half crackMask = smoothstep(_CrackCutoff, _CrackCutoff + 0.05, rawCracks);
                half3 crackLayer = _CrackColor.rgb * crackMask * _CrackColor.a;

                // 5. Thick Colorful Toon Rim Glow
                half VdotN = saturate(dot(normalWS, viewDirWS));
                half rimMask = pow(1.0 - VdotN, _RimPower);
                half3 finalRimGlow = _RimColor.rgb * rimMask * _RimColor.a;

                // 6. Final Composition
                // First mix the flat background scene color with our stylized base colors
                half3 baseComposition = lerp(backgroundScene * LitIceColor, LitIceColor, _BaseColor.a);
                
                // Overlay graphic internal cracks
                baseComposition = lerp(baseComposition, crackLayer, crackMask * _CrackColor.a);

                // Add emissive highlights (Specular and Outer Rim) on top
                half3 finalColor = baseComposition + finalSpecular + finalRimGlow;

                // Set total opacity directly from the inspector color alpha channel
                half finalAlpha = saturate(_BaseColor.a + rimMask * _RimColor.a + specMask);

                return half4(finalColor, finalAlpha);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}