using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace PSX
{
    public class PixelationRenderFeature : ScriptableRendererFeature
    {
        private PixelationPass _pixelationPass;

        public override void Create()
        {
            _pixelationPass = new PixelationPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_pixelationPass == null) return;
            renderer.EnqueuePass(_pixelationPass);
        }

        protected override void Dispose(bool disposing)
        {
            _pixelationPass?.Dispose();
        }
    }
    
    
    public class PixelationPass : ScriptableRenderPass
    {
        private static readonly string ShaderPath = "PostEffect/Pixelation";
        private static readonly string RenderTag = "Render Pixelation Effects";
        
        private static readonly int WidthPixelation = Shader.PropertyToID("_WidthPixelation");
        private static readonly int HeightPixelation = Shader.PropertyToID("_HeightPixelation");
        private static readonly int ColorPrecision = Shader.PropertyToID("_ColorPrecision");

        private Pixelation _pixelation;
        private readonly Material _pixelationMaterial;
        private RTHandle _tempRTHandle;

        private class PassData
        {
            internal TextureHandle source;
            internal Material material;
            internal float widthPixelation;
            internal float heightPixelation;
            internal float colorPrecision;
        }
    
        public PixelationPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            var shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("Shader not found: " + ShaderPath);
                return;
            }

            _pixelationMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_pixelationMaterial == null)
            {
                Debug.LogError("Pixelation Material not created.");
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (!cameraData.postProcessEnabled) return;

            var stack = VolumeManager.instance.stack;
            _pixelation = stack.GetComponent<Pixelation>();
            if (_pixelation == null || !_pixelation.IsActive()) return;

            cameraData.camera.depthTextureMode |= DepthTextureMode.Depth;

            var source = resourceData.activeColorTexture;
            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            
            var tempTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "_TempPixelation", false, FilterMode.Point);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(RenderTag, out var passData))
            {
                passData.source = source;
                passData.material = _pixelationMaterial;
                passData.widthPixelation = _pixelation.widthPixelation.value;
                passData.heightPixelation = _pixelation.heightPixelation.value;
                passData.colorPrecision = _pixelation.colorPrecision.value;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetFloat(WidthPixelation, data.widthPixelation);
                    data.material.SetFloat(HeightPixelation, data.heightPixelation);
                    data.material.SetFloat(ColorPrecision, data.colorPrecision);

                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Copy Pixelation Back", out var passData))
            {
                passData.source = tempTexture;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        [Obsolete("This rendering path is for compatibility mode only.", false)]
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _tempRTHandle, descriptor, FilterMode.Point,
                TextureWrapMode.Clamp, name: "_TempTargetPixelation");
        }

        [Obsolete("This rendering path is for compatibility mode only.", false)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_pixelationMaterial == null) return;
            if (!renderingData.cameraData.postProcessEnabled) return;
    
            var stack = VolumeManager.instance.stack;
            _pixelation = stack.GetComponent<Pixelation>();
            if (_pixelation == null || !_pixelation.IsActive()) return;

            var cmd = CommandBufferPool.Get(RenderTag);
            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            cameraData.camera.depthTextureMode |= DepthTextureMode.Depth;

            _pixelationMaterial.SetFloat(WidthPixelation, _pixelation.widthPixelation.value);
            _pixelationMaterial.SetFloat(HeightPixelation, _pixelation.heightPixelation.value);
            _pixelationMaterial.SetFloat(ColorPrecision, _pixelation.colorPrecision.value);

#pragma warning disable CS0618
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
#pragma warning restore CS0618

            Blitter.BlitCameraTexture(cmd, source, _tempRTHandle, _pixelationMaterial, 0);
            Blitter.BlitCameraTexture(cmd, _tempRTHandle, source);
        }

        public void Dispose()
        {
            _tempRTHandle?.Release();
            CoreUtils.Destroy(_pixelationMaterial);
        }
    }
}