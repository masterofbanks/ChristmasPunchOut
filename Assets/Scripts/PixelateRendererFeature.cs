using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class PixelateRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        [Range(64, 1920)]
        public int targetWidth = 320;
        [Range(64, 1080)]
        public int targetHeight = 180;
    }

    public Settings settings = new Settings();
    private PixelateRenderPass renderPass;

    public override void Create()
    {
        renderPass = new PixelateRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        renderPass?.Dispose();
    }

    class PixelateRenderPass : ScriptableRenderPass
    {
        private Settings settings;
        private RTHandle tempTexture;

        public PixelateRenderPass(Settings settings)
        {
            this.settings = settings;
            this.renderPassEvent = settings.renderPassEvent;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.camera.cameraType != CameraType.Game)
                return;

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.width = settings.targetWidth;
            descriptor.height = settings.targetHeight;
            descriptor.depthBufferBits = 0;

            TextureHandle source = resourceData.activeColorTexture;
            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_TemporaryPixelateTexture", false, FilterMode.Point);

            // First pass: Blit to low-res texture
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixelate Effect Downscale", out var passData))
            {
                passData.source = source;
                passData.destination = tempTexture;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc<PassData>((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }

            // Second pass: Blit back to source
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Pixelate Effect Upscale", out var passData))
            {
                passData.source = tempTexture;
                passData.destination = source;

                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);
                builder.SetRenderFunc<PassData>((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        // Legacy rendering path (for compatibility mode)
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.width = settings.targetWidth;
            descriptor.height = settings.targetHeight;
            descriptor.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(ref tempTexture, descriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_TemporaryPixelateTexture");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.camera.cameraType != CameraType.Game)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("PixelateEffect");

            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            Blitter.BlitCameraTexture(cmd, source, tempTexture);
            Blitter.BlitCameraTexture(cmd, tempTexture, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // Cleanup handled by RTHandle system
        }

        public void Dispose()
        {
            tempTexture?.Release();
        }

        private class PassData
        {
            public TextureHandle source;
            public TextureHandle destination;
        }
    }
}