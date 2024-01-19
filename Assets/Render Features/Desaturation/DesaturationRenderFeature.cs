using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures
{
    [Serializable]
    public class DesaturationSettings
    {
        [Range(0, 1f)]
        public float Saturation;

        /// <summary>
        /// When the render feature should be injected.
        /// </summary>
        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        /// <summary>
        /// The render texture format to use for RTHandles.
        /// </summary>
        public RenderTextureFormat RenderTextureFormat = RenderTextureFormat.Default;

        /// <summary>
        /// The LayerMask of the objects to include in the desaturation.
        /// </summary>
        public LayerMask LayerMask = 0;

        /// <summary>
        /// The render layer mask of the objects to include in the desaturation.
        /// </summary>
        public int RenderLayerMask;

        /// <summary>
        /// The override shader to use when rendering objects into a buffer.
        /// </summary>
        public Shader OverrideShader;

        /// <summary>
        /// The override shader to use for the fullscreen blit.
        /// </summary>
        public Shader FullscreenShader;
    }

    public class DesaturationRenderFeature : ScriptableRendererFeature
    {
        public DesaturationSettings Settings;

        private DesaturationRenderPass m_DesaturationRenderPass;

        /// <summary>
        /// Initializes this feature's resources. This is called every time serialization happens.
        /// </summary>
        public override void Create()
        {
            // We can only proceed if we have a valid fullscreen pass, the override shader is optional.
            if (Settings.FullscreenShader == null) return;

            m_DesaturationRenderPass = new DesaturationRenderPass(Settings)
            {
                renderPassEvent = Settings.RenderPassEvent,
            };
        }

        /// <summary>
        /// Injects one or multiple <c>ScriptableRenderPass</c> in the renderer.
        /// </summary>
        /// <param name="renderer">Renderer used for adding render passes.</param>
        /// <param name="renderingData">Rendering state. Use this to setup render passes.</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Remove the feature from preview rendering.
            if (renderingData.cameraData.cameraType <= CameraType.SceneView)
                renderer.EnqueuePass(m_DesaturationRenderPass);
        }

        /// <summary>
        /// Clean up any resources used by the render feature and pass.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            // Make sure we dispose all used resources from the pass.
            m_DesaturationRenderPass.Dispose();
        }
    }
}