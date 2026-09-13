namespace AFK4.Shared.Contracts.Branding;

/// <summary>
/// Оформление клуба: логотип и цвет. Поля у организации были с самого начала и читались публичной
/// витриной и приложением игрока, но записать их было нечем — единственное присвоение жило в сидере
/// для разработки.
/// </summary>
public sealed record UpdateOrganizationBrandingRequest(string? LogoUrl, string? AccentColor);
