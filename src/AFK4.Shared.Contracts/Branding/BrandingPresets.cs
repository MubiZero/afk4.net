namespace AFK4.Shared.Contracts.Branding;

/// <summary>
/// Готовые эмблемы для клуба, у которого своего логотипа ещё нет. Лежат картинками на платформе:
/// <c>LogoUrl</c> потребляют приложение игрока и оболочка игрового ПК, и им нужен настоящий адрес,
/// а не внутренний идентификатор пресета.
/// </summary>
public static class BrandingPresets
{
    public static readonly IReadOnlyList<string> Ids =
        ["bolt", "flame", "rocket", "crown", "shield", "star", "target", "hexagon", "cube", "joystick"];

    public static string Url(Uri platformBaseUrl, string presetId) =>
        new Uri(platformBaseUrl, $"/branding/presets/{presetId}.svg").ToString();
}
