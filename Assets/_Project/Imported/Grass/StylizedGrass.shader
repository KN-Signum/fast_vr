Shader "Custom/StylizedGrass"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.15, 0.4, 0.1, 1)
        _TipColor ("Tip", Color) = (0.6, 0.9, 0.3, 1)
        _BaseColorAlt ("Base (Variation)", Color) = (0.18, 0.35, 0.08, 1)
        _TipColorAlt ("Tip (Variation)", Color) = (0.7, 0.85, 0.25, 1)
        _ColorVariation ("Tint Variation Strength", Range(0,1)) = 0.5
        _BrightnessVariation ("Brightness Variation", Range(0,0.5)) = 0.15
        _WindStrength ("Wind Strength", Float) = 0.15
        _WindSpeed ("Wind Speed", Float) = 1.5
        _CullDistance ("Cull Distance (m)", Float) = 20
        _FadeStart ("Fade Start (m)", Float) = 16
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Cull Off

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            StructuredBuffer<float4x4> _Matrices;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TipColor;
                float4 _BaseColorAlt;
                float4 _TipColorAlt;
                float _ColorVariation;
                float _BrightnessVariation;
                float _WindStrength;
                float _WindSpeed;
                float _CullDistance;
                float _FadeStart;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  fogCoord    : TEXCOORD2;
                float2 variation   : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash21(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float4x4 mat = _Matrices[instanceID];

                float3 rootWS = float3(mat._m03, mat._m13, mat._m23);
                float dist = distance(_WorldSpaceCameraPos, rootWS);
                float fade = 1.0 - smoothstep(_FadeStart, _CullDistance, dist);
                float4 posOS = IN.positionOS;
                posOS.y *= fade;

                float3 worldPos = mul(mat, posOS).xyz;

                float2 rootXZ = float2(mat._m03, mat._m23);
                OUT.variation.x = Hash21(rootXZ);
                OUT.variation.y = Hash21(rootXZ + 17.31);

                float wind = sin(_Time.y * _WindSpeed + worldPos.x * 0.5 + worldPos.z * 0.3);
                worldPos.x += wind * _WindStrength * IN.uv.y;
                worldPos.z += wind * 0.5 * _WindStrength * IN.uv.y;

                OUT.positionHCS = TransformWorldToHClip(worldPos);
                OUT.uv = IN.uv;
                OUT.normalWS = float3(0, 1, 0);
                OUT.fogCoord = ComputeFogFactor(OUT.positionHCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half tintHash = IN.variation.x * _ColorVariation;
                half3 baseCol = lerp(_BaseColor.rgb, _BaseColorAlt.rgb, tintHash);
                half3 tipCol  = lerp(_TipColor.rgb,  _TipColorAlt.rgb,  tintHash);
                half3 col = lerp(baseCol, tipCol, IN.uv.y);

                col *= 1.0 + (IN.variation.y * 2.0 - 1.0) * _BrightnessVariation;

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(IN.normalWS, mainLight.direction)) * 0.5 + 0.5;
                col *= mainLight.color * ndotl;
                col = MixFog(col, IN.fogCoord);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
