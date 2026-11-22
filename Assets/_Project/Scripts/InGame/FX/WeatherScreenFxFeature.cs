using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace MyGame
{
    /// <summary>
    /// URP renderer feature that draws <see cref="WeatherScreenFx"/> as one full-screen pass (RenderGraph).
    /// Same contract as <see cref="FrostScreenFxFeature"/>: runs only for game cameras with post-processing on and an
    /// active WeatherScreenFx volume, and is injected before URP post-processing so grading, vignette and the
    /// perfect-dodge grayscale also apply to it.
    /// </summary>
    public sealed class WeatherScreenFxFeature : ScriptableRendererFeature
    {
        [SerializeField] private Shader shader;
        [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

        private Material material;
        private WeatherPass pass;

        public override void Create()
        {
            pass = new WeatherPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var fx = VolumeManager.instance.stack.GetComponent<WeatherScreenFx>();
            if (fx == null || !fx.IsActive())
                return;
            if (material == null)
            {
                if (shader == null)
                    shader = Shader.Find(WeatherPass.ShaderName);
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

        private sealed class WeatherPass : ScriptableRenderPass
        {
            public const string ShaderName = "Hidden/MyGame/WeatherScreen";

            private static readonly int IntensityId = Shader.PropertyToID("_WxIntensity");
            private static readonly int StyleId = Shader.PropertyToID("_WxStyle");
            private static readonly int PartId = Shader.PropertyToID("_PartParams");       // amount, size, speed, glow
            private static readonly int PartColorId = Shader.PropertyToID("_PartColor");
            private static readonly int PartColorBId = Shader.PropertyToID("_PartColorB");
            private static readonly int PartMotionId = Shader.PropertyToID("_PartMotion"); // drift.xy, flutter, stretch
            private static readonly int WindId = Shader.PropertyToID("_WindParams");       // dir.xy, strength, gust
            private static readonly int HazeId = Shader.PropertyToID("_HazeColor");        // rgb, a = amount
            private static readonly int StreakId = Shader.PropertyToID("_StreakAmount");
            private static readonly int BorderId = Shader.PropertyToID("_BorderParams");   // width, opacity, distortion, detail
            private static readonly int BorderTintId = Shader.PropertyToID("_BorderTint");
            private static readonly int BorderGlowId = Shader.PropertyToID("_BorderGlow");
            private static readonly int HeatId = Shader.PropertyToID("_HeatParams");       // amplitude, scale, speed, splash

            private Material material;

            public WeatherPass()
            {
                profilingSampler = new ProfilingSampler("Weather Screen FX");
                requiresIntermediateTexture = true;
            }

            public void Setup(Material mat, WeatherScreenFx fx)
            {
                material = mat;
                Vector2 dir = fx.windDirection.value;
                if (dir.sqrMagnitude < 1e-4f)
                    dir = Vector2.left;
                dir.Normalize();
                Vector2 drift = fx.particleDrift.value;
                Color haze = fx.hazeColor.value;
                haze.a = fx.hazeAmount.value;

                material.SetFloat(IntensityId, fx.intensity.value);
                material.SetFloat(StyleId, (float)(int)fx.borderStyle.value);
                material.SetVector(PartId, new Vector4(fx.particleAmount.value, fx.particleSize.value, fx.particleSpeed.value, fx.particleGlow.value));
                material.SetColor(PartColorId, fx.particleColor.value);
                material.SetColor(PartColorBId, fx.particleColorB.value);
                material.SetVector(PartMotionId, new Vector4(drift.x, drift.y, fx.particleFlutter.value, fx.particleStretch.value));
                material.SetVector(WindId, new Vector4(dir.x, dir.y, fx.windStrength.value, fx.gustStrength.value));
                material.SetColor(HazeId, haze);
                material.SetFloat(StreakId, fx.streakAmount.value);
                material.SetVector(BorderId, new Vector4(fx.borderWidth.value, fx.borderOpacity.value, fx.borderDistortion.value, fx.borderDetail.value));
                material.SetColor(BorderTintId, fx.borderTint.value);
                material.SetColor(BorderGlowId, fx.borderGlow.value);
                material.SetVector(HeatId, new Vector4(fx.heatShimmer.value, fx.heatScale.value, fx.heatSpeed.value, fx.splashAmount.value));
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
                desc.name = "_WeatherScreenColor";
                desc.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(desc);

                var blit = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                renderGraph.AddBlitPass(blit, "Weather Screen FX");
                resourceData.cameraColor = destination;
            }
        }
    }
}
