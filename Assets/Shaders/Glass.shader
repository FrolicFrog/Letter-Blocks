Shader "Custom/GameScreenGlass"
{
    Properties
    {
       
        _GlassTint ("Glass Tint & Opacity", Color) = (0.9, 0.9, 1.0, 0.4) // Semi-transparent bluish tint

      
        _CloudColor ("Cloud Spot Color & Opacity", Color) = (1.0, 1.0, 1.0, 0.7) // Bright, soft haze spots
        _CloudPatternScale ("Cloud Haze Pattern Scale", Float) = 2.0 // Controls texture frequency
        _CloudHazeSoftness ("Haze Diffusion (Softness)", Range(0.0, 1.0)) = 0.5 // Controls blur of clouds
        _CloudDensityThreshold ("Cloud Density Threshold", Range(0.0, 1.0)) = 0.5 // Controls cloud density

       
        _RimColor ("Edge Rim Light Color", Color) = (1.0, 1.0, 1.0, 1.0) // Soft, bright rim
        _RimPower ("Edge Rim Light Power", Range(0.1, 10)) = 3.0 // Width of the edge glow

      
        _Spot1Pos ("Spot 1 Position (UV)", Vector) = (0.4, 0.6, 0.2, 0.0) // (X, Y, Size, [unused])
        _Spot2Pos ("Spot 2 Position (UV)", Vector) = (0.7, 0.3, 0.15, 0.0) // (X, Y, Size, [unused])
        _Spot3Pos ("Spot 3 Position (UV)", Vector) = (0.2, 0.3, 0.3, 0.0) // (X, Y, Size, [unused])
    }

    SubShader
    {
        // Transparent shader properties
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        LOD 100

        Pass
        {
            Name "ForwardLit"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc" // For basic lit support

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 vertex : SV_POSITION;
                float3 viewDir : TEXCOORD3;
            };

            // Property Declarations
            fixed4 _GlassTint;
            fixed4 _CloudColor;
            fixed4 _RimColor;
            float _RimPower;
            float _CloudPatternScale;
            float _CloudHazeSoftness;
            float _CloudDensityThreshold;
            float4 _Spot1Pos;
            float4 _Spot2Pos;
            float4 _Spot3Pos;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            // Simple pseudo-noise function
            float pseudoNoise(float2 uv)
            {
                return frac(sin(dot(uv, float2(12.9898, 78.233))) * 43758.5453);
            }

            // Fractal Brownian Motion for complex haze
            float fractalNoise(float2 uv)
            {
                float scale = _CloudPatternScale;
                float value = 0.0;
                float amplitude = 1.0;
                float persistence = 0.5;
                float lacunarity = 2.0;

                for (int i = 0; i < 3; i++)
                {
                    value += pseudoNoise(uv * scale) * amplitude;
                    scale *= lacunarity;
                    amplitude *= persistence;
                }
                return value;
            }

            // Function to generate a soft, position-based spot
            float softSpot(float2 uv, float2 pos, float size, float noiseHaze)
            {
                float dist = distance(uv, pos);
                // Define soft inner and outer edges for diffusion
                float innerEdge = size * (1.0 - _CloudHazeSoftness * 0.5);
                float outerEdge = size * (1.0 + _CloudHazeSoftness * 0.5);
                
                float spotMask = 1.0 - smoothstep(innerEdge, outerEdge, dist);
                // Blend spot mask with noise haze for natural integration
                float blendedMask = saturate(spotMask - noiseHaze * 0.3 * _CloudHazeSoftness);
                return blendedMask;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normalWS = normalize(i.normal);
                float3 viewDirWS = normalize(i.viewDir);

                // --- 1. Base Glass Tint ---
                fixed4 col = _GlassTint;

                // --- 2. Procedural Haze & Spots ---
                // Generate natural haze texture
                float noiseHaze = fractalNoise(i.uv);
                float naturalHaze = smoothstep(_CloudDensityThreshold, 1.0, noiseHaze) * 0.3; // Low contrast general haze

                // Calculate soft spots with noise blending for each position
                float spotMask1 = softSpot(i.uv, _Spot1Pos.xy, _Spot1Pos.z, noiseHaze);
                float spotMask2 = softSpot(i.uv, _Spot2Pos.xy, _Spot2Pos.z, noiseHaze);
                float spotMask3 = softSpot(i.uv, _Spot3Pos.xy, _Spot3Pos.z, noiseHaze);

                // Combine spots and haze, accounting for diffusion softness
                float combinedCloudDensity = saturate(naturalHaze + (spotMask1 + spotMask2 + spotMask3) * 1.5);
                combinedCloudDensity *= _CloudColor.a; // Apply property cloud opacity

                fixed3 cloudEffect = _CloudColor.rgb * combinedCloudDensity;

                // Combine cloud with base glass color
                // The clouds are brighter and more opaque, acting like volumetric haze.
                col.rgb = lerp(col.rgb, cloudEffect, combinedCloudDensity);
                col.a = saturate(col.a + combinedCloudDensity * 0.8); // Make clouds more opaque but still soft

                // --- 3. Fresnel Rim Light ---
                // Create a glow on the edge of the glass
                float NdotV = 1.0 - saturate(dot(normalWS, viewDirWS));
                float rim = pow(NdotV, _RimPower);
                
                // Add rim glow contribution
                col.rgb += _RimColor.rgb * rim * _RimColor.a;
                col.a = saturate(col.a + rim * 0.5); // Rim contributes slightly to overall opacity

                return col;
            }
            ENDCG
        }
    }
}