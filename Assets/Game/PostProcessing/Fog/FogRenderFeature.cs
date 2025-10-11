using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace PSX
{
    public class FogRenderFeature : ScriptableRendererFeature
    {
        private FogPass _fogPass;

        public override void Create()
        {
            _fogPass = new FogPass(RenderPassEvent.BeforeRenderingPostProcessing);
        }
        
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_fogPass == null) return;
            renderer.EnqueuePass(_fogPass);
        }

        protected override void Dispose(bool disposing)
        {
            _fogPass?.Dispose();
        }
    }
    
    
    public class FogPass : ScriptableRenderPass
    {
        private static readonly string ShaderPath = "PostEffect/Fog";
        private static readonly string RenderTag = "Render Fog Effects";
        
        private static readonly int FogDensity = Shader.PropertyToID("_FogDensity");
        private static readonly int FogDistance = Shader.PropertyToID("_FogDistance");
        private static readonly int FogColor = Shader.PropertyToID("_FogColor");
        private static readonly int FogNear = Shader.PropertyToID("_FogNear");
        private static readonly int FogFar = Shader.PropertyToID("_FogFar");
        private static readonly int FogAltScale = Shader.PropertyToID("_FogAltScale");
        private static readonly int FogThinning = Shader.PropertyToID("_FogThinning");
        private static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseStrength = Shader.PropertyToID("_NoiseStrength");

        private Fog _fog;
        private readonly Material _fogMaterial;
        private RTHandle _tempRTHandle;

        private class PassData
        {
            internal TextureHandle source;
            internal Material material;
            internal float fogDensity;
            internal float fogDistance;
            internal Color fogColor;
            internal float fogNear;
            internal float fogFar;
            internal float fogAltScale;
            internal float fogThinning;
            internal float noiseScale;
            internal float noiseStrength;
        }
    
        public FogPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
            var shader = Shader.Find(ShaderPath);
            if (shader == null)
            {
                Debug.LogError("Shader not found: " + ShaderPath);
                return;
            }

            _fogMaterial = CoreUtils.CreateEngineMaterial(shader);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_fogMaterial == null)
            {
                Debug.LogError("Fog Material not created.");
                return;
            }

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            if (!cameraData.postProcessEnabled) return;

            var stack = VolumeManager.instance.stack;
            _fog = stack.GetComponent<Fog>();
            if (_fog == null || !_fog.IsActive()) return;

            cameraData.camera.depthTextureMode |= DepthTextureMode.Depth;

            var source = resourceData.activeColorTexture;
            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            
            var tempTexture = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph, descriptor, "_TempFog", false, FilterMode.Point);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(RenderTag, out var passData))
            {
                passData.source = source;
                passData.material = _fogMaterial;
                passData.fogDensity = _fog.fogDensity.value;
                passData.fogDistance = _fog.fogDistance.value;
                passData.fogColor = _fog.fogColor.value;
                passData.fogNear = _fog.fogNear.value;
                passData.fogFar = _fog.fogFar.value;
                passData.fogAltScale = _fog.fogAltScale.value;
                passData.fogThinning = _fog.fogThinning.value;
                passData.noiseScale = _fog.noiseScale.value;
                passData.noiseStrength = _fog.noiseStrength.value;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                {
                    data.material.SetFloat(FogDensity, data.fogDensity);
                    data.material.SetFloat(FogDistance, data.fogDistance);
                    data.material.SetColor(FogColor, data.fogColor);
                    data.material.SetFloat(FogNear, data.fogNear);
                    data.material.SetFloat(FogFar, data.fogFar);
                    data.material.SetFloat(FogAltScale, data.fogAltScale);
                    data.material.SetFloat(FogThinning, data.fogThinning);
                    data.material.SetFloat(NoiseScale, data.noiseScale);
                    data.material.SetFloat(NoiseStrength, data.noiseStrength);

                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Copy Fog Back", out var passData))
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
                TextureWrapMode.Clamp, name: "_TempTargetFog");
        }

        [Obsolete("This rendering path is for compatibility mode only.", false)]
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_fogMaterial == null) return;
            if (!renderingData.cameraData.postProcessEnabled) return;
    
            var stack = VolumeManager.instance.stack;
            _fog = stack.GetComponent<Fog>();
            if (_fog == null || !_fog.IsActive()) return;

            var cmd = CommandBufferPool.Get(RenderTag);
            Render(cmd, ref renderingData);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private void Render(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ref var cameraData = ref renderingData.cameraData;
            cameraData.camera.depthTextureMode |= DepthTextureMode.Depth;

            _fogMaterial.SetFloat(FogDensity, _fog.fogDensity.value);
            _fogMaterial.SetFloat(FogDistance, _fog.fogDistance.value);
            _fogMaterial.SetColor(FogColor, _fog.fogColor.value);
            _fogMaterial.SetFloat(FogNear, _fog.fogNear.value);
            _fogMaterial.SetFloat(FogFar, _fog.fogFar.value);
            _fogMaterial.SetFloat(FogAltScale, _fog.fogAltScale.value);
            _fogMaterial.SetFloat(FogThinning, _fog.fogThinning.value);
            _fogMaterial.SetFloat(NoiseScale, _fog.noiseScale.value);
            _fogMaterial.SetFloat(NoiseStrength, _fog.noiseStrength.value);

#pragma warning disable CS0618
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;
#pragma warning restore CS0618

            Blitter.BlitCameraTexture(cmd, source, _tempRTHandle, _fogMaterial, 0);
            Blitter.BlitCameraTexture(cmd, _tempRTHandle, source);
        }

        public void Dispose()
        {
            _tempRTHandle?.Release();
            CoreUtils.Destroy(_fogMaterial);
        }
    }
}