Shader "Hidden/SimpleOutlineShader"
{
    Properties
    {
        _MainTex ("Camera Color", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "OutlinePass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_OutlineMaskTexture);
            SAMPLER(sampler_OutlineMaskTexture);

            float4 _MainTex_TexelSize;
            float _OutlineThickness;
            float4 _OutlineColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                float4 maskCenter = SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv);
                
                // If this pixel is Green (the Blocker layer), immediately abort.
                if (maskCenter.g > 0.5) return col;

                // If this pixel is Red (the Target object), immediately abort.
                if (maskCenter.r > 0.5) return col;

                // Calculate orthogonal offsets
                float2 offsetX = float2(_MainTex_TexelSize.x * _OutlineThickness, 0);
                float2 offsetY = float2(0, _MainTex_TexelSize.y * _OutlineThickness);
                
                // Calculate diagonal offsets (multiplied by 0.707 to maintain a perfect circular radius)
                float2 offsetDiag1 = float2(offsetX.x, offsetY.y) * 0.707;
                float2 offsetDiag2 = float2(offsetX.x, -offsetY.y) * 0.707;
                
                float edge = 0;
                
                // 1. Check Orthogonal Edges (Up, Down, Left, Right)
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv + offsetX).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv - offsetX).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv + offsetY).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv - offsetY).r;
                
                // 2. Check Diagonal Edges (Corners) to fill in the gaps
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv + offsetDiag1).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv - offsetDiag1).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv + offsetDiag2).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv - offsetDiag2).r;
                
                // If we detected a red pixel anywhere in the radius, draw the outline
                if (edge > 0.1) 
                {
                    return _OutlineColor;
                }
                
                return col;
            }
            ENDHLSL
        }
    }
}