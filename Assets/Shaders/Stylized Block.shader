Shader "Custom/StylizedBlockOptimized"
{
    Properties
    {
        [Header(Main Block Settings)]
        _Color ("Main Color (Top)", Color) = (1.0, 0.7, 0.1, 1.0)
        _SideDarkness ("Side Darkness Multiplier", Range(0.0, 1.0)) = 0.75
        _BottomDarkness ("Bottom Darkness Multiplier", Range(0.0, 1.0)) = 0.5

        [Header(Fake Shine (Gloss))]
        [Toggle] _Enable_Shine ("Enable Fake Shine", Float) = 1.0
        _ShineColor ("Shine Color", Color) = (1.0, 1.0, 1.0, 0.4)
        _ShineSize ("Shine Size", Range(0.01, 1.0)) = 0.07
        _ShineSoftness ("Shine Softness", Range(0.0, 0.5)) = 0.15
        _ShineAngle ("Shine Angle", Range(0.0, 360.0)) = 180.0

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
            #pragma multi_compile_instancing 
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL; // Upgraded to float3 for better vertex precision
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0; // Upgraded to float3 to prevent truncation artifacts
                float3 viewDir : TEXCOORD1;     // Upgraded to float3 to prevent truncation artifacts
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _SideDarkness)
                UNITY_DEFINE_INSTANCED_PROP(float, _BottomDarkness)
                
                UNITY_DEFINE_INSTANCED_PROP(float, _Enable_Shine)
                UNITY_DEFINE_INSTANCED_PROP(float4, _ShineColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShineSize)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShineSoftness)
                UNITY_DEFINE_INSTANCED_PROP(float, _ShineAngle)

                UNITY_DEFINE_INSTANCED_PROP(float, _Enable_Highlights)
                UNITY_DEFINE_INSTANCED_PROP(float4, _RimColor)
                UNITY_DEFINE_INSTANCED_PROP(float, _FresnelPower)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                
                // Parallel view rays for orthographic consistency
                o.viewDir = -float3(unity_CameraToWorld._m02, unity_CameraToWorld._m12, unity_CameraToWorld._m22);
                
                return o;
            }

            float4 frag (v2f i) : SV_Target 
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Use float3 for all directional math to eliminate highlight mismatch
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                float topMask = saturate(normal.y);
                float bottomMask = saturate(-normal.y);

                // Access Instanced Properties
                float4 mainColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float sideDark = UNITY_ACCESS_INSTANCED_PROP(Props, _SideDarkness);
                float bottomDark = UNITY_ACCESS_INSTANCED_PROP(Props, _BottomDarkness);
                
                float enableShine = UNITY_ACCESS_INSTANCED_PROP(Props, _Enable_Shine);
                float4 shineColor = UNITY_ACCESS_INSTANCED_PROP(Props, _ShineColor);
                float shineSize = UNITY_ACCESS_INSTANCED_PROP(Props, _ShineSize);
                float shineSoftness = UNITY_ACCESS_INSTANCED_PROP(Props, _ShineSoftness);
                float shineAngle = UNITY_ACCESS_INSTANCED_PROP(Props, _ShineAngle);

                float enableHL = UNITY_ACCESS_INSTANCED_PROP(Props, _Enable_Highlights);
                float4 rimColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);
                float fresnelPower = UNITY_ACCESS_INSTANCED_PROP(Props, _FresnelPower);

                // --- 1. Base Shading ---
                float4 sideColor = float4(mainColor.rgb * sideDark, mainColor.a);
                float4 bottomColor = float4(mainColor.rgb * bottomDark, mainColor.a);

                float4 finalColor = sideColor;
                finalColor = lerp(finalColor, bottomColor, bottomMask);
                finalColor = lerp(finalColor, mainColor, topMask);

                // --- 2. Fake Shine (Glossy Specular) ---
                float rad = shineAngle * 0.0174533f;
                
                float3 fakeLightDir = normalize(float3(sin(rad), 1.0f, cos(rad)));
                
                float3 halfVector = normalize(fakeLightDir + viewDir);
                float nDotH = saturate(dot(normal, halfVector));
                
                float shineThreshold = 1.0f - shineSize;
                float shineIntensity = smoothstep(shineThreshold, shineThreshold + shineSoftness, nDotH);
                
                finalColor.rgb += shineColor.rgb * shineIntensity * shineColor.a * enableShine;

                // --- 3. Proper Fresnel Rim Light ---
                float nDotV = saturate(dot(normal, viewDir));
                float fresnel = pow(1.0f - nDotV, fresnelPower); 
                float3 appliedRim = rimColor.rgb * fresnel * enableHL;
                
                finalColor.rgb += appliedRim;
                return finalColor;
            }
            ENDCG
        }
    }
}