using System.Globalization;

namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Пути работы с сотрудниками, общие для сервера и клиентов — по той же причине, что
/// <see cref="Install.InstallRoutes"/>: строка в двух местах однажды уже развела мастер и API.
/// </summary>
public static class StaffRoutes
{
    public static string Invites(Guid organizationId, Guid branchId) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/organizations/{organizationId:D}/branches/{branchId:D}/staff/invites");
}
