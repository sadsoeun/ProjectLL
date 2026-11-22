using System;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// Current language + string lookup. The language chosen here is a runtime value;
    /// persisting it is SettingsService's job.
    /// </summary>
    public sealed class LocalizationService : MonoBehaviour
    {
        public string CurrentLanguage { get; private set; } = LocalizationTable.DefaultLanguage;
        public event Action LanguageChanged;

        public void Initialize(SettingsService settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            string code;
            if (settings.Data.hasSelectedLanguage && LocalizationTable.IsSupported(settings.Data.languageCode))
                code = settings.Data.languageCode;
            else
                code = LocalizationTable.FromSystemLanguage(Application.systemLanguage); // temporary until the user chooses (spec 4.3)

            CurrentLanguage = code;
        }

        public void SetLanguage(string code)
        {
            if (!LocalizationTable.IsSupported(code))
            {
                Debug.LogWarning($"[LocalizationService] Unsupported language '{code}'. Ignored.");
                return;
            }
            if (code == CurrentLanguage)
                return;
            CurrentLanguage = code;
            LanguageChanged?.Invoke();
        }

        public string Get(string key)
        {
            if (LocalizationTable.TryGet(CurrentLanguage, key, out var value))
                return value;
            Debug.LogWarning($"[LocalizationService] Missing key '{key}'.");
            return key;
        }

        public string Get(string key, params object[] args)
        {
            string format = Get(key);
            if (args == null || args.Length == 0)
                return format;
            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                Debug.LogWarning($"[LocalizationService] Bad format string for key '{key}'.");
                return format;
            }
        }
    }
}
