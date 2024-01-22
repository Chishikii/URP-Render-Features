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
        private readonly OutlineSettings m_OutlineSettings;
        private readonly ViewSpaceNormalTextureSettings m_TextureSettings;

        private readonly Material m_NormalsMaterial;
        private readonly Material m_OutlineMaterial;

        private readonly FilteringSettings m_FilteringSettings;
        private readonly List<ShaderTagId> m_ShaderTagIds = new();

        private RendererList m_RendererList;

        /// <summary>
        /// Used as a render target for drawing objects into.
        /// </summary>
        private RTHandle m_NormalsTextureHandle;

        /// <summary>
        /// Used as a temporary texture when blitting fullscreen.
        /// </summary>
        private RTHandle m_TempColorTextureHandle;

        // Ids of shader properties to use when updating.
        private static readonly int OutlineScaleId = Shader.PropertyToID("_OutlineScale");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int RobertsCrossMultiplierId = Shader.PropertyToID("_RobertsCrossMultiplier");
        private static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        private static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
        private static readonly int SteepAngleThresholdId = Shader.PropertyToID("_SteepAngleThreshold");
        private static readonly int SteepAngleMultiplierId = Shader.PropertyToID("_SteepAngleMultiplier");

        public OutlineRenderPass(OutlineSettings outlineSettings, ViewSpaceNormalTextureSettings textureSettings)
        {
            m_OutlineSettings = outlineSettings;
            m_TextureSettings = textureSettings;
            m_NormalsMaterial = new Material(Shader.Find("Hidden/ViewSpaceNormals"));
            m_OutlineMaterial = new Material(Shader.Find("Hidden/Outlines"));

            // Set the stage of when this feature will get rendered.
            renderPassEvent = outlineSettings.RenderPassEvent;

            // Make sure we use our layers in the filtering settings.
            var renderLayer = (uint)1 << outlineSettings.RenderLayerMask;
            m_FilteringSettings =
                new FilteringSettings(RenderQueueRange.opaque, outlineSettings.LayerMask, renderLayer);

            // Use default shader tags.
            m_ShaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            m_ShaderTagIds.Add(new ShaderTagId("UniversalForward"));
            m_ShaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        /// <summary>
        /// Updates shader properties based on settings.
        /// </summary>
        private void UpdateSettings()
        {
            if (m_OutlineMaterial == null)
                return;

            m_OutlineMaterial.SetFloat(OutlineScaleId, m_OutlineSettings.OutlineScale);
            m_OutlineMaterial.SetColor(OutlineColorId, m_OutlineSettings.OutlineColor);
            m_OutlineMaterial.SetFloat(RobertsCrossMultiplierId, m_OutlineSettings.RobertsCrossMultiplier);
            m_OutlineMaterial.SetFloat(DepthThresholdId, m_OutlineSettings.DepthThreshold);
            m_OutlineMaterial.SetFloat(NormalThresholdId, m_OutlineSettings.NormalThreshold);
            m_OutlineMaterial.SetFloat(SteepAngleThresholdId, m_OutlineSettings.SteepAngleThreshold);
            m_OutlineMaterial.SetFloat(SteepAngleMultiplierId, m_OutlineSettings.SteepAngleMultiplier);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // Set up texture descriptors
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.colorFormat = m_TextureSettings.RenderTextureFormat;
            descriptor.depthBufferBits = (int)m_TextureSettings.DepthBufferBits;

            // Reallocate render textures if needed
            RenderingUtils.ReAllocateIfNeeded(ref m_NormalsTextureHandle, descriptor, name: "_NormalsTexture");
            RenderingUtils.ReAllocateIfNeeded(ref m_TempColorTextureHandle, descriptor, name: "_TempColorTexture");

            var cameraDepthTextureHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;

            if (m_TextureSettings.IgnoreSceneObjects)
                ConfigureTarget(m_NormalsTextureHandle);
            else
                ConfigureTarget(m_NormalsTextureHandle, cameraDepthTextureHandle);

            // Make sure the color is transparent.
            m_TextureSettings.BackgroundColor.a = 0;
            ConfigureClear(ClearFlag.Color, m_TextureSettings.BackgroundColor);
        }

        private void InitRendererLists(ref RenderingData renderingData, ScriptableRenderContext context)
        {
            const SortingCriteria sortingCriteria = SortingCriteria.BackToFront;

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

                // Initialize and draw all renderers.
                InitRendererLists(ref renderingData, context);
                cmd.DrawRendererList(m_RendererList);

                // Pass our filter texture to shaders as a global texture reference.
                // Obtain this in a shader graph as a Texture2D with exposed un-ticked
                // and reference _NormalsTexture.
                cmd.SetGlobalTexture(Shader.PropertyToID(m_NormalsTextureHandle.name), m_NormalsTextureHandle);

                // For some reasons these rt are null for a frame when selecting in scene view so we need to check for null.
                if (cameraTargetHandle.rt != null && m_TempColorTextureHandle.rt != null)
                {
                    Blitter.BlitCameraTexture(cmd, cameraTargetHandle, m_TempColorTextureHandle, m_OutlineMaterial, 0);
                    Blitter.BlitCameraTexture(cmd, m_TempColorTextureHandle, cameraTargetHandle);
                }
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <summary>
        /// Releases all used resources. Called by the feature.
        /// </summary>
        public void Dispose()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                Object.Destroy(m_NormalsMaterial);
                Object.Destroy(m_OutlineMaterial);
            }
            else
            {
                Object.DestroyImmediate(m_NormalsMaterial);
                Object.DestroyImmediate(m_NormalsMaterial);
            }
#else
            Object.Destroy(m_NormalsMaterial);
            Object.Destroy(m_OutlineMaterial);
#endif

            m_NormalsTextureHandle?.Release();
            m_TempColorTextureHandle?.Release();
        }
    }
}