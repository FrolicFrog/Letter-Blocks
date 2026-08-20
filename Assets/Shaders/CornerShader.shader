Shader "UI/Custom/RedEdgeVignetteRounded"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _CenterColor ("Center Color", Color) = (0,0,0,1)
        _EdgeColor ("Edge Color", Color) = (0.5, 0, 0, 1)
        
        _Radius ("Vignette Spread", Range(0.0, 1.5)) = 0.8
        _Smoothness ("Vignette Smoothness", Range(0.0, 1.0)) = 0.5
        
        // NEW: Roundness slider
        _Roundness ("Corner Roundness", Range(0.0, 0.5)) = 0.1
        [Toggle] _TransparentCorners ("Transparent Corners?", Float) = 1

        // Required UI properties
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _CenterColor;
            fixed4 _EdgeColor;
            float _Radius;
            float _Smoothness;
            float _Roundness;
            float _TransparentCorners;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color;
                return OUT;
            }

            // Standard SDF function for a rounded box
            float sdRoundRect(float2 p, float2 b, float r)
            {
                float2 q = abs(p) - b + r;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                // 1. Shift UVs to center (-0.5 to 0.5)
                float2 uv = IN.texcoord - 0.5;
                
                // 2. Define the size of our UI box (0.5 extents covers the 0-1 UV range)
                float2 boxSize = float2(0.5, 0.5);
                
                // 3. Get the distance to the edge of the rounded box 
                // (Returns negative values inside the box, 0 at the edge, positive outside)
                float dist = sdRoundRect(uv, boxSize, _Roundness);
                
                // 4. Calculate Vignette (Map center (-0.5) to edge (0.0))
                float vignetteDist = (dist + 0.5) * 2.0; 
                float vignette = smoothstep(_Radius * (1.0 - _Smoothness), _Radius, vignetteDist);
                
                // 5. Apply Colors
                fixed4 gradColor = lerp(_CenterColor, _EdgeColor, vignette);
                color.rgb *= gradColor.rgb;
                color.a *= gradColor.a;

                // 6. Transparent Corners (Anti-aliased soft clip outside the box bounds)
                if (_TransparentCorners > 0.5)
                {
                    float alphaMask = smoothstep(0.01, -0.01, dist);
                    color.a *= alphaMask;
                }

                // UI Clipping Support
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}