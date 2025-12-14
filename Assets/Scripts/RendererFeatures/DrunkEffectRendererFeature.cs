using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

public class DrunkEffectRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Shader drunkShader;
    }

    public Settings settings = new Settings();
    private DrunkEffectRenderPass renderPass;

    public override void Create()
    {
        if (settings.drunkShader == null)
        {
            settings.drunkShader = Shader.Find("Hidden/DrunkDistortion");
            if (settings.drunkShader == null)
            {
                Debug.LogError("[DrunkEffectRendererFeature] Shader 'Hidden/DrunkDistortion' not found! Make sure the shader is in your project.");
            }
        }
        renderPass = new DrunkEffectRenderPass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (DrunkEffectController.Instance != null && DrunkEffectController.Instance.IsActive)
        {
            renderer.EnqueuePass(renderPass);
        }
    }

    protected override void Dispose(bool disposing)
    {
        renderPass?.Dispose();
    }

    class DrunkEffectRenderPass : ScriptableRenderPass
    {
        private Settings settings;
        private Material drunkMaterial;
        private static readonly int IntensityID = Shader.PropertyToID("_Intensity");
        private static readonly int SpeedID = Shader.PropertyToID("_Speed");

        public DrunkEffectRenderPass(Settings settings)
        {
            this.settings = settings;
            this.renderPassEvent = settings.renderPassEvent;

            if (settings.drunkShader != null)
            {
                drunkMaterial = new Material(settings.drunkShader);
                drunkMaterial.hideFlags = HideFlags.HideAndDontSave;
            }
            else
            {
                Debug.LogError("[DrunkEffectRenderPass] Cannot create material - shader is null!");
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (drunkMaterial == null || DrunkEffectController.Instance == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (cameraData.camera.cameraType != CameraType.Game)
                return;

            // Update shader parameters
            drunkMaterial.SetFloat(IntensityID, DrunkEffectController.Instance.CurrentIntensity);
            drunkMaterial.SetFloat(SpeedID, DrunkEffectController.Instance.Speed);

            TextureHandle source = resourceData.activeColorTexture;

            // Create temporary destination texture
            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            TextureHandle destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_DrunkEffectTemp", false);

            // Apply effect from source to destination
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Drunk Effect", out var passData))
            {
                passData.material = drunkMaterial;
                passData.source = source;

                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                builder.SetRenderFunc<PassData>((PassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Copy result back to source
            using (var builder = renderGraph.AddRasterRenderPass<CopyPassData>("Drunk Effect Copy Back", out var passData))
            {
                passData.source = destination;

                builder.UseTexture(destination, AccessFlags.Read);
                builder.SetRenderAttachment(source, 0, AccessFlags.Write);

                builder.SetRenderFunc<CopyPassData>((CopyPassData data, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, data.source, new Vector4(1, 1, 0, 0), 0, false);
                });
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (drunkMaterial == null || DrunkEffectController.Instance == null)
                return;

            if (renderingData.cameraData.camera.cameraType != CameraType.Game)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("DrunkEffect");

            drunkMaterial.SetFloat(IntensityID, DrunkEffectController.Instance.CurrentIntensity);
            drunkMaterial.SetFloat(SpeedID, DrunkEffectController.Instance.Speed);

            RTHandle source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // Use temporary RT for legacy path
            RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            cmd.GetTemporaryRT(Shader.PropertyToID("_TempDrunkRT"), desc);
            RTHandle temp = RTHandles.Alloc("_TempDrunkRT");

            Blitter.BlitCameraTexture(cmd, source, temp, drunkMaterial, 0);
            Blitter.BlitCameraTexture(cmd, temp, source);

            cmd.ReleaseTemporaryRT(Shader.PropertyToID("_TempDrunkRT"));

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            if (drunkMaterial != null)
            {
                Object.DestroyImmediate(drunkMaterial);
            }
        }

        private class PassData
        {
            public Material material;
            public TextureHandle source;
        }

        private class CopyPassData
        {
            public TextureHandle source;
        }
    }
}