using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MyGame
{
    /// <summary>
    /// URP renderer feature that draws the <see cref="ScreenOutline"/> background outline in one full-screen pass
    /// (RenderGraph). Needs the depth and normals textures (DepthNormals prepass; PC already has it for SSAO).
    /// Injected before transparents so water, particles, the frost screen FX and post-processing come after it.
    /// Works whether or not the camera has post-processing enabled (06_InGame keeps it off by default).
    /// </summary>
    public sealed class ScreenOutlineFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;
        [Tooltip("Also draw in Scene view cameras (handy for tuning).")]
        [SerializeField] private bool drawInSceneView = true;
        [Tooltip("Shows the edge mask in white instead of the composited result.")]
        [SerializeField] private bool debugEdges;

        private Material material;
        private OutlinePass pass;

        public override void Create()
        {
            pass = new OutlinePass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camType = renderingData.cameraData.cameraType;
            if (camType != CameraType.Game && !(drawInSceneView && camType == CameraType.SceneView))
                return;
            if (renderingData.cameraData.renderType == CameraRenderType.Overlay)
                return;
            var fx = VolumeManager.instance.stack.GetComponent<ScreenOutline>();
            if (fx == null || !fx.IsActive())
                return;
            if (material == null)
            {
                if (shader == null)
                    shader = Shader.Find(OutlinePass.ShaderName);
                if (shader == null || !shader.isSupported)
                    return;
                material = CoreUtils.CreateEngineMaterial(shader);
            }
            pass.renderPassEvent = injectionPoint;
            pass.Setup(material, fx, debugEdges);
            pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class OutlinePass : ScriptableRenderPass
        {
            public const string ShaderName = "Hidden/MyGame/ScreenOutline";

            private static readonly int ParamsId = Shader.PropertyToID("_OutlineParams");
            private static readonly int Params2Id = Shader.PropertyToID("_OutlineParams2");
            private static readonly int FadeId = Shader.PropertyToID("_OutlineFade");
            private static readonly int ColorId = Shader.PropertyToID("_OutlineColor");

            private Material material;
            private float thickness;

            private sealed class PassData
            {
                public TextureHandle source;
                public Material material;
            }

            public OutlinePass()
            {
                profilingSampler = new ProfilingSampler("Screen Outline");
                requiresIntermediateTexture = true;
            }

            public void Setup(Material mat, ScreenOutline fx, bool debug)
            {
                material = mat;
                thickness = fx.thickness.value;
                float fadeStart = fx.fadeStart.value;
                float fadeEnd = Mathf.Max(fx.fadeEnd.value, fadeStart + 0.01f);
                material.SetVector(Params2Id, new Vector4(fx.depthWeight.value, fx.normalWeight.value,
                    fx.foliageCreaseScale.value, fx.softness.value));
                material.SetVector(FadeId, new Vector4(fadeStart, fadeEnd, fx.nearFade.value, fx.maxDarkening.value));
                material.SetColor(ColorId, fx.tint.value);
                material.SetVector(ParamsId, new Vector4(fx.intensity.value, 0f, fx.depthThreshold.value, fx.normalThreshold.value));
                CoreUtils.SetKeyword(material, "_OUTLINE_DEBUG", debug);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (material == null || resourceData.isActiveTargetBackBuffer)
                    return;
                if (!resourceData.cameraDepthTexture.IsValid() || !resourceData.cameraNormalsTexture.IsValid())
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_ScreenOutlineColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                // Pixel offset: ~1 px at 1080p, scaled with the render height.
                float px = Mathf.Max(1f, Mathf.Round(thickness * desc.height / 1080f));
                var p = material.GetVector(ParamsId);
                p.y = px;
                material.SetVector(ParamsId, p);

                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Screen Outline", out var data, profilingSampler))
                {
                    data.source = source;
                    data.material = material;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
                    builder.UseTexture(resourceData.cameraNormalsTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
                    {
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, 0);
                    });
                }
                resourceData.cameraColor = destination;
            }
        }
    }
}
