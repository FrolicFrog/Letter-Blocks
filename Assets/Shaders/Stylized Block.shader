Shader "Custom/StylizedBlockOptimized"
{
    Properties
    {
        [Header(Main Block Settings)]
        _Color ("Main Color (Top)", Color) = (1.0, 0.7, 0.1, 1.0)
        _SideDarkness ("Side Darkness Multiplier", Range(0.0, 1.0)) = 0.75
        _BottomDarkness ("Bottom Darkness Multiplier", Range(0.0, 1.0)) = 0.5

        [Header(Fresnel Rim Light)]
        [Toggle] _Enable_Highlights ("Enable Rim Light", Float) = 1.0
        _RimColor ("Rim Color", Color) = (1.0, 1.0, 1.0, 0.5)
        _FresnelPower ("Fresnel Power", Range(0.1, 10.0)) = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 1. ADDED GPU INSTANCING SUPPORT
            #pragma multi_compile_instancing 
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                half3 normal : NORMAL; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 worldNormal : TEXCOORD0; 
                half3 viewDir : TEXCOORD1; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // 2. WRAPPED PROPERTIES IN INSTANCING BLOCK
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half, _SideDarkness)
                UNITY_DEFINE_INSTANCED_PROP(half, _BottomDarkness)
                UNITY_DEFINE_INSTANCED_PROP(half, _Enable_Highlights)
                UNITY_DEFINE_INSTANCED_PROP(half4, _RimColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _FresnelPower)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            // 3. SWITCHED FROM FLOAT TO HALF PRECISION
            half4 frag (v2f i) : SV_Target 
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half3 normal = normalize(i.worldNormal);
                half3 viewDir = normalize(i.viewDir);

                // 4. REPLACED CLAMP WITH SATURATE
                half topMask = saturate(normal.y);
                half bottomMask = saturate(-normal.y);

                half4 mainColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                half sideDark = UNITY_ACCESS_INSTANCED_PROP(Props, _SideDarkness);
                half bottomDark = UNITY_ACCESS_INSTANCED_PROP(Props, _BottomDarkness);
                half enableHL = UNITY_ACCESS_INSTANCED_PROP(Props, _Enable_Highlights);
                half4 rimColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);
                half fresnelPower = UNITY_ACCESS_INSTANCED_PROP(Props, _FresnelPower);

                half4 sideColor = half4(mainColor.rgb * sideDark, mainColor.a);
                half4 bottomColor = half4(mainColor.rgb * bottomDark, mainColor.a);

                half4 finalColor = sideColor;
                finalColor = lerp(finalColor, bottomColor, bottomMask);
                finalColor = lerp(finalColor, mainColor, topMask);

                half nDotV = saturate(dot(normal, viewDir));
                half fresnel = pow(1.0h - nDotV, fresnelPower); 
                half3 appliedRim = rimColor.rgb * fresnel * enableHL;
                
                finalColor.rgb += appliedRim;

                return finalColor;
            }
            ENDCG
        }
    }
}