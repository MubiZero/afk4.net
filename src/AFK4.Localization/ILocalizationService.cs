namespace AFK4.Localization;

/// <summary>
/// Shared WPF localization surface: resolves catalog keys with the P6 fallback
/// (<c>tg → ru → key</c>, <c>en → key</c>), holds the current locale and raises a
/// change event so bound UI re-resolves live, and exposes locale-aware formatters
/// (replacing <see cref="System.Globalization.CultureInfo.InvariantCulture"/> and
/// hard-coded currency codes on the WPF surfaces).
/// </summary>
public interface ILocalizationService
{
    string CurrentLocale { get; }

    event EventHandler? LocaleChanged;

    /// <summary>Sets the active locale (unknown values clamp to the default) and raises <see cref="LocaleChanged"/>.</summary>
    void SetLocale(string locale);

    /// <summary>Resolves a catalog key in the current locale with the P6 fallback chain.</summary>
    string T(string key);

    /// <summary>Formats a <c>long</c> minor-unit amount as localized currency using the explicit currency code.</summary>
    string FormatCurrency(long minorUnits, string currencyCode);

    string FormatNumber(decimal value);

    string FormatDate(DateTimeOffset value);
}
