Shader "Custom/UI/IceCrystalsSafeZone"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HDR] _SnowColor ("Crystal Color", Color) = (1, 1, 2, 1)
        _GlowIntensity ("Core Brightness", Range(1, 10)) = 3.0
        _BloomSpread ("Fake Bloom Radius", Range(1, 6)) = 3.0
        
        // Center Safe Zone Properties
        _ClearWidth ("Center Clear Width", Range(0.0, 1.0)) = 0.75
        _ClearHeight ("Center Clear Height", Range(0.0, 1.0)) = 0.75
        _ClearSoftness ("Clear Edge Softness", Range(0.01, 1.0)) = 0.2
        
        _SnowSpeed ("Drift Speed", Range(0, 5)) = 0.5
        _SnowDensity ("Grid Density", Range(1, 20)) = 4.0
        _ParticleSize ("Base Particle Size", Range(0.1, 5.0)) = 1.0
        _RotationSpeed ("Rotation Speed", Range(0, 5)) = 1.0
        _TwinkleSpeed ("Twinkle Speed", Range(0, 10)) = 4.0
        
        // Required UI Masking properties
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float2 localPos  : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _SnowColor;
            float _GlowIntensity;
            float _BloomSpread;
            
            float _ClearWidth;
            float _ClearHeight;
            float _ClearSoftness;
            
            float _SnowSpeed;
            float _SnowDensity;
            float _ParticleSize;
            float _RotationSpeed;
            float _TwinkleSpeed;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.texcoord = v.texcoord;
                o.localPos = v.vertex.xy * 0.01; 
                o.color = v.color * _Color;
                return o;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            float2 rotate(float2 uv, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
            }

            float getCrystal(float2 uv, float size, float rotAngle)
            {
                uv = rotate(uv, rotAngle);
                float angle = atan2(uv.y, uv.x);
                float radius = length(uv);
                
                float spikes = cos(angle * 6.0) * 0.5 + 0.5;
                float shapeDist = size * (0.2 + spikes * 0.8); 
                
                float core = smoothstep(size * 0.3, 0.0, radius);
                float branches = smoothstep(shapeDist, shapeDist * 0.1, radius);
                float fakeBloom = smoothstep(shapeDist * _BloomSpread, 0.0, radius) * 0.5; 
                
                return saturate(core + branches) + fakeBloom; 
            }

            float getCrystalLayer(float2 pos, float density, float speed, float offset, float globalSize)
            {
                pos *= density;
                pos.y -= _Time.y * speed;
                pos.x += sin(_Time.y * speed * 0.5 + pos.y) * 0.2; 
                
                float2 id = floor(pos);
                float2 f = frac(pos);
                
                float h = hash(id + offset);
                float2 cellPos = float2(hash(id + offset + 1.0), hash(id + offset + 2.0)) * 0.6 + 0.2;
                
                float size = (h * 0.3 + 0.1) * globalSize / density;
                float rotAngle = _Time.y * _RotationSpeed * (h > 0.5 ? 1.0 : -1.0) + h * 6.28;
                
                float twinkle = sin(_Time.y * _TwinkleSpeed + h * 31.4) * 0.5 + 0.5;
                twinkle = lerp(0.3, 1.0, twinkle); 
                
                float crystal = getCrystal(f - cellPos, size, rotAngle) * twinkle;
                float mask = step(0.7, h); 
                return crystal * mask;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half4 baseColor = tex2D(_MainTex, i.texcoord) * i.color;
                
                // Procedural Vignette Mask calculation (0 in center, 1 at edges)
                float2 distFromCenter = abs(i.texcoord - 0.5) * 2.0;
                float maskX = smoothstep(_ClearWidth - _ClearSoftness, _ClearWidth, distFromCenter.x);
                float maskY = smoothstep(_ClearHeight - _ClearSoftness, _ClearHeight, distFromCenter.y);
                float safeZoneMask = saturate(maskX + maskY);
                
                float layer1 = getCrystalLayer(i.localPos, _SnowDensity, _SnowSpeed, 0.0, _ParticleSize);
                float layer2 = getCrystalLayer(i.localPos, _SnowDensity * 1.5, _SnowSpeed * 0.6, 5.0, _ParticleSize);
                
                float totalCrystals = (layer1 + layer2) * _GlowIntensity;

                // Combine original alpha inversion with the new safe zone mask
                float inverseAlpha = 1.0 - baseColor.a;
                float finalParticleMask = inverseAlpha * safeZoneMask;
                
                float crystalAlpha = saturate(totalCrystals) * finalParticleMask;
                float crystalEmission = totalCrystals * finalParticleMask;

                fixed4 finalColor = baseColor;
                finalColor.rgb = finalColor.rgb + (_SnowColor.rgb * crystalEmission);
                finalColor.a = saturate(baseColor.a + crystalAlpha);
                
                #if UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}