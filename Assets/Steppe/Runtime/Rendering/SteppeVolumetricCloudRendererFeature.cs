using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Steppe.Rendering
{
    /// <summary>
    /// Renders the weather-driven cloud volume at half resolution, then composites it
    /// over sky pixels. The weather simulation publishes shader globals; this feature
    /// owns only the URP integration and transient render target.
    /// </summary>
    public sealed class SteppeVolumetricCloudRendererFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/Steppe/Volumetric Clouds";

        [SerializeField] private Shader cloudShader;

        private Material cloudMaterial;
        private VolumetricCloudPass cloudPass;

        public static bool PresentationActive { get; private set; }
        public Shader CloudShader => cloudShader;

        public static void SetPresentationActive(bool active)
        {
            PresentationActive = active;
        }

        public void SetCloudShader(Shader value)
        {
            cloudShader = value;
            Create();
        }

        public override void Create()
        {
            CoreUtils.Destroy(cloudMaterial);
            cloudMaterial = null;
            cloudPass = null;

            if (cloudShader == null)
            {
                cloudShader = Shader.Find(ShaderName);
            }

            if (cloudShader == null)
            {
                return;
            }

            cloudMaterial = CoreUtils.CreateEngineMaterial(cloudShader);
            cloudMaterial.name = "Steppe Volumetric Cloud Renderer Material";
            cloudPass = new VolumetricCloudPass(cloudMaterial);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!PresentationActive || cloudMaterial == null || cloudPass == null)
            {
                return;
            }

            var cameraType = renderingData.cameraData.cameraType;
            if (cameraType == CameraType.Preview || cameraType == CameraType.Reflection)
            {
                return;
            }

            renderer.EnqueuePass(cloudPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(cloudMaterial);
            cloudMaterial = null;
            cloudPass = null;
        }

        private sealed class VolumetricCloudPass : ScriptableRenderPass
        {
            private static readonly int CloudTextureId = Shader.PropertyToID("_SteppeCloudTexture");
            private static readonly int CameraDepthTextureId = Shader.PropertyToID("_SteppeCloudCameraDepthTexture");
            private static readonly int CloudUvScaleBiasId = Shader.PropertyToID("_SteppeCloudUvScaleBias");
            private static readonly int DepthUvScaleBiasId = Shader.PropertyToID("_SteppeDepthUvScaleBias");

            private readonly Material material;
            private readonly MaterialPropertyBlock raymarchProperties = new MaterialPropertyBlock();
            private readonly MaterialPropertyBlock compositeProperties = new MaterialPropertyBlock();

            public VolumetricCloudPass(Material cloudMaterial)
            {
                material = cloudMaterial;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                profilingSampler = new ProfilingSampler("Steppe Volumetric Clouds");
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.activeColorTexture.IsValid())
                {
                    return;
                }

                var cloudDescriptor = renderGraph.GetTextureDesc(resources.activeColorTexture);
                cloudDescriptor.name = "_SteppeVolumetricCloudTexture";
                cloudDescriptor.width = Mathf.Max(1, cloudDescriptor.width / 2);
                cloudDescriptor.height = Mathf.Max(1, cloudDescriptor.height / 2);
                cloudDescriptor.format = SystemInfo.IsFormatSupported(
                    GraphicsFormat.R16G16B16A16_SFloat,
                    GraphicsFormatUsage.Render)
                    ? GraphicsFormat.R16G16B16A16_SFloat
                    : GraphicsFormat.R8G8B8A8_UNorm;
                cloudDescriptor.msaaSamples = MSAASamples.None;
                cloudDescriptor.bindTextureMS = false;
                cloudDescriptor.clearBuffer = true;
                cloudDescriptor.clearColor = Color.clear;
                cloudDescriptor.filterMode = FilterMode.Bilinear;
                cloudDescriptor.wrapMode = TextureWrapMode.Clamp;
                var cloudTexture = renderGraph.CreateTexture(cloudDescriptor);

                using (var builder = renderGraph.AddRasterRenderPass<RaymarchPassData>(
                           "Steppe Cloud Raymarch",
                           out var passData,
                           profilingSampler))
                {
                    passData.material = material;
                    passData.properties = raymarchProperties;
                    builder.SetRenderAttachment(cloudTexture, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (RaymarchPassData data, RasterGraphContext context) =>
                    {
                        data.properties.Clear();
                        CoreUtils.DrawFullScreen(context.cmd, data.material, data.properties, 0);
                    });
                }

                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "Steppe Cloud Composite",
                           out var passData,
                           profilingSampler))
                {
                    passData.material = material;
                    passData.properties = compositeProperties;
                    passData.cloudTexture = cloudTexture;
                    passData.depthTexture = resources.cameraDepthTexture;
                    passData.destination = resources.activeColorTexture;

                    builder.UseTexture(cloudTexture, AccessFlags.Read);
                    if (resources.cameraDepthTexture.IsValid())
                    {
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    }

                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                    {
                        data.properties.Clear();
                        data.properties.SetTexture(CloudTextureId, data.cloudTexture);
                        data.properties.SetVector(
                            CloudUvScaleBiasId,
                            GetUvScaleBias(context, data.cloudTexture, data.destination));

                        if (data.depthTexture.IsValid())
                        {
                            data.properties.SetTexture(CameraDepthTextureId, data.depthTexture);
                            data.properties.SetVector(
                                DepthUvScaleBiasId,
                                GetUvScaleBias(context, data.depthTexture, data.destination));
                        }
                        else
                        {
                            data.properties.SetVector(DepthUvScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                        }

                        CoreUtils.DrawFullScreen(context.cmd, data.material, data.properties, 1);
                    });
                }
            }

            private static Vector4 GetUvScaleBias(
                RasterGraphContext context,
                TextureHandle source,
                TextureHandle destination)
            {
                var flipY = context.GetTextureUVOrigin(in source)
                            != context.GetTextureUVOrigin(in destination);
                return flipY
                    ? new Vector4(1f, -1f, 0f, 1f)
                    : new Vector4(1f, 1f, 0f, 0f);
            }

            private sealed class RaymarchPassData
            {
                public Material material;
                public MaterialPropertyBlock properties;
            }

            private sealed class CompositePassData
            {
                public Material material;
                public MaterialPropertyBlock properties;
                public TextureHandle cloudTexture;
                public TextureHandle depthTexture;
                public TextureHandle destination;
            }
        }
    }
}
