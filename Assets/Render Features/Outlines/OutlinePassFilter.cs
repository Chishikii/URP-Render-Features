using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Xenon
{
    public class OutlinePassFilter : ScriptableRenderPass
    {
        private class PassData
        {
            internal RendererListHandle RendererListHandle;
            internal UniversalCameraData CameraData;
            internal bool ClearDepth;
        }

        private readonly bool _clearDepth;
        private readonly Material _overrideMaterial;
        private readonly LayerMask _layerMask;
        private readonly RenderingLayerMask _renderingLayerMask;

        private readonly List<ShaderTagId> _shaderTagIds = new()
        {
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("LightweightForward"),
        };

        public OutlinePassFilter(OutlineRenderFeature.Settings settings)
        {
            renderPassEvent = settings.RenderPassEvent;

            _layerMask = settings.LayerMask;
            _renderingLayerMask = settings.RenderingLayerMask;
            _overrideMaterial = settings.OverrideMaterial;
            _clearDepth = settings.ClearDepth;
        }

        private void InitRendererLists(ContextContainer context, ref PassData passData, RenderGraph renderGraph)
        {
            var renderingData = context.Get<UniversalRenderingData>();
            var cameraData = context.Get<UniversalCameraData>();
            var lightData = context.Get<UniversalLightData>();

            var sortingCriteria = passData.CameraData.defaultOpaqueSortFlags;
            var filterSettings = new FilteringSettings(RenderQueueRange.opaque, _layerMask, _renderingLayerMask);
            var drawSettings = RenderingUtils.CreateDrawingSettings(_shaderTagIds, renderingData, cameraData, lightData, sortingCriteria);
            drawSettings.overrideMaterial = _overrideMaterial;
            var param = new RendererListParams(renderingData.cullResults, drawSettings, filterSettings);

            passData.RendererListHandle = renderGraph.CreateRendererList(param);
        }

        private static void ExecutePass(PassData passData, RasterGraphContext context)
        {
            context.cmd.ClearRenderTarget(passData.ClearDepth, true, new Color(0, 0, 0, 0));
            context.cmd.DrawRendererList(passData.RendererListHandle);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var resourceData = frameData.Get<UniversalResourceData>();
            var outlineData = frameData.GetOrCreate<OutlineRenderFeature.OutlineData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>("OutlinePass_Render", out var passData, new ProfilingSampler("OutlinePass_Render"));

            var targetDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
            targetDesc.name = "_OutlineObjects";
            targetDesc.format = GraphicsFormat.R8G8B8A8_SRGB;

            var destinationHandle = renderGraph.CreateTexture(targetDesc);
            outlineData.FilterTextureHandle = destinationHandle;

            passData.CameraData = cameraData;
            passData.ClearDepth = _clearDepth;
            InitRendererLists(frameData, ref passData, renderGraph);

            builder.AllowPassCulling(false);
            builder.UseRendererList(passData.RendererListHandle);
            builder.SetRenderAttachment(destinationHandle, 0);
            builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
            builder.SetRenderFunc<PassData>(ExecutePass);
        }
    }
}