namespace AFK4.Localization;

/// <summary>
/// Ambient access to the application's active <see cref="ILocalizationService"/>.
/// A XAML markup extension cannot take constructor dependencies, so each app sets
/// <see cref="Current"/> once at startup and the <c>{loc:T}</c> extension reads it
/// when it provides its value.
/// </summary>
public static class LocalizationScope
{
    public static ILocalizationService? Current { get; set; }
}
