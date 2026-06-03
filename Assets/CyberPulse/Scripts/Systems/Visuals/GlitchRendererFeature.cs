using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace CyberPulse.Systems
{
    /// <summary>
    /// URP ScriptableRendererFeature that blits the camera through GlitchEffect.shader.
    /// Add this to the active UniversalRendererData asset (or let PlayableLevelBuilder do it).
    /// GlitchController drives the _GlitchStrength property on the material each frame.
    /// </summary>
    public class GlitchRendererFeature : ScriptableRendererFeature
    {
        public Material material;

        private GlitchPass _pass;

        private static readonly int GlitchStrengthID = Shader.PropertyToID("_GlitchStrength");

        public void SetStrength(float strength)
        {
            if (material != null)
                material.SetFloat(GlitchStrengthID, strength);
        }

        public override void Create()
        {
            _pass = new GlitchPass
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null) return;
            if (material.GetFloat(GlitchStrengthID) < 0.001f) return;
            _pass.SetMaterial(material);
            renderer.EnqueuePass(_pass);
        }

        private sealed class GlitchPass : ScriptableRenderPass
        {
            private Material _mat;

            public GlitchPass()
            {
                requiresIntermediateTexture = true;
            }

            public void SetMaterial(Material m) => _mat = m;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material      material;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_mat == null) return;

                var resourceData = frameData.Get<UniversalResourceData>();

                if (resourceData.isActiveTargetBackBuffer) return;

                var src  = resourceData.activeColorTexture;

                var desc     = renderGraph.GetTextureDesc(src);
                desc.name    = "_GlitchTemp";
                desc.clearBuffer = false;
                var dst      = renderGraph.CreateTexture(desc);

                using var builder = renderGraph.AddRasterRenderPass<PassData>("CyberPulse_Glitch", out var pd);
                pd.source   = src;
                pd.material = _mat;

                builder.UseTexture(src);
                builder.SetRenderAttachment(dst, 0);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0));

                resourceData.cameraColor = dst;
            }
        }
    }
}
