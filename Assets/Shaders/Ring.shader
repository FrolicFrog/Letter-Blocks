Shader "Custom/AnimatedRing"
{
    Properties
    {
        // This line satisfies the SpriteRenderer requirement
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {} 
        
        _Color ("Ring Color", Color) = (0.13, 0.13, 0.13, 1)
        _OuterRadius ("Outer Radius", Range(0.0, 0.5)) = 0.45
        _InnerRadius ("Inner Radius", Range(0.0, 0.5)) = 0.35
    }
    SubShader
    {
        // Added some standard Sprite tags for better compatibility
        Tags { 
            "RenderType"="Transparent" 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "PreviewType"="Plane" 
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex; // Declare it here so it compiles correctly
            float4 _Color;
            float _OuterRadius;
            float _InnerRadius;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate distance from the center (0.5, 0.5)
                float dist = distance(i.uv, float2(0.5, 0.5));
                
                // Create sharp edges for the ring
                float ringAlpha = step(dist, _OuterRadius) * step(_InnerRadius, dist);
                
                return fixed4(_Color.rgb, _Color.a * ringAlpha);
            }
            ENDCG
        }
    }
    }