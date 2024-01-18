using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures.Outlines
{
    [Serializable]
    public class OutlineSettings
    {
        /// <summary>
        /// When to render the outlines.
        /// </summary>
        public RenderPassEvent RenderPassEvent = RenderPassEvent.BeforeRenderingTransparents;

        /// <summary>
        /// The render texture format to use for RTHandles.
        /// </summary>
        public RenderTextureFormat RenderTextureFormat = RenderTextureFormat.Default;

        /// <summary>
        /// The layer mask of the objects to include in the outlines.
        /// </summary>
        public LayerMask LayerMask = 0;

        /// <summary>
        /// The render layer mask of the objects to include in the outlines.
        /// </summary>
        public int RenderLayerMask = 1;

        public float OutlineScale = 1;
        public Color OutlineColor = new(0, 22, 255);

        public float RobertsCrossMultiplier = 500;

        public float DepthThreshold = 10;
        public float NormalThreshold = 0.6f;

        public float SteepAngleThreshold = 0;
        public float SteepAngleMultiplier = 100;
    }

    public class OutlineRenderFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// The settings used for the outline renderer feature.
        /// </summary>
        public OutlineSettings Settings = new();

        private OutlineRenderPass m_OutlineRenderPass;

        public override void Create()
        {
            m_OutlineRenderPass = new OutlineRenderPass(Settings)
            {
                renderPassEvent = Settings.RenderPassEvent,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_OutlineRenderPass);
        }
    }
}