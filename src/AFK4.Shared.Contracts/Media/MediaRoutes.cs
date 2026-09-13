using System.Globalization;

namespace AFK4.Shared.Contracts.Media;

/// <summary>Путь загрузки медиа, общий для сервера и клиентов — см. <see cref="Install.InstallRoutes"/>.</summary>
public static class MediaRoutes
{
    public static string BranchMedia(Guid organizationId, Guid branchId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/organizations/{organizationId:D}/branches/{branchId:D}/media");
}
