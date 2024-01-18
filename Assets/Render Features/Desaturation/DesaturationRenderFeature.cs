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

        public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingOpaques;

        /// <summary>
        /// The LayerMask of the objects to include in the desaturation.
        /// </summary>
        public LayerMask LayerMask = 0;

        /// <summary>
        /// The render layer mask of the objects to include in the desaturation.
        /// </summary>
        public int RenderLayerMask = 0;

        /// <summary>
        /// The override material to use. 
        /// </summary>
        public Material RenderOverrideMaterial = null;

        /// <summary>
        /// The override shader to use for the fullscreen blit.
        /// </summary>
        public Shader FullscreenShader = null;
    }

    public class DesaturationRenderFeature : ScriptableRendererFeature
    {
        public DesaturationSettings Settings;

        private Material m_Material;

        private DesaturationRenderPass m_DesaturationRenderPass;

        public override void Create()
        {
            if (Settings.FullscreenShader == null) return;
            m_Material = new Material(Settings.FullscreenShader);

            m_DesaturationRenderPass = new DesaturationRenderPass(m_Material, Settings)
            {
                renderPassEvent = Settings.RenderPassEvent,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Only add the pass to the game camera.
            renderer.EnqueuePass(m_DesaturationRenderPass);
        }

        protected override void Dispose(bool disposing)
        {
            // Make sure we dispose all used resources.
            m_DesaturationRenderPass.Dispose();

#if UNITY_EDITOR
            if (Application.isPlaying)
                Destroy(m_Material);
            else
                DestroyImmediate(m_Material);
#else
            Destroy(m_Material);
#endif
        }
    }
}