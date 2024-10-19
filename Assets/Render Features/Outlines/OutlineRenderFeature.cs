using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Xenon
{
    public class OutlineRenderFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

            public LayerMask LayerMask = 0;

            public RenderingLayerMask RenderingLayerMask = 0;

            public Material OverrideMaterial;

            public Material BlitMaterial;

            public bool ClearDepth;
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

        public override void Create()
        {
            _outlinePassFilter = new OutlinePassFilter(FeatureSettings);
            _outlinePassFinal = new OutlinePassFinal(FeatureSettings, MaterialSettings);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_outlinePassFilter);
            renderer.EnqueuePass(_outlinePassFinal);
        }
    }
}