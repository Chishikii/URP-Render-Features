using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures
{
    public class DesaturationRenderPass : ScriptableRenderPass
    {
        private readonly DesaturationSettings m_Settings;
        private readonly Material m_FullscreenMaterial;
        private readonly Material m_OverrideMaterial;

        private readonly FilteringSettings m_FilteringSettings;
        private readonly List<ShaderTagId> m_ShaderTagIds = new();

        private RendererList m_RendererList;

        /// <summary>
        /// Used as a render target for drawing objects.
        /// </summary>
        private RTHandle m_FilterTextureHandle;

        /// <summary>
        /// Used for the fullscreen blit.
        /// </summary>
        private RTHandle m_TemporaryColorTextureHandle;

        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");

        public DesaturationRenderPass(DesaturationSettings settings)
        {
            m_Settings = settings;
            m_FullscreenMaterial = new Material(settings.FullscreenShader);

            if (settings.OverrideShader != null)
                m_OverrideMaterial = new Material(settings.OverrideShader);

            // Make sure we use layers in our filtering settings.
            var renderLayer = (uint)1 << settings.RenderLayerMask;
            m_FilteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.LayerMask, renderLayer);

            // Use default shader tags.
            m_ShaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            m_ShaderTagIds.Add(new ShaderTagId("UniversalForward"));
            m_ShaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        private void UpdateSettings()
        {
            if (m_FullscreenMaterial == null) return;

            m_FullscreenMaterial.SetFloat(SaturationId, m_Settings.Saturation);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            cameraTextureDescriptor.colorFormat = m_Settings.RenderTextureFormat;
            cameraTextureDescriptor.depthBufferBits = (int)DepthBits.None;

            RenderingUtils.ReAllocateIfNeeded(ref m_FilterTextureHandle, cameraTextureDescriptor,
                name: "_FilterTexture");

            RenderingUtils.ReAllocateIfNeeded(ref m_TemporaryColorTextureHandle, cameraTextureDescriptor,
                name: "_TemporaryColor");

            var cameraDepthTextureHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;

            ConfigureTarget(m_FilterTextureHandle, cameraDepthTextureHandle);
            ConfigureClear(ClearFlag.Color, new Color(0, 0, 0, 0));
        }

        private void InitRendererLists(ref RenderingData renderingData, ScriptableRenderContext context)
        {
            var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;

            var drawingSettings = CreateDrawingSettings(m_ShaderTagIds, ref renderingData, sortingCriteria);
            drawingSettings.overrideMaterial = m_OverrideMaterial;
            drawingSettings.overrideMaterialPassIndex = 0;

            var param = new RendererListParams(renderingData.cullResults, drawingSettings, m_FilteringSettings);
            m_RendererList = context.CreateRendererList(ref param);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Make sure we have a valid material
            if (m_FullscreenMaterial == null)
                return;

            var cmd = CommandBufferPool.Get();
            var cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            UpdateSettings();

            using (new ProfilingScope(cmd, new ProfilingSampler("DesaturationPass")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Initialize and draw all renderers.
                InitRendererLists(ref renderingData, context);
                cmd.DrawRendererList(m_RendererList);

                // Pass our filter texture to shaders as a global texture reference.
                // Obtain this in a shader graph as a Texture2D with exposed un-ticked
                // and reference _FilterTexture.
                cmd.SetGlobalTexture(Shader.PropertyToID(m_FilterTextureHandle.name),
                    m_FilterTextureHandle);

                // For some reasons these rt are null for a frame when selecting in scene view.
                if (cameraTargetHandle.rt != null && m_TemporaryColorTextureHandle.rt != null)
                {
                    Blitter.BlitCameraTexture(cmd, cameraTargetHandle, m_TemporaryColorTextureHandle,
                        m_FullscreenMaterial,
                        0);
                    Blitter.BlitCameraTexture(cmd, m_TemporaryColorTextureHandle, cameraTargetHandle);
                }
            }

            // Execute and release command buffer
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
                Object.Destroy(m_FullscreenMaterial);
                Object.Destroy(m_OverrideMaterial);
            }
            else
            {
                Object.DestroyImmediate(m_FullscreenMaterial);
                Object.DestroyImmediate(m_OverrideMaterial);
            }
#else
            Object.Destroy(m_FullscreenMaterial);
            Object.Destroy(m_OverrideMaterial);
#endif

            m_FilterTextureHandle?.Release();
            m_TemporaryColorTextureHandle?.Release();
        }
    }
}