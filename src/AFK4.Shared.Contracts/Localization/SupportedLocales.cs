namespace AFK4.Shared.Contracts.Localization;

/// <summary>
/// Языки, на которых система разговаривает с человеком. Список один на весь продукт: язык, который
/// можно выбрать в профиле, но нельзя отрендерить в SMS, — это молчаливая подмена выбора на русский.
/// </summary>
public static class SupportedLocales
{
    public const string Russian = "ru";
    public const string English = "en";
    public const string Tajik = "tg";

    public static readonly IReadOnlyList<string> All = [Russian, English, Tajik];

    public static bool IsSupported(string? locale) =>
        locale is not null && All.Contains(locale, StringComparer.Ordinal);
}
