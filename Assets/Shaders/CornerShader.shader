Shader "CustomUI/SpriteEdgeTracer"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BorderColor ("Border Color", Color) = (0.45, 0.35, 0.8, 1) // Purple
        _CenterColor ("Center Color", Color) = (0, 0, 0, 1)         // Black
        _BorderThickness ("Border Thickness", Range(0.0, 50.0)) = 5.0
        _Softness ("Edge Softness", Range(0.01, 1.0)) = 0.1

        // Hidden Stencil/Mask properties so Unity UI can manage them automatically
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [HideInInspector] _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
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
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

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
            float4 _MainTex_TexelSize; // To get accurate pixel offsets
            fixed4 _BorderColor;
            fixed4 _CenterColor;
            float _BorderThickness;
            float _Softness;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Sample original sprite
                fixed4 baseColor = tex2D(_MainTex, IN.texcoord);
                float alpha = baseColor.a;

                // If pixel is fully transparent, skip calculations
                if (alpha < 0.01) return fixed4(0,0,0,0);

                // Multi-tap surrounding pixels based on exact Texel Size
                float2 offset = _MainTex_TexelSize.xy * _BorderThickness;
                
                float a1 = tex2D(_MainTex, IN.texcoord + float2(offset.x, 0)).a;
                float a2 = tex2D(_MainTex, IN.texcoord + float2(-offset.x, 0)).a;
                float a3 = tex2D(_MainTex, IN.texcoord + float2(0, offset.y)).a;
                float a4 = tex2D(_MainTex, IN.texcoord + float2(0, -offset.y)).a;

                // If any neighboring pixel is transparent, we are near the edge
                float minAlpha = min(min(a1, a2), min(a3, a4));
                
                // Determine if we are on the border (0) or in the center (1)
                float isCenter = smoothstep(0.5 - _Softness, 0.5 + _Softness, minAlpha);

                // Blend the Border Color and Center Color based on position
                fixed4 finalColor = lerp(_BorderColor, _CenterColor, isCenter);
                
                // Preserve the original sprite's transparency shape
                finalColor.a = alpha;
                finalColor *= IN.color; // Support CanvasGroup Alpha

                // Standard UI Masking support
                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                return finalColor;
            }
            ENDCG
        }
    }
}