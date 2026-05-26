Shader "CyberPulse/GridFloor"
{
    Properties
    {
        _GridScale        ("Grid Scale (cells per world unit)", Float)        = 1.0
        _LineWidth        ("Line Width",  Range(0.005, 0.15))                 = 0.04
        _BaseColor        ("Base Color",  Color)                              = (0.04, 0.04, 0.08, 1)
        _GridColor        ("Grid Color",  Color)                              = (0.0, 0.96, 1.0, 1)
        _GlowColor        ("Glow Color (HDR)", Color)                         = (0.3, 1.2, 1.2, 1)
        _EmissiveIntensity("Emissive Intensity", Float)                       = 2.5
        _GlowRadius       ("Glow Radius (world units)", Float)                = 10.0
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
            Name "GridFloor_Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // ── Per-material constants ─────────────────────────────────────────
            CBUFFER_START(UnityPerMaterial)
                float  _GridScale;
                float  _LineWidth;
                float4 _BaseColor;
                float4 _GridColor;
                float4 _GlowColor;
                float  _EmissiveIntensity;
                float  _GlowRadius;
            CBUFFER_END

            // Set every frame from GridFloorUpdater.cs via Shader.SetGlobalVector
            float4 _CyberPlayerPosition;

            // ── Vertex I/O ─────────────────────────────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── Vertex shader ──────────────────────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.worldPos    = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            // ── Fragment shader ────────────────────────────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                // Grid from world XZ so tiling is world-space independent.
                float2 cell  = IN.worldPos.xz * _GridScale;
                float2 fract = abs(frac(cell) * 2.0 - 1.0); // remap 0..1 → fold to 0..1..0
                // Use screen-space derivative for anti-aliased lines
                float2 deriv = fwidth(cell);
                float2 grid2 = smoothstep(_LineWidth - deriv, _LineWidth, 1.0 - fract);
                float  lines = 1.0 - min(grid2.x, grid2.y);

                // Player proximity glow
                float  dist      = distance(_CyberPlayerPosition.xz, IN.worldPos.xz);
                float  proximity = 1.0 - saturate(dist / _GlowRadius);
                // Squared falloff for a tighter, more dramatic glow
                proximity = proximity * proximity;

                // Blend grid line color from neutral to hot glow based on proximity
                half3 lineColor = lerp(_GridColor.rgb, _GlowColor.rgb, proximity);

                // Mix base surface with grid lines
                half3 col = lerp(_BaseColor.rgb, lineColor, lines);

                // HDR emissive boost on the lines — interaction with Bloom
                float emissiveMult = 1.0 + lines * (_EmissiveIntensity - 1.0) * (1.0 + proximity * 3.0);
                col *= emissiveMult;

                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass so the floor receives shadows properly.
        Pass
        {
            Name "GridFloor_ShadowCaster"
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
