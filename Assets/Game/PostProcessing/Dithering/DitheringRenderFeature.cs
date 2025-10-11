using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace PSX
{
    public class DitheringRenderFeature : ScriptableRendererFeature
    {
        private DitheringPass _ditheringPass;

        public override void Create()
        {
            _ditheringPass = new DitheringPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_ditheringPass == null) return;
            renderer.EnqueuePass(_ditheringPass);
        }

        protected override void Dispose(bool disposing)
        {
            _ditheringPass?.Dispose();
        }
    }
    
    public class DitheringPass : ScriptableRenderPass
    {
        private static readonly string ShaderPath = "PostEffect/Dithering";
        private static readonly string RenderTag = "Render Dithering Effects";
        
        private static readonly int PatternIndex = Shader.PropertyToID("_PatternIndex");
        private static readonly int DitherThreshold = Shader.PropertyToID("_DitherThreshold");
        private static readonly int DitherStrength = Shader.PropertyToID("_DitherStrength");
        private static readonly int DitherScale = Shader.PropertyToID("_DitherScale");

        private Dithering _dithering;
        private Material _ditheringMaterial;
        private RTHandle _tempRTHandle;

        private class PassData
        {
            internal TextureHandle source;
            internal Material material;
            internal int patternIndex;
            internal float ditherThreshold;
            internal float ditherStrength;
            internal float ditherScale;
        }
    
        public DitheringPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            var shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("Shader not found: " + ShaderPath);
                return;
            }
            _ditheringMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_ditheringMaterial == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (!cameraData.postProcessEnabled) return;

            var stack = VolumeManager.instance.stack;
            _dithering = stack.GetComponent<Dithering>();
            if (_dithering == null || !_dithering.IsActive()) return;

            var source = resourceData.activeColorTexture;
            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            
            var tempTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "_TempDithering", false);

            // Pass 1: Применяем эффект (source -> temp)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(RenderTag, out var passData))
            {
                passData.source = source;
                passData.material = _ditheringMaterial;
                passData.patternIndex = _dithering.patternIndex.value;
                passData.ditherThreshold = _dithering.ditherThreshold.value;
                passData.ditherStrength = _dithering.ditherStrength.value;
                passData.ditherScale = _dithering.ditherScale.value;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    data.material.SetInt(PatternIndex, data.patternIndex);
                    data.material.SetFloat(DitherThreshold, data.ditherThreshold);
                    data.material.SetFloat(DitherStrength, data.ditherStrength);
                    data.material.SetFloat(DitherScale, data.ditherScale);

                    Blitter.BlitTexture(ctx.cmd, data.source, Vector4.one, data.material, 0);
                });
            }

            // Pass 2: Копируем обратно (temp -> source)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Copy Back", out var passData))
            {
                passData.source = tempTexture;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
                {
                    Blitter.BlitTexture(ctx.cmd, data.source, Vector4.one, 0, false);
                });
            }
        }

        [Obsolete("Compatibility mode only", false)]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _tempRTHandle, descriptor, FilterMode.Point,
                TextureWrapMode.Clamp, name: "_TempDithering");
        }

        [Obsolete("Compatibility mode only", false)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_ditheringMaterial == null) return;
            if (!renderingData.cameraData.postProcessEnabled) return;
    
            var stack = VolumeManager.instance.stack;
            _dithering = stack.GetComponent<Dithering>();
            if (_dithering == null || !_dithering.IsActive()) return;

            var cmd = CommandBufferPool.Get(RenderTag);
            
            _ditheringMaterial.SetInt(PatternIndex, _dithering.patternIndex.value);
            _ditheringMaterial.SetFloat(DitherThreshold, _dithering.ditherThreshold.value);
            _ditheringMaterial.SetFloat(DitherStrength, _dithering.ditherStrength.value);
            _ditheringMaterial.SetFloat(DitherScale, _dithering.ditherScale.value);

#pragma warning disable CS0618
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
#pragma warning restore CS0618

            Blitter.BlitCameraTexture(cmd, source, _tempRTHandle, _ditheringMaterial, 0);
            Blitter.BlitCameraTexture(cmd, _tempRTHandle, source);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _tempRTHandle?.Release();
            CoreUtils.Destroy(_ditheringMaterial);
        }
    }
}