using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ProfilingScope = UnityEngine.Rendering.ProfilingScope;

namespace RenderFeatures.Outlines
{
    public class OutlineRenderPass : ScriptableRenderPass
    {
        private readonly OutlineSettings m_Settings;
        private readonly Material m_NormalsMaterial;
        private readonly Material m_OutlineMaterial;

        private readonly FilteringSettings m_FilteringSettings;
        private readonly List<ShaderTagId> m_ShaderTagIds = new List<ShaderTagId>();

        private RendererList m_RendererList;

        /// <summary>
        /// Used as a render target for drawing objects into.
        /// </summary>
        private RTHandle m_NormalsTextureHandle;

        private RTHandle m_TempColorTextureHandle;

        private static readonly int OutlineScaleId = Shader.PropertyToID("_OutlineScale");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int RobertsCrossMultiplierId = Shader.PropertyToID("_RobertsCrossMultiplier");
        private static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        private static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
        private static readonly int SteepAngleThresholdId = Shader.PropertyToID("_SteepAngleThreshold");
        private static readonly int SteepAngleMultiplierId = Shader.PropertyToID("_SteepAngleMultiplier");

        public OutlineRenderPass(OutlineSettings settings)
        {
            m_Settings = settings;
            m_NormalsMaterial = new Material(Shader.Find("Hidden/ViewSpaceNormals"));
            m_OutlineMaterial = new Material(Shader.Find("Hidden/Outlines"));

            var renderLayer = (uint)1 << settings.RenderLayerMask;
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.LayerMask, renderLayer);

            m_ShaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            m_ShaderTagIds.Add(new ShaderTagId("UniversalForward"));
            m_ShaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        private void UpdateSettings()
        {
            if (m_OutlineMaterial == null)
                return;

            m_OutlineMaterial.SetFloat(OutlineScaleId, m_Settings.OutlineScale);
            m_OutlineMaterial.SetColor(OutlineColorId, m_Settings.OutlineColor);
            m_OutlineMaterial.SetFloat(RobertsCrossMultiplierId, m_Settings.RobertsCrossMultiplier);
            m_OutlineMaterial.SetFloat(DepthThresholdId, m_Settings.DepthThreshold);
            m_OutlineMaterial.SetFloat(NormalThresholdId, m_Settings.NormalThreshold);
            m_OutlineMaterial.SetFloat(SteepAngleThresholdId, m_Settings.SteepAngleThreshold);
            m_OutlineMaterial.SetFloat(SteepAngleMultiplierId, m_Settings.SteepAngleMultiplier);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            cameraTextureDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            cameraTextureDescriptor.depthBufferBits = (int)DepthBits.None;

            RenderingUtils.ReAllocateIfNeeded(ref m_NormalsTextureHandle, cameraTextureDescriptor,
                name: "_NormalsTexture");

            RenderingUtils.ReAllocateIfNeeded(ref m_TempColorTextureHandle, cameraTextureDescriptor,
                name: "_TempColorTexture");

            var cameraDepthTextureHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;

            ConfigureTarget(m_NormalsTextureHandle, cameraDepthTextureHandle);
            ConfigureClear(ClearFlag.Color, new Color(0, 0, 0, 0));
        }

        private void InitRendererLists(ref RenderingData renderingData, ScriptableRenderContext context)
        {
            var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;

            var drawingSettings = CreateDrawingSettings(m_ShaderTagIds, ref renderingData, sortingCriteria);
            drawingSettings.overrideMaterial = m_NormalsMaterial;
            drawingSettings.overrideMaterialPassIndex = 0;

            var param = new RendererListParams(renderingData.cullResults, drawingSettings, m_FilteringSettings);
            m_RendererList = context.CreateRendererList(ref param);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_NormalsMaterial == null || m_OutlineMaterial == null)
                return;

            var cmd = CommandBufferPool.Get();
            var cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            UpdateSettings();

            using (new ProfilingScope(cmd, new ProfilingSampler("OutlinePass")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                InitRendererLists(ref renderingData, context);
                cmd.DrawRendererList(m_RendererList);

                // Expose the normals texture to shaders.
                cmd.SetGlobalTexture(Shader.PropertyToID(m_NormalsTextureHandle.name), m_NormalsTextureHandle);

                if (cameraTargetHandle.rt != null && m_TempColorTextureHandle.rt != null)
                {
                    Blitter.BlitCameraTexture(cmd, cameraTargetHandle, m_TempColorTextureHandle, m_OutlineMaterial, 0);
                    Blitter.BlitCameraTexture(cmd, m_TempColorTextureHandle, cameraTargetHandle);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
                Object.Destroy(m_NormalsMaterial);
            else
                Object.DestroyImmediate(m_NormalsMaterial);
#else
            Object.Destroy(m_NormalsMaterial);
#endif

            m_NormalsTextureHandle?.Release();
        }
    }
}