using System.Globalization;

namespace AFK4.Shared.Contracts.Branding;

/// <summary>
/// Путь оформления, общий для сервера и клиентов — по тем же причинам, что и
/// <see cref="Install.InstallRoutes"/>: строка, продублированная в двух местах, однажды уже
/// развела мастер и API по разным адресам.
/// </summary>
public static class BrandingRoutes
{
    public static string Organization(Guid organizationId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/organizations/{organizationId:D}/branding");
}
