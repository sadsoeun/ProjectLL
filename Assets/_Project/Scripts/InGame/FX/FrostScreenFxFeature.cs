using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace MyGame
{
    /// <summary>
    /// URP renderer feature that draws <see cref="FrostScreenFx"/> as one full-screen pass (RenderGraph).
    /// Runs only for game cameras with post-processing on and an active FrostScreenFx volume, so every other stage
    /// pays nothing. Injected before URP post-processing so colour grading, vignette and the perfect-dodge grayscale
    /// also apply to the snow.
    /// </summary>
    public sealed class FrostScreenFxFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

        private Material material;
        private FrostPass pass;

        public override void Create()
        {
            pass = new FrostPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var fx = VolumeManager.instance.stack.GetComponent<FrostScreenFx>();
            if (fx == null || !fx.IsActive())
                return;
            if (material == null)
            {
                if (shader == null)
                    shader = Shader.Find(FrostPass.ShaderName);
                if (shader == null || !shader.isSupported)
                    return;
                material = CoreUtils.CreateEngineMaterial(shader);
            }
            pass.renderPassEvent = injectionPoint;
            pass.Setup(material, fx);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class FrostPass : ScriptableRenderPass
        {
            public const string ShaderName = "Hidden/MyGame/FrostScreen";

            private static readonly int IntensityId = Shader.PropertyToID("_FrostIntensity");
            private static readonly int SnowId = Shader.PropertyToID("_SnowParams");     // amount, size, speed
            private static readonly int SnowColorId = Shader.PropertyToID("_SnowColor");
            private static readonly int WindId = Shader.PropertyToID("_WindParams");     // dir.xy, strength, gust
            private static readonly int StreakId = Shader.PropertyToID("_StreakAmount");
            private static readonly int FrostId = Shader.PropertyToID("_FrostParams");   // width, opacity, distortion
            private static readonly int FrostTintId = Shader.PropertyToID("_FrostTint");

            private Material material;

            public FrostPass()
            {
                profilingSampler = new ProfilingSampler("Frost Screen FX");
                requiresIntermediateTexture = true;
            }

            public void Setup(Material mat, FrostScreenFx fx)
            {
                material = mat;
                Vector2 dir = fx.windDirection.value;
                if (dir.sqrMagnitude < 1e-4f)
                    dir = Vector2.left;
                dir.Normalize();
                material.SetFloat(IntensityId, fx.intensity.value);
                material.SetVector(SnowId, new Vector4(fx.snowAmount.value, fx.flakeSize.value, fx.snowSpeed.value, 0f));
                material.SetColor(SnowColorId, fx.snowColor.value);
                material.SetVector(WindId, new Vector4(dir.x, dir.y, fx.windStrength.value, fx.gustStrength.value));
                material.SetFloat(StreakId, fx.streakAmount.value);
                material.SetVector(FrostId, new Vector4(fx.frostWidth.value, fx.frostOpacity.value, fx.frostDistortion.value, 0f));
                material.SetColor(FrostTintId, fx.frostTint.value);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                if (material == null || resourceData.isActiveTargetBackBuffer)
                    return;
                if (cameraData.cameraType != CameraType.Game || !cameraData.postProcessEnabled)
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc desc = renderGraph.GetTextureDesc(source);
                desc.name = "_FrostScreenColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                renderGraph.AddBlitPass(blit, "Frost Screen FX");
                resourceData.cameraColor = destination;
            }
        }
    }
}
