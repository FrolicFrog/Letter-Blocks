Shader "Custom/StylizedBlockOptimizedTransparent"
{
    Properties
    {
        [Header(Main Block Settings)]
        _Color ("Main Color (Top)", Color) = (1.0, 0.7, 0.1, 1.0)
        _Transparency ("Transparency (Opacity)", Range(0.0, 1.0)) = 0.8
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

        [Header(Sweeping Reflection (Sheen))]
        [Toggle] _UseSweep ("Enable Sweeping Reflection", Float) = 0.0 
        [Toggle] _SweepStatic ("Static Sweep (No Movement)", Float) = 0.0
        _SweepPosition ("Static Sweep Position", Range(-1.0, 2.0)) = 0.5
        _SweepColor ("Sweep Color", Color) = (1.0, 1.0, 1.0, 0.6)
        
        // --- NEW START/END BOUNDS ---
        _SweepStart ("Sweep Start Pos", Float) = 1.5
        _SweepEnd ("Sweep End Pos", Float) = -0.5
        
        _SweepSpeed ("Sweep Speed", Range(0.1, 5.0)) = 1.5
        _SweepDelay ("Sweep Delay (Seconds)", Range(0.0, 10.0)) = 0.0 
        _SweepWidth ("Sweep Width", Range(0.01, 1.0)) = 0.2
        _SweepAngle ("Sweep Angle", Range(0.0, 360.0)) = 45.0
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "IgnoreProjector"="True" 
        }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2; 
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float, _Transparency)
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

                UNITY_DEFINE_INSTANCED_PROP(float, _UseSweep) 
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepStatic)
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepPosition)
                UNITY_DEFINE_INSTANCED_PROP(float4, _SweepColor)
                
                // Registered new variables for instancing
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepStart)
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepEnd)
                
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepSpeed)
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepDelay)
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepWidth)
                UNITY_DEFINE_INSTANCED_PROP(float, _SweepAngle)
            UNITY_INSTANCING_BUFFER_END(Props)

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = v.uv; 
                
                return o;
            }

            float4 frag (v2f i) : SV_Target 
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float3 normal = normalize(i.worldNormal);
                
                float3 viewDir = (unity_OrthoParams.w == 1.0) 
                    ? -float3(unity_CameraToWorld._m02, unity_CameraToWorld._m12, unity_CameraToWorld._m22)
                    : normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                float topMask = saturate(normal.y);
                float bottomMask = saturate(-normal.y);

                float4 mainColor = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float transparency = UNITY_ACCESS_INSTANCED_PROP(Props, _Transparency);
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

                float useSweep = UNITY_ACCESS_INSTANCED_PROP(Props, _UseSweep); 
                float sweepStatic = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepStatic);
                float sweepPos = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepPosition);
                float4 sweepColor = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepColor);
                
                // Fetching bounds
                float sweepStart = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepStart);
                float sweepEnd = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepEnd);
                
                float sweepSpeed = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepSpeed);
                float sweepDelay = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepDelay); 
                float sweepWidth = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepWidth);
                float sweepAngle = UNITY_ACCESS_INSTANCED_PROP(Props, _SweepAngle);

                float baseAlpha = mainColor.a * transparency;

                // --- 1. Base Shading ---
                float3 baseRgb = mainColor.rgb * sideDark;
                baseRgb = lerp(baseRgb, mainColor.rgb * bottomDark, bottomMask);
                baseRgb = lerp(baseRgb, mainColor.rgb, topMask);

                // --- 2. Fake Shine ---
                float rad = shineAngle * 0.0174533f;
                float3 fakeLightDir = normalize(float3(sin(rad), 1.0f, cos(rad)));
                float3 halfVector = normalize(fakeLightDir + viewDir);
                float nDotH = saturate(dot(normal, halfVector));
                
                float shineThreshold = 1.0f - shineSize;
                float shineIntensity = smoothstep(shineThreshold, shineThreshold + shineSoftness, nDotH);
                float3 shineContribution = shineColor.rgb * (shineIntensity * shineColor.a * enableShine);

                // --- 3. Fresnel Rim Light ---
                float nDotV = saturate(dot(normal, viewDir));
                float fresnel = pow(1.0f - nDotV, fresnelPower); 
                float3 rimContribution = rimColor.rgb * (fresnel * rimColor.a * enableHL);

                // --- 4. Sweeping Reflection (Sheen) ---
                float sweepRad = sweepAngle * 0.0174533f;
                float rotatedUV = i.uv.x * cos(sweepRad) + i.uv.y * sin(sweepRad);
                
                float sweepActiveDuration = 1.0 / max(sweepSpeed, 0.0001); 
                float sweepTotalDuration = sweepActiveDuration + sweepDelay;
                
                float sweepLocalTime = fmod(_Time.y, sweepTotalDuration);
                float sweepProgress = saturate(sweepLocalTime / sweepActiveDuration);

                // FIXED: We now use lerp to move exactly from the Start Pos to the End Pos
                float animatedPhase = lerp(sweepStart, sweepEnd, sweepProgress); 
                
                float sweepPhase = lerp(animatedPhase, sweepPos, sweepStatic);
                
                float sweepDist = abs(rotatedUV - sweepPhase);
                float sweepIntensityEffect = smoothstep(sweepWidth, 0.0, sweepDist);
                
                float3 sweepContribution = sweepColor.rgb * (sweepIntensityEffect * sweepColor.a * useSweep * topMask); 

                // Combine RGB & Alpha cleanly
                float3 finalRgb = baseRgb + shineContribution + rimContribution + sweepContribution;
                float finalAlpha = saturate(baseAlpha + (fresnel * rimColor.a * enableHL * 0.5));

                return float4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }
}