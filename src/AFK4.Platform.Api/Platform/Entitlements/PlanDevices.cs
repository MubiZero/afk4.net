using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Какие игровые ПК клуба работают при пределе ПК на клуб (спека тарифов клуба, §5a). Сначала те,
/// что отметил владелец, потом подключённые раньше других — так выбор не меняется от запроса к
/// запросу. Остальные — «вне тарифа»: новые сессии на них не запускаются.
/// </summary>
public static class PlanDevices
{
    public sealed record Allowance(int? Limit, int Count, IReadOnlySet<Guid> Outside)
    {
        public static readonly Allowance Unlimited = new(null, 0, new HashSet<Guid>());
    }

    public static async Task<Allowance> ForOrganizationAsync(PlatformDbContext db, Guid organizationId, CancellationToken ct)
    {
        var limitsJson = await db.Organizations.AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => organization.LimitsJson)
            .SingleOrDefaultAsync(ct);
        if (OrganizationLimitsJson.Deserialize(limitsJson).MaxDevices is not { } limit) return Allowance.Unlimited;

        var devices = await Ordered(db, organizationId).Select(device => device.DeviceId).ToListAsync(ct);
        return new Allowance(limit, devices.Count, devices.Skip(limit).ToHashSet());
    }

    /// <summary>Подтверждённые игровые ПК в порядке, в котором им достаются места в тарифе.</summary>
    public static IQueryable<DeviceEntity> Ordered(PlatformDbContext db, Guid organizationId) =>
        db.Devices.AsNoTracking()
            .Where(device => device.OrganizationId == organizationId
                && device.Role == DeviceRoleNames.GamingPc
                && device.EnrollmentState == DeviceEnrollmentStateNames.Approved)
            .OrderByDescending(device => device.KeptOnFreePlan)
            .ThenBy(device => device.EnrolledAtUtc)
            .ThenBy(device => device.DeviceId);
}
