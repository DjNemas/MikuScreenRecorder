using System.Globalization;
using System.Resources;
using MikuScreenRecorder.Resources;

namespace MikuScreenRecorder
{
    /// <summary>
    /// Verwaltet die UI-Sprache. Verfügbare Sprachen werden über den ResourceManager ermittelt
    /// (Resources/Strings.xx.resx), neue Sprachen benötigen daher keine Codeänderung.
    /// Funktioniert auch bei Single-File-Builds, da keine Ordner auf der Festplatte durchsucht werden.
    /// </summary>
    internal static class Localization
    {
        public const string DefaultLanguage = "en";

        public static event Action? LanguageChanged;

        public static string CurrentLanguage { get; private set; } = DefaultLanguage;

        public static IReadOnlyList<CultureInfo> AvailableLanguages { get; } = FindAvailableLanguages();

        /// <summary>
        /// Liefert die gespeicherte Sprache oder beim ersten Start die Systemsprache (Fallback Englisch).
        /// </summary>
        public static string Resolve(string? savedLanguage)
        {
            if (IsAvailable(savedLanguage)) return savedLanguage!;
            string system = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
            return IsAvailable(system) ? system : DefaultLanguage;
        }

        public static void SetLanguage(string language)
        {
            if (!IsAvailable(language)) language = DefaultLanguage;
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            bool changed = CurrentLanguage != language;
            CurrentLanguage = language;
            if (changed) LanguageChanged?.Invoke();
        }

        private static bool IsAvailable(string? language) =>
            language != null && AvailableLanguages.Any(c => c.Name.Equals(language, StringComparison.OrdinalIgnoreCase));

        private static List<CultureInfo> FindAvailableLanguages()
        {
            var result = new List<CultureInfo> { CultureInfo.GetCultureInfo(DefaultLanguage) };
            var manager = Strings.ResourceManager;

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
            {
                if (culture.Equals(CultureInfo.InvariantCulture) || culture.Name == DefaultLanguage) continue;
                try
                {
                    // tryParents = false: nur Sprachen mit eigener Ressourcendatei, kein Fallback auf Englisch
                    if (manager.GetResourceSet(culture, true, false) != null)
                        result.Add(culture);
                }
                catch (MissingManifestResourceException) { }
            }

            return result.OrderBy(c => c.NativeName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
