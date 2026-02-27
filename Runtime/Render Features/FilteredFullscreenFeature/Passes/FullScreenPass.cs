using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Chishikii.RenderFeatures
{
    public class FullScreenPass : ScriptableRenderPass
    {
        private class PassData
        {
            internal TextureHandle FilterTextureHandle;
            internal Material Material;
        }

        private static readonly int FilterTexture = Shader.PropertyToID("_FilterTexture");

        private readonly Material _blitMaterial;

        private readonly string _name;

        public FullScreenPass(FilteredFullScreenRenderFeature.Settings settings)
        {
            renderPassEvent = settings.RenderPassEvent;
            _name = settings.Name;
            _blitMaterial = settings.FullScreenMaterial;
            profilingSampler = new ProfilingSampler($"{_name}_Filter");
        }

        private static void ExecutePass(PassData passData, RasterGraphContext context)
        {
            if (passData.Material != null)
            {
                passData.Material.SetTexture(FilterTexture, passData.FilterTextureHandle);
            }

            Blitter.BlitTexture(context.cmd, passData.FilterTextureHandle, new Vector4(1, 1, 0, 0), passData.Material, 0);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            var outlineData = frameData.Get<FilteredFullScreenRenderFeature.FilterData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>($"{_name}_Filter", out var passData, profilingSampler);

            if (!outlineData.FilterTextureHandle.IsValid())
                return;

            if (_blitMaterial == null)
                return;

            passData.Material = _blitMaterial;
            passData.FilterTextureHandle = outlineData.FilterTextureHandle;

            builder.AllowPassCulling(false);
            builder.UseTexture(passData.FilterTextureHandle);
            builder.SetRenderAttachment(resourceData.cameraColor, index: 0);
            builder.SetRenderFunc<PassData>(ExecutePass);
        }
    }
}