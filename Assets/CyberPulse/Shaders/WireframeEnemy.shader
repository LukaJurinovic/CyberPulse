Shader "CyberPulse/WireframeEnemy"
{
    Properties
    {
        _EdgeColor    ("Edge Color (HDR)", Color)       = (0.8, 0.2, 0.05, 1)
        _FillColor    ("Fill Color",       Color)       = (0.12, 0.02, 0.01, 1)
        _FresnelPower ("Fresnel Power",    Range(1, 8)) = 3.0
        _EdgeWidth    ("Edge Blend Width", Range(0.1, 1.0)) = 0.5
        _PulseSpeed   ("Pulse Speed",      Float)       = 1.5
        _PulseAmount  ("Pulse Amount",     Range(0, 1)) = 0.3
        _EmissiveScale("Emissive Scale",   Float)       = 3.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 100

        Pass
        {
            Name "WireframeEnemy_Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _EdgeColor;
                float4 _FillColor;
                float  _FresnelPower;
                float  _EdgeWidth;
                float  _PulseSpeed;
                float  _PulseAmount;
                float  _EmissiveScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS    : TEXCOORD0;
                float3 viewDirWS   : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs  = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   normInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInputs.positionCS;
                OUT.normalWS    = normInputs.normalWS;
                OUT.viewDirWS   = GetWorldSpaceViewDir(posInputs.positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);

                // Fresnel — 0 at face-on, 1 at silhouette edges
                float fresnel = 1.0 - saturate(dot(N, V));
                fresnel = pow(fresnel, _FresnelPower);

                // Edge mask — sharpen and smooth the transition
                float edge = smoothstep(1.0 - _EdgeWidth, 1.0, fresnel);

                // Animated pulse so the enemy breathes
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed);

                // Combine: fill is dim, edges are HDR bright for Bloom
                half3 col = lerp(_FillColor.rgb, _EdgeColor.rgb * _EmissiveScale * pulse, edge);

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "WireframeEnemy_ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
