using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Tablefront
{
    /// <summary>Sharp desktop rendering; the native HUD is composited after world post-processing.</summary>
    public static class TablefrontGraphics
    {
        public const float RenderScale = 1.5f;
        public const int MsaaSamples = 8;

        public static void Configure()
        {
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
            QualitySettings.lodBias = 4;
            QualitySettings.vSyncCount = 1;
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!urp) return;
            urp.supportsHDR = true;
            urp.renderScale = RenderScale;
            urp.msaaSampleCount = MsaaSamples;
            urp.useAdaptivePerformance = false;
            urp.mainLightShadowmapResolution = 8192;
            urp.shadowCascadeCount = 4;
            urp.shadowDistance = 100;
        }

        public static void NativeFullscreen()
        {
            var display = Screen.currentResolution;
            Screen.SetResolution(Mathf.Max(display.width, 1280), Mathf.Max(display.height, 720), FullScreenMode.FullScreenWindow);
        }

        public static void ToggleFullscreen()
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed) NativeFullscreen();
            else
            {
                var display = Screen.currentResolution;
                int width = Mathf.Min(1920, Mathf.Max(1024, display.width - 160));
                int height = Mathf.Min(1080, Mathf.Max(720, display.height - 160));
                Screen.SetResolution(width, height, FullScreenMode.Windowed);
            }
        }
    }
}
