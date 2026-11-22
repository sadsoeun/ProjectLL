using System;
using System.IO;
using UnityEngine;

namespace MyGame
{
    /// <summary>Loads, saves and applies device settings (settings.json).</summary>
    public sealed class SettingsService : MonoBehaviour
    {
        public const string FileName = "settings.json";

        public SettingsData Data { get; private set; } = new SettingsData();
        /// <summary>True if the settings file existed but was unreadable (defaults were used).</summary>
        public bool WasCorrupted { get; private set; }

        /// <summary>Raised after Data was modified via NotifyChanged (volumes, display).</summary>
        public event Action Changed;

        private string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Loads from disk. Missing or corrupt files yield defaults with 'language not selected' (spec 8).</summary>
        public void Load()
        {
            WasCorrupted = false;
            var result = JsonFileStore.TryRead(FilePath, out SettingsData loaded);
            if (result == ReadResult.Ok && loaded.Sanitize())
            {
                Data = loaded;
                return;
            }

            if (result != ReadResult.Missing)
            {
                WasCorrupted = true;
                Debug.LogWarning("[SettingsService] settings.json is corrupt or has an unsupported version. Using defaults.");
            }
            Data = new SettingsData { hasSelectedLanguage = false };
        }

        public bool Save()
        {
            return JsonFileStore.TryWrite(FilePath, Data);
        }

        /// <summary>Call after changing Data fields so audio/display and listeners update.</summary>
        public void NotifyChanged()
        {
            ApplyAll();
            Changed?.Invoke();
        }

        public void ApplyAll()
        {
            ApplyDisplay();
            Changed?.Invoke();
        }

        private void ApplyDisplay()
        {
            QualitySettings.vSyncCount = Data.vSync ? 1 : 0;

#if UNITY_WEBGL
            // Browser: the page owns the canvas size and the browser paces the frames. Asking for the display's full
            // resolution here would render far more pixels than the canvas shows.
            if (!Application.isEditor)
                return;
#endif
            // Window changes are skipped in the Editor (Game view is not a real window).
            if (Application.isEditor)
            {
                // The Game view does not follow vSyncCount (its own VSync toggle is off by default), so play mode ran
                // uncapped at several hundred fps with uneven frame pacing. Cap it to the display refresh rate instead,
                // which is what the build does through VSync. Left alone while a recording drives the frame rate.
                if (Time.captureFramerate == 0)
                {
                    QualitySettings.vSyncCount = 0; // targetFrameRate is ignored while vSyncCount > 0
                    int refresh = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
                    Application.targetFrameRate = Data.vSync && refresh > 0 ? refresh : -1;
                }
                return;
            }

            // The chosen resolution applies in both modes. Windowed: it is the window size. Fullscreen (borderless):
            // the window always has the display's own size and the choice sets the 3D render scale instead (the UI
            // stays sharp). A borderless window with a smaller, stretched back buffer must not be used: on Direct3D 12
            // it comes out without the sRGB conversion, i.e. the whole picture far too dark.
            // Only re-applied on a real change: this runs on every settings change, volume sliders included.
            var mode = Data.fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (!TryParseResolution(Data.resolution, out int w, out int h))
            {
                w = Screen.width;
                h = Screen.height;
            }
            float scale = 1f;
            if (Data.fullscreen && Display.main != null && Display.main.systemWidth > 0 && Display.main.systemHeight > 0)
            {
                int nativeW = Display.main.systemWidth, nativeH = Display.main.systemHeight;
                scale = Mathf.Clamp(Mathf.Min((float)w / nativeW, (float)h / nativeH), MinRenderScale, 1f);
                w = nativeW;
                h = nativeH;
            }
            if (w == appliedWidth && h == appliedHeight && mode == appliedMode && Mathf.Approximately(scale, appliedScale))
                return;
            bool windowChanged = w != appliedWidth || h != appliedHeight || mode != appliedMode;
            appliedWidth = w;
            appliedHeight = h;
            appliedMode = mode;
            appliedScale = scale;
            if (windowChanged)
                Screen.SetResolution(w, h, mode);
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset urp)
                urp.renderScale = scale;
        }

        private const float MinRenderScale = 0.25f;
        private float appliedScale = -1f;
        private int appliedWidth, appliedHeight;
        private FullScreenMode appliedMode = (FullScreenMode)(-1);

        private static readonly int[] PresetHeights = { 720, 900, 1080, 1440, 2160 };

        /// <summary>16:9 resolutions offered in the settings: every preset that fits the display, plus the current one.</summary>
        public static System.Collections.Generic.List<string> ResolutionOptions(string current)
        {
            int maxW = Display.main != null ? Display.main.systemWidth : 0, maxH = Display.main != null ? Display.main.systemHeight : 0;
            var list = new System.Collections.Generic.List<string>();
            foreach (int height in PresetHeights)
            {
                int width = height * 16 / 9;
                if (list.Count == 0 || maxW <= 0 || (width <= maxW && height <= maxH))
                    list.Add(width + "x" + height);
            }
            if (TryParseResolution(current, out _, out _) && !list.Contains(current))
            {
                list.Add(current);
                list.Sort((a, b) => { TryParseResolution(a, out int aw, out _); TryParseResolution(b, out int bw, out _); return aw.CompareTo(bw); });
            }
            return list;
        }

        public static bool TryParseResolution(string text, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (string.IsNullOrEmpty(text))
                return false;
            var parts = text.Split('x');
            return parts.Length == 2
                && int.TryParse(parts[0], out width)
                && int.TryParse(parts[1], out height)
                && width >= 320 && height >= 240;
        }
    }
}
