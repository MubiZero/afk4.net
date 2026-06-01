using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace AFK4.Localization;

/// <summary>
/// Default <see cref="ILocalizationService"/> over an in-memory catalog (one
/// <c>key → value</c> dictionary per locale). Resolution and formatting mirror the
/// React surfaces: P6 fallback for keys, locale <see cref="CultureInfo"/> for
/// numbers/dates, and an explicit currency code (never a culture symbol) for money.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogs;
    private string currentLocale;

    public LocalizationService(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogs,
        string initialLocale)
    {
        this.catalogs = catalogs;
        currentLocale = Locales.Clamp(initialLocale);
    }

    /// <summary>
    /// Builds a service from the embedded shared catalog (<c>locales/{ru,en,tg}.json</c>,
    /// the same source of truth as the React surfaces).
    /// </summary>
    public static LocalizationService LoadEmbedded(string initialLocale)
    {
        var assembly = typeof(LocalizationService).Assembly;
        var catalogs = new Dictionary<string, IReadOnlyDictionary<string, string>>();

        foreach (var locale in Locales.Supported)
        {
            catalogs[locale] = ReadCatalog(assembly, $"AFK4.Localization.Catalog.{locale}.json");
        }

        return new LocalizationService(catalogs, initialLocale);
    }

    private static IReadOnlyDictionary<string, string> ReadCatalog(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded locale catalog '{resourceName}' was not found.");

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Embedded locale catalog '{resourceName}' was empty or malformed.");
    }

    public string CurrentLocale => currentLocale;

    public event EventHandler? LocaleChanged;

    public void SetLocale(string locale)
    {
        var clamped = Locales.Clamp(locale);
        if (clamped == currentLocale)
        {
            return;
        }

        currentLocale = clamped;
        LocaleChanged?.Invoke(this, EventArgs.Empty);
    }

    public string T(string key)
    {
        foreach (var locale in FallbackChain(currentLocale))
        {
            if (catalogs.TryGetValue(locale, out var dict) &&
                dict.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        // Missing-key / missing-locale fallback (P6): the key string itself.
        return key;
    }

    public string FormatCurrency(long minorUnits, string currencyCode)
    {
        var major = minorUnits / 100m;
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(Locales.Tag(currentLocale)).Clone();

        // Mirror the React `Intl.NumberFormat({style:'currency', maximumFractionDigits:0})`
        // convention but with the *explicit* currency code as the symbol, so display is
        // locale-aware while the stored currency is never substituted by the culture's own.
        var nfi = (NumberFormatInfo)culture.NumberFormat.Clone();
        nfi.CurrencySymbol = currencyCode;
        nfi.CurrencyDecimalDigits = 0;
        culture.NumberFormat = nfi;

        return major.ToString("C", culture);
    }

    public string FormatNumber(decimal value) =>
        value.ToString("#,0.###", CultureInfo.GetCultureInfo(Locales.Tag(currentLocale)));

    public string FormatDate(DateTimeOffset value) =>
        value.ToString("g", CultureInfo.GetCultureInfo(Locales.Tag(currentLocale)));

    /// <summary>P6 locale fallback order: <c>tg → ru</c>, <c>en</c> and <c>ru</c> stand alone.</summary>
    private static IEnumerable<string> FallbackChain(string locale) => locale switch
    {
        Locales.Tg => [Locales.Tg, Locales.Ru],
        Locales.En => [Locales.En],
        _ => [Locales.Ru],
    };
}
