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
        _ShineAngle ("Shine Angle", Range(0.0, 360.0)) = 180.0 // Added Angle Slider

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

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(half, _SideDarkness)
                UNITY_DEFINE_INSTANCED_PROP(half, _BottomDarkness)
                
                UNITY_DEFINE_INSTANCED_PROP(half, _Enable_Shine)
                UNITY_DEFINE_INSTANCED_PROP(half4, _ShineColor)
                UNITY_DEFINE_INSTANCED_PROP(half, _ShineSize)
                UNITY_DEFINE_INSTANCED_PROP(half, _ShineSoftness)
                UNITY_DEFINE_INSTANCED_PROP(half, _ShineAngle) // Instanced Angle

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
                
                // Parallel view rays for orthographic consistency
                o.viewDir = -float3(unity_CameraToWorld._m02, unity_CameraToWorld._m12, unity_CameraToWorld._m22);
                
                return o;
            }

            half4 frag (v2f i) : SV_Target 
            {
                UNITY_SETUP_INSTANCE_ID(i);

                half3 normal = normalize(i.worldNormal);
                half3 viewDir = normalize(i.viewDir);

                half topMask = saturate(normal.y);
                half bottomMask = saturate(-normal.y);

                // Access Instanced Properties
                half4 mainColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                half sideDark = UNITY_ACCESS_INSTANCED_PROP(Props, _SideDarkness);
                half bottomDark = UNITY_ACCESS_INSTANCED_PROP(Props, _BottomDarkness);
                
                half enableShine = UNITY_ACCESS_INSTANCED_PROP(Props, _Enable_Shine);
                half4 shineColor = UNITY_ACCESS_INSTANCED_PROP(Props, _ShineColor);
                half shineSize = UNITY_ACCESS_INSTANCED_PROP(Props, _ShineSize);
                half shineSoftness = UNITY_ACCESS_INSTANCED_PROP(Props, _ShineSoftness);
                half shineAngle = UNITY_ACCESS_INSTANCED_PROP(Props, _ShineAngle); // Get the angle

                half enableHL = UNITY_ACCESS_INSTANCED_PROP(Props, _Enable_Highlights);
                half4 rimColor = UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor);
                half fresnelPower = UNITY_ACCESS_INSTANCED_PROP(Props, _FresnelPower);

                // --- 1. Base Shading ---
                half4 sideColor = half4(mainColor.rgb * sideDark, mainColor.a);
                half4 bottomColor = half4(mainColor.rgb * bottomDark, mainColor.a);

                half4 finalColor = sideColor;
                finalColor = lerp(finalColor, bottomColor, bottomMask);
                finalColor = lerp(finalColor, mainColor, topMask);

                // --- 2. Fake Shine (Glossy Specular) ---
                // Convert degrees to radians (pi / 180 = 0.0174533)
                half rad = shineAngle * 0.0174533h;
                
                // Calculate the rotated light direction around the Y-axis
                half3 fakeLightDir = normalize(half3(sin(rad), 1.0h, cos(rad)));
                
                half3 halfVector = normalize(fakeLightDir + viewDir);
                half nDotH = saturate(dot(normal, halfVector));
                
                half shineThreshold = 1.0h - shineSize;
                half shineIntensity = smoothstep(shineThreshold, shineThreshold + shineSoftness, nDotH);
                
                finalColor.rgb += shineColor.rgb * shineIntensity * shineColor.a * enableShine;

                // --- 3. Proper Fresnel Rim Light ---
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