Shader "UI/AdaptiveMultiCutout"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _OverlayColor ("Dim Color", Color) = (0, 0, 0, 0.75)
        _CornerRadius ("Corner Roundness", Range(0.0, 0.05)) = 0.015
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.05)) = 0.01
        _Aspect ("Screen Aspect Ratio (W/H)", Float) = 0.5625
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

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #define MAX_CUTOUTS 8

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
            };

            fixed4 _OverlayColor;
            float _CornerRadius;
            float _EdgeSoftness;
            float _Aspect;

            int _CutoutCount;
            float4 _Centers[MAX_CUTOUTS];   // xy = Center Viewport Pos
            float4 _HalfSizes[MAX_CUTOUTS]; // xy = Half Dimensions

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.uv = v.texcoord;
                return OUT;
            }

            // Signed Distance Function for 2D Rounded Box
            float sdRoundedBox(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float minDistance = 1000.0;

                // Loop through all active cutouts and punch holes
                for (int i = 0; i < _CutoutCount; i++)
                {
                    float2 p = (IN.uv - _Centers[i].xy);
                    p.x *= _Aspect;

                    float2 b = _HalfSizes[i].xy;
                    b.x *= _Aspect;

                    float dist = sdRoundedBox(p, b, _CornerRadius);
                    minDistance = min(minDistance, dist);
                }

                // If no cutouts, draw full overlay
                if (_CutoutCount <= 0)
                {
                    minDistance = 1.0;
                }

                float alpha = smoothstep(0.0, _EdgeSoftness, minDistance);
                return fixed4(_OverlayColor.rgb, _OverlayColor.a * alpha);
            }
            ENDCG
        }
    }
}