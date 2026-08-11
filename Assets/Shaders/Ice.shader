Shader "Custom/StylizedCartoonyIce_Procedural"
{
    Properties
    {
        [Header(Ice Surface and Core Settings)]
        _Color ("Ice Surface Color Top", Color) = (0.35, 0.75, 1.0, 1.0)
        _CoreColor ("Ice Core Color Sides", Color) = (0.1, 0.45, 0.8, 1.0)
        _BottomDarkness ("Deep Ice Darkness", Range(0.0, 1.0)) = 0.5

        [Header(Procedural Frost Pattern)]
        [Toggle] _Enable_Procedural ("Enable Procedural Frost", Float) = 1.0
        _ProceduralScale ("Frost Scale", Range(1.0, 50.0)) = 15.0
        _FrostIntensity ("Frost Intensity", Range(0.0, 1.0)) = 0.3
        _FrostColor ("Procedural Frost Color", Color) = (0.8, 0.95, 1.0, 1.0)

        [Header(Fake Specular Glare)]
        [Toggle] _Enable_Shine ("Enable Ice Glare", Float) = 1.0
        _ShineColor ("Glare Color", Color) = (1.0, 1.0, 1.0, 0.8)
        _ShineSize ("Glare Size", Range(0.01, 1.0)) = 0.1
        _ShineSoftness ("Glare Softness", Range(0.0, 0.5)) = 0.05
        _ShineAngle ("Light Angle for glare position", Range(0.0, 360.0)) = 210.0

        [Header(Edge Definition)]
        [Toggle] _Enable_Highlights ("Enable Stylized Edges", Float) = 1.0
        _RimColor ("Edge Frost Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _FresnelPower ("Edge Spread Higher is thinner", Range(0.1, 10.0)) = 4.0

        [Header(Text or Number Overlay)]
        [MainTexture] _BaseMap("Number Texture", 2D) = "white" {}
        [Toggle] _MultiplyText("Multiply (Check if texture has white background)", Float) = 1.0
        _TextOpacity ("Texture Opacity", Range(0.0, 1.0)) = 1.0

        [Header(Render State Fixes)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 1 
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 0 
        [Enum(Off, 0, On, 1)] _ZWrite ("Depth Write ZWrite", Float) = 1 
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling Mode", Float) = 2 
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing 
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float3 positionWS   : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Color;
                half4 _CoreColor;
                half _BottomDarkness;
                
                half _Enable_Procedural;
                half _ProceduralScale;
                half _FrostIntensity;
                half4 _FrostColor;
                
                half _Enable_Shine;
                half4 _ShineColor;
                half _ShineSize;
                half _ShineSoftness;
                half _ShineAngle;

                half _Enable_Highlights;
                half4 _RimColor;
                half _FresnelPower;

                half _MultiplyText;
                half _TextOpacity;
            CBUFFER_END

            // --- Procedural Noise Functions ---
            float hash(float2 p) 
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float proceduralNoise(float2 uv) 
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f); // Smoothstep interpolation
                
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.positionWS = vertexInput.positionWS; // Passed for 3D procedural mapping
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
                return output;
            }

            half4 frag (Varyings input) : SV_Target 
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                // --- 1. Base Ice Colors ---
                half topMask = saturate(normalWS.y);
                topMask = pow(topMask, 4.0);
                half bottomMask = saturate(-normalWS.y);

                half4 topColor = _Color;
                half4 sideColor = _CoreColor;
                half4 bottomColor = half4(_CoreColor.rgb * _BottomDarkness, _CoreColor.a);

                half4 finalColor = sideColor;
                finalColor = lerp(finalColor, bottomColor, bottomMask);
                finalColor = lerp(finalColor, topColor, topMask);

                // --- 2. Procedural Frost ---
                // We use world position so the noise flows continuously across blocks
                if (_Enable_Procedural > 0.5)
                {
                    float2 noiseUV = input.positionWS.xz + input.positionWS.xy; 
                    float noiseVal = proceduralNoise(noiseUV * _ProceduralScale);
                    // Sharpen the noise slightly for an icy look
                    noiseVal = smoothstep(0.3, 0.7, noiseVal); 
                    
                    finalColor.rgb = lerp(finalColor.rgb, _FrostColor.rgb, noiseVal * _FrostIntensity);
                }

                // --- 3. Fake Specular Glare ---
                half rad = _ShineAngle * 0.0174533h; 
                half3 fakeLightDir = normalize(half3(sin(rad), 0.5h, cos(rad)));
                
                half3 halfVector = normalize(fakeLightDir + viewDirWS);
                half nDotH = saturate(dot(normalWS, halfVector));
                
                half shineThreshold = 1.0h - _ShineSize;
                half shineIntensity = smoothstep(shineThreshold, shineThreshold + _ShineSoftness, nDotH);
                
                finalColor.rgb = lerp(finalColor.rgb, _ShineColor.rgb, shineIntensity * _ShineColor.a * _Enable_Shine);

                // --- 4. Stylized Edges ---
                half nDotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0h - nDotV, _FresnelPower); 
                half stylizedBorderMask = step(0.5, fresnel); 

                if (_Enable_Highlights > 0.0)
                {
                    finalColor.rgb = lerp(finalColor.rgb, _RimColor.rgb, stylizedBorderMask * _RimColor.a);
                }

                // --- 5. Text/Number Texture Overlay ---
                half4 textTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
                if (_MultiplyText > 0.5)
                {
                    half3 multipliedColor = finalColor.rgb * textTex.rgb;
                    finalColor.rgb = lerp(finalColor.rgb, multipliedColor, _TextOpacity);
                }
                else
                {
                    finalColor.rgb = lerp(finalColor.rgb, textTex.rgb, textTex.a * _TextOpacity);
                }

                finalColor.rgb = saturate(finalColor.rgb);
                return finalColor;
            }
            ENDHLSL
        }
    }
}