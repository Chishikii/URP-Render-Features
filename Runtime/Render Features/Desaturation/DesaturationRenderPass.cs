using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures
{
    public class DesaturationRenderPass : ScriptableRenderPass
    {
        private readonly DesaturationSettings _settings;

        private readonly Material _fullscreenMaterial;

        private readonly Material _overrideMaterial;

        private readonly FilteringSettings _filteringSettings;

        private readonly List<ShaderTagId> _shaderTagIds = new();

        private RendererList _rendererList;

        /// <summary>
        /// Used as a render target for drawing objects.
        /// </summary>
        private RTHandle _filterTextureHandle;

        /// <summary>
        /// Used for the fullscreen blit.
        /// </summary>
        private RTHandle _temporaryColorTextureHandle;

        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");

        public DesaturationRenderPass(DesaturationSettings settings)
        {
            _settings = settings;
            _fullscreenMaterial = new Material(settings.FullscreenShader);

            if (settings.OverrideShader != null)
                _overrideMaterial = new Material(settings.OverrideShader);

            // Make sure we use layers in our filtering settings.
            var renderLayer = (uint)1 << settings.RenderLayerMask;
            _filteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.LayerMask, renderLayer);

            // Use default shader tags.
            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
        }

        private void UpdateSettings()
        {
            if (_fullscreenMaterial == null) return;

            _fullscreenMaterial.SetFloat(SaturationId, _settings.Saturation);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var cameraTextureDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            cameraTextureDescriptor.colorFormat = _settings.RenderTextureFormat;
            cameraTextureDescriptor.depthBufferBits = (int)DepthBits.None;

            RenderingUtils.ReAllocateIfNeeded(ref _filterTextureHandle, cameraTextureDescriptor,
                name: "_FilterTexture");

            RenderingUtils.ReAllocateIfNeeded(ref _temporaryColorTextureHandle, cameraTextureDescriptor,
                name: "_TemporaryColor");

            var cameraDepthTextureHandle = renderingData.cameraData.renderer.cameraDepthTargetHandle;

            ConfigureTarget(_filterTextureHandle, cameraDepthTextureHandle);
            ConfigureClear(ClearFlag.Color, new Color(0, 0, 0, 0));
        }

        private void InitRendererLists(ref RenderingData renderingData, ScriptableRenderContext context)
        {
            var sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;

            var drawingSettings = CreateDrawingSettings(_shaderTagIds, ref renderingData, sortingCriteria);
            drawingSettings.overrideMaterial = _overrideMaterial;
            drawingSettings.overrideMaterialPassIndex = 0;

            var param = new RendererListParams(renderingData.cullResults, drawingSettings, _filteringSettings);
            _rendererList = context.CreateRendererList(ref param);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Make sure we have a valid material
            if (_fullscreenMaterial == null)
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
                cmd.DrawRendererList(_rendererList);

                // Pass our filter texture to shaders as a global texture reference.
                // Obtain this in a shader graph as a Texture2D with exposed un-ticked
                // and reference _FilterTexture.
                cmd.SetGlobalTexture(Shader.PropertyToID(_filterTextureHandle.name),
                    _filterTextureHandle);

                // For some reasons these rt are null for a frame when selecting in scene view.
                if (cameraTargetHandle.rt != null && _temporaryColorTextureHandle.rt != null)
                {
                    Blitter.BlitCameraTexture(cmd, cameraTargetHandle, _temporaryColorTextureHandle,
                        _fullscreenMaterial,
                        0);
                    Blitter.BlitCameraTexture(cmd, _temporaryColorTextureHandle, cameraTargetHandle);
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
                Object.Destroy(_fullscreenMaterial);
                Object.Destroy(_overrideMaterial);
            }
            else
            {
                Object.DestroyImmediate(_fullscreenMaterial);
                Object.DestroyImmediate(_overrideMaterial);
            }
#else
            Object.Destroy(m_FullscreenMaterial);
            Object.Destroy(m_OverrideMaterial);
#endif

            _filterTextureHandle?.Release();
            _temporaryColorTextureHandle?.Release();
        }
    }
}