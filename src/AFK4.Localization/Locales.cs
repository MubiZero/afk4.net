namespace AFK4.Localization;

/// <summary>
/// The shared locale set (mirrors the React surfaces' <c>Locale</c> union and
/// <c>LOCALE_TAG</c> map) plus clamp/tag helpers. The single source of truth for
/// which locales exist and how they map to BCP-47 / .NET culture tags.
/// </summary>
public static class Locales
{
    public const string Ru = "ru";
    public const string En = "en";
    public const string Tg = "tg";

    /// <summary>Default locale for every surface (P1).</summary>
    public const string Default = Ru;

    public static IReadOnlyList<string> Supported { get; } = [Ru, En, Tg];

    public static bool IsSupported(string? locale) =>
        locale is not null && (locale == Ru || locale == En || locale == Tg);

    /// <summary>Unknown/invalid locale clamps to the default (never throws); P-edge-case.</summary>
    public static string Clamp(string? locale) => IsSupported(locale) ? locale! : Default;

    /// <summary>BCP-47 / .NET culture tag for a locale (parity with React <c>LOCALE_TAG</c>).</summary>
    public static string Tag(string locale) => locale switch
    {
        En => "en-US",
        Tg => "tg-TJ",
        _ => "ru-RU",
    };
}
