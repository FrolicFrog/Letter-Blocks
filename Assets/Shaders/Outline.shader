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

            // OPTIMIZATION: Converted all floats to half for mobile GPU speed
            half4 _MainTex_TexelSize;
            half _OutlineThickness;
            half4 _OutlineColor;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Early outs remain the same, they are already highly optimized
                half4 maskCenter = SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv);
                
                // If this pixel is Green (the Blocker layer) or Red (the Target object), immediately abort.
                if (maskCenter.g > 0.5h || maskCenter.r > 0.5h) 
                {
                    return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                }

                // Calculate orthogonal offsets using half precision
                half2 offsetX = half2(_MainTex_TexelSize.x * _OutlineThickness, 0.0h);
                half2 offsetY = half2(0.0h, _MainTex_TexelSize.y * _OutlineThickness);
                
                // Calculate diagonal offsets
                half2 offsetDiag1 = half2(offsetX.x, offsetY.y) * 0.707h;
                half2 offsetDiag2 = half2(offsetX.x, -offsetY.y) * 0.707h;
                
                half edge = 0.0h;
                
                // Check Orthogonal Edges (Up, Down, Left, Right)
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv + offsetX).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv - offsetX).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv + offsetY).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv - offsetY).r;
                
                // Check Diagonal Edges (Corners)
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv + offsetDiag1).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv - offsetDiag1).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv + offsetDiag2).r;
                edge += SAMPLE_TEXTURE2D(_OutlineMaskTexture, sampler_OutlineMaskTexture, input.uv - offsetDiag2).r;
                
                if (edge > 0.1h) 
                {
                    return _OutlineColor;
                }
                
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            }
            ENDHLSL
        }
    }
}