using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RenderFeatures.Blur
{
    [Serializable]
    public class BlurSettings
    {
        [Range(0, 0.4f)]
        public float HorizontalBlur;

        [Range(0, 0.4f)]
        public float VerticalBlur;
    }

    public class BlurRendererFeature : ScriptableRendererFeature
    {
        [SerializeField]
        private BlurSettings BlurSettings;

        [SerializeField]
        private Shader Shader;

        private Material m_Material;

        private BlurRenderPass m_BlurRenderPass;

        public override void Create()
        {
            if (Shader == null)
                return;

            m_Material = new Material(Shader);
            m_BlurRenderPass = new BlurRenderPass(m_Material, BlurSettings)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingSkybox
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(m_BlurRenderPass);
        }

        protected override void Dispose(bool disposing)
        {
            m_BlurRenderPass.Dispose();

            if (m_Material == null)
                return;

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