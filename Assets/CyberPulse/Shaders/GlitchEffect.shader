// Full-screen glitch/UV-row displacement shader for URP 17 (Unity 6).
// Used as a blit material in GlitchRendererFeature.
// _BlitTexture and Varyings are provided by Blit.hlsl.
Shader "CyberPulse/GlitchEffect"
{
    Properties
    {
        _GlitchStrength ("Glitch Strength", Range(0, 0.12)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest  Always
        Cull   Off
        Blend  Off

        Pass
        {
            Name "GlitchEffect"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment frag
            #pragma target   3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _GlitchStrength;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv  = input.texcoord;

                // Hash-based per-row random displacement
                float row    = floor(uv.y * 20.0);
                float rand   = frac(sin(row * 127.1 + _Time.y * 50.0) * 43758.5453);
                float offset = rand * _GlitchStrength * step(0.7, rand);

                // Wrap X so displaced UVs don't sample outside [0,1]
                float2 glitchedUV = float2(frac(uv.x + offset), uv.y);

                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, glitchedUV);
            }
            ENDHLSL
        }
    }
}
