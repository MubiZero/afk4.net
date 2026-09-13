using System.Globalization;

namespace AFK4.Shared.Contracts.Tariffs;

/// <summary>Пути тарифов, общие для сервера и клиентов — см. <see cref="Install.InstallRoutes"/>.</summary>
public static class TariffRoutes
{
    public static string Tariffs(Guid organizationId, Guid branchId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/organizations/{organizationId:D}/branches/{branchId:D}/tariffs");

    public static string Versions(Guid organizationId, Guid branchId, Guid tariffId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/organizations/{organizationId:D}/branches/{branchId:D}/tariffs/{tariffId:D}/versions");
}
