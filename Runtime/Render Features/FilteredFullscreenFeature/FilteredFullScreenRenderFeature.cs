using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace Chishikii.RenderFeatures
{
    public class FilteredFullScreenRenderFeature : ScriptableRendererFeature
    {
        [Serializable]
        public class Settings
        {
            public string Name = "Filtered Full Screen";

            public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;

            public LayerMask LayerMask = 0;

            public RenderingLayerMask RenderingLayerMask = 0;

            public bool ClearDepth;

            public Material OverrideMaterial;

            public Material FullScreenMaterial;
        }

        public class FilterData : ContextItem
        {
            public TextureHandle FilterTextureHandle;

            public override void Reset()
            {
                FilterTextureHandle = TextureHandle.nullHandle;
            }
        }

        public Settings FeatureSettings;

        private FilterPass _filterPass;
        private FullScreenPass _fullScreenPass;

        public override void Create()
        {
            if (FeatureSettings == null)
                return;

            _filterPass = new FilterPass(FeatureSettings);
            _fullScreenPass = new FullScreenPass(FeatureSettings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_filterPass == null || _fullScreenPass == null)
                return;

            renderer.EnqueuePass(_filterPass);
            renderer.EnqueuePass(_fullScreenPass);
        }
    }
}