using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Chishikii.RenderFeatures
{
    public class OutlineRenderFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

            public LayerMask LayerMask = 0;

            public RenderingLayerMask RenderingLayerMask = 0;

            public bool ClearDepth;

            public Shader OverrideShader;

            public Shader BlitShader;
        }

        [Serializable]
        public class OutlineSettings
        {
            public float OutlineScale = 1f;
            public float RobertsCrossMultiplier = 100;
            public float DepthThreshold = 10f;
            public float NormalThreshold = 0.4f;
            public float SteepAngleThreshold = 0.2f;
            public float SteepAngleMultiplier = 25f;
            public Color OutlineColor = Color.white;
        }

        public class OutlineData : ContextItem
        {
            public TextureHandle FilterTextureHandle;

            public override void Reset()
            {
                FilterTextureHandle = TextureHandle.nullHandle;
            }
        }

        public Settings FeatureSettings;
        public OutlineSettings MaterialSettings;

        private OutlinePassFilter _outlinePassFilter;
        private OutlinePassFinal _outlinePassFinal;

        private Material _overrideMaterial;
        private Material _blitMaterial;

        public override void Create()
        {
            if (FeatureSettings == null)
                return;

            if (FeatureSettings.OverrideShader == null)
                FeatureSettings.OverrideShader = Shader.Find("Hidden/S_Outlines_Normals");

            if (FeatureSettings.BlitShader == null)
                FeatureSettings.BlitShader = Shader.Find("Hidden/S_Outlines");

            if (FeatureSettings.OverrideShader != null)
                _overrideMaterial = CoreUtils.CreateEngineMaterial(FeatureSettings.OverrideShader);

            if (FeatureSettings.BlitShader != null)
                _blitMaterial = CoreUtils.CreateEngineMaterial(FeatureSettings.BlitShader);

            _outlinePassFilter = new OutlinePassFilter(FeatureSettings, _overrideMaterial);
            _outlinePassFinal = new OutlinePassFinal(FeatureSettings, MaterialSettings, _blitMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_overrideMaterial == null || _blitMaterial == null)
                return;

            renderer.EnqueuePass(_outlinePassFilter);
            renderer.EnqueuePass(_outlinePassFinal);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_overrideMaterial);
            CoreUtils.Destroy(_blitMaterial);
        }
    }
}