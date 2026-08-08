Shader "Custom/StylizedURPIce"
{
    Properties
    {
        [Header(Ice Surface and Core Settings)]
        _Color ("Ice Surface Color (Top)", Color) = (0.7, 0.9, 1.0, 0.8)
        _CoreColor ("Ice Core Color (Sides)", Color) = (0.3, 0.6, 0.9, 0.95)
        _BottomDarkness ("Deep Ice Darkness", Range(0.0, 1.0)) = 0.6

        [Header(Ice Glare (Fake Specular))]
        [Toggle] _Enable_Shine ("Enable Ice Glare", Float) = 1.0
        _ShineColor ("Glare Color", Color) = (1.0, 1.0, 1.0, 0.6)
        _ShineSize ("Glare Size", Range(0.01, 1.0)) = 0.12
        _ShineSoftness ("Glare Softness", Range(0.0, 0.5)) = 0.05
        _ShineAngle ("Light Angle", Range(0.0, 360.0)) = 180.0

        [Header(Frost Edge (Fresnel))]
        [Toggle] _Enable_Highlights ("Enable Frost Edge", Float) = 1.0
        _RimColor ("Frost Color", Color) = (0.9, 0.95, 1.0, 1.0)
        _FresnelPower ("Frost Spread", Range(0.1, 10.0)) = 2.5

        [Header(Render State Fixes (Invisible Wall Fix))]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5 
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 10 
        [Enum(Off, 0, On, 1)] _ZWrite ("Depth Write (ZWrite)", Float) = 0 
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Culling Mode", Float) = 2 
    }
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
        }
        LOD 100

        Pass
        {
            Name "Unlit"
            
            // These lines now pull directly from the exposed properties
            // This prevents Unity's hidden material cache from overriding your transparency!
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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CoreColor;
                half _BottomDarkness;
                
                half _Enable_Shine;
                half4 _ShineColor;
                half _ShineSize;
                half _ShineSoftness;
                half _ShineAngle;

                half _Enable_Highlights;
                half4 _RimColor;
                half _FresnelPower;
            CBUFFER_END

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
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(vertexInput.positionWS);
                
                return output;
            }

            half4 frag (Varyings input) : SV_Target 
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half3 normalWS = normalize(input.normalWS);
                half3 viewDirWS = normalize(input.viewDirWS);

                half topMask = saturate(normalWS.y);
                half bottomMask = saturate(-normalWS.y);

                half4 topColor = _Color;
                half4 sideColor = _CoreColor;
                half4 bottomColor = half4(_CoreColor.rgb * _BottomDarkness, _CoreColor.a);

                half4 finalColor = sideColor;
                finalColor = lerp(finalColor, bottomColor, bottomMask);
                finalColor = lerp(finalColor, topColor, topMask);

                half rad = _ShineAngle * 0.0174533h; 
                half3 fakeLightDir = normalize(half3(sin(rad), 1.0h, cos(rad)));
                
                half3 halfVector = normalize(fakeLightDir + viewDirWS);
                half nDotH = saturate(dot(normalWS, halfVector));
                
                half shineThreshold = 1.0h - _ShineSize;
                half shineIntensity = smoothstep(shineThreshold, shineThreshold + _ShineSoftness, nDotH);
                
                finalColor.rgb += _ShineColor.rgb * shineIntensity * _ShineColor.a * _Enable_Shine;

                half nDotV = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0h - nDotV, _FresnelPower); 
                half3 appliedFrost = _RimColor.rgb * fresnel * _Enable_Highlights;
                
                finalColor.rgb += appliedFrost;
                finalColor.rgb = saturate(finalColor.rgb);

                return finalColor;
            }
            ENDHLSL
        }
    }
}