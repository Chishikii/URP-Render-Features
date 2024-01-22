using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

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

    [Serializable]
    public class ViewSpaceNormalTextureSettings
    {
        /// <summary>
        /// Format of the render texture. In order to enable HDR dependent post processing use at least ARGBHalf.
        /// </summary>
        public RenderTextureFormat RenderTextureFormat = RenderTextureFormat.Default;

        /// <summary>
        /// The amount of bits restored for the depth buffer.
        /// </summary>
        public DepthBits DepthBufferBits = DepthBits.None;

        /// <summary>
        /// The filter mode of the texture.
        /// </summary>
        public FilterMode FilterMode = FilterMode.Point;

        /// <summary>
        /// Color used to fill empty areas.
        /// </summary>
        public Color BackgroundColor = Color.black;

        /// <summary>
        /// Whether the objects should ignore scene objects when rendering or not.
        /// </summary>
        public bool IgnoreSceneObjects = true;
    }

    public class OutlineRenderFeature : ScriptableRendererFeature
    {
        /// <summary>
        /// The settings used for the outline renderer feature.
        /// </summary>
        [FormerlySerializedAs("Settings")]
        public OutlineSettings OutlineSettings = new();

        public ViewSpaceNormalTextureSettings TextureSettings = new();

        private OutlineRenderPass m_OutlineRenderPass;

        /// <summary>
        /// Initializes this feature's resources. This is called every time serialization happens.
        /// </summary>
        public override void Create()
        {
            m_OutlineRenderPass = new OutlineRenderPass(OutlineSettings, TextureSettings);
        }

        /// <summary>
        /// Injects one or multiple <c>ScriptableRenderPass</c> in the renderer.
        /// </summary>
        /// <param name="renderer">Renderer used for adding render passes.</param>
        /// <param name="renderingData">Rendering state. Use this to setup render passes.</param>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_OutlineRenderPass);
        }

        /// <summary>
        /// Clean up any resources used by the render feature and pass.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            // Make sure we also clean up the passes resources.
            m_OutlineRenderPass.Dispose();
        }
    }
}