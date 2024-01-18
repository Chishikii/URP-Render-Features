using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures.Blur
{
    public class BlurRenderPass : ScriptableRenderPass
    {
        private readonly BlurSettings m_DefaultSettings;
        private readonly Material m_Material;
        private RenderTextureDescriptor m_BlurTextureDescriptor;

        private RTHandle m_BlurTextureHandle;

        private static readonly int HorizontalBlurId = Shader.PropertyToID("_HorizontalBlur");
        private static readonly int VerticalBlurId = Shader.PropertyToID("_VerticalBlur");

        public BlurRenderPass(Material material, BlurSettings defaultSettings)
        {
            m_DefaultSettings = defaultSettings;
            m_Material = material;

            m_BlurTextureDescriptor =
                new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.Default, 0);
        }

        private void UpdateBlurSettings()
        {
            if (m_Material == null) return;

            m_Material.SetFloat(HorizontalBlurId, m_DefaultSettings.HorizontalBlur);
            m_Material.SetFloat(VerticalBlurId, m_DefaultSettings.VerticalBlur);
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // Set the blur texture size to the same as the camera target size.
            m_BlurTextureDescriptor.width = cameraTextureDescriptor.width;
            m_BlurTextureDescriptor.height = cameraTextureDescriptor.height;

            // Check if the descriptor has changed, and reallocate the RTHandle if necessary.
            RenderingUtils.ReAllocateIfNeeded(ref m_BlurTextureHandle, m_BlurTextureDescriptor, name: "_BlurTexture");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // Get a command buffer from the pool.
            var cmd = CommandBufferPool.Get();
            var cameraTargetHandle = renderingData.cameraData.renderer.cameraColorTargetHandle;

            UpdateBlurSettings();

            using (new ProfilingScope(cmd, new ProfilingSampler("BlurRenderPass")))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                // Blit from the camera target to the temporary render texture using the first pass.
                Blit(cmd, cameraTargetHandle, m_BlurTextureHandle, m_Material);
                // Blit from the temporary render texture to the camera target using the second pass.
                Blit(cmd, m_BlurTextureHandle, cameraTargetHandle, m_Material, 1);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
                Object.Destroy(m_Material);
            else
                Object.DestroyImmediate(m_Material);
#else
            Object.Destroy(m_Material);
#endif

            m_BlurTextureHandle?.Release();
        }
    }
}