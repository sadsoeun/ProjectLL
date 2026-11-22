using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Supplies the font for a language. Bundled Noto Sans CJK (SIL OFL 1.1, Resources/Fonts) is used first, in the
    /// regional variant whose Han glyph shapes fit the language; dynamic OS fonts are only a fallback.
    /// Legacy uGUI Text is used on purpose: TextMeshPro would need manual font-asset creation.
    /// </summary>
    public static class FontProvider
    {
        private const string BundledFontFolder = "Fonts/";

        private static readonly Dictionary<string, Font> Cache = new Dictionary<string, Font>();
        private static Font builtin;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
            builtin = null;
        }

        // Latin languages share the KR variant (it includes full Latin with Spanish accents).
        private static string BundledFontName(string languageCode)
        {
            switch (languageCode)
            {
                case "zh-Hans": return "NotoSansSC-Regular";
                case "ja": return "NotoSansJP-Regular";
                default: return "NotoSansKR-Regular";
            }
        }

        private static string[] Candidates(string languageCode)
        {
            switch (languageCode)
            {
                case "zh-Hans":
                    return new[] { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Malgun Gothic", "Yu Gothic", "Arial" };
                case "ko":
                    return new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Microsoft YaHei", "Yu Gothic", "Arial" };
                case "ja":
                    return new[] { "Yu Gothic", "Yu Gothic UI", "Meiryo", "MS Gothic", "Hiragino Sans", "Noto Sans CJK JP", "Malgun Gothic", "Microsoft YaHei", "Arial" };
                default:
                    return new[] { "Segoe UI", "Arial", "Helvetica", "Malgun Gothic", "Microsoft YaHei", "Yu Gothic" };
            }
        }

        public static Font GetBuiltin()
        {
            if (builtin == null)
                builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return builtin;
        }

        public static Font Get(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                languageCode = LocalizationTable.DefaultLanguage;
            if (Cache.TryGetValue(languageCode, out var cached) && cached != null)
                return cached;

            Font font = Resources.Load<Font>(BundledFontFolder + BundledFontName(languageCode));
            if (font != null)
            {
                Cache[languageCode] = font;
                Debug.Log($"[FontProvider] '{languageCode}' -> {font.name} (bundled)");
                return font;
            }
            Debug.LogWarning($"[FontProvider] Bundled font missing for '{languageCode}', falling back to OS fonts.");

            try
            {
                var wanted = Candidates(languageCode);
                var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
                var usable = new List<string>();
                foreach (var name in wanted)
                    if (installed.Contains(name)) usable.Add(name);

                // If detection failed (naming differences), still pass the full list: Unity skips unknown names.
                var names = usable.Count > 0 ? usable.ToArray() : wanted;
                font = Font.CreateDynamicFontFromOSFont(names, 16);
                Debug.Log($"[FontProvider] '{languageCode}' -> {string.Join(", ", names)}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[FontProvider] OS font creation failed for '{languageCode}': {e.Message}");
            }

            if (font == null)
                font = GetBuiltin();
            Cache[languageCode] = font;
            return font;
        }
    }
}
