using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>Пробуждение по сети: кнопкой «Разбудить» в Панели и по расписанию ПК клуба.</summary>
public static class DeviceWake
{
    public readonly record struct WakePlan(
        Guid HelperDeviceId,
        CreateDeviceCommandRequest? Command,
        (string Code, string Message)? Error);

    /// <summary>
    /// Кто разбудит спящий ПК: включённый сосед той же подсети, недавно подававший сердцебиение.
    /// Волшебный пакет не проходит маршрутизаторы — сосед из другой подсети не разбудит никого.
    /// </summary>
    public static async Task<WakePlan> PlanAsync(
        PlatformDbContext dbContext,
        DeviceEntity target,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(target.NetworkMacAddress) || string.IsNullOrWhiteSpace(target.NetworkSubnet))
        {
            return new WakePlan(Guid.Empty, null, (DeviceCommandErrorCodeNames.WakeTargetUnknown,
                "This PC has never reported its network address, so it cannot be woken."));
        }

        var aliveSince = timeProvider.GetUtcNow().AddMinutes(-1);
        var helper = await dbContext.Devices
            .AsNoTracking()
            .Where(candidate => candidate.OrganizationId == target.OrganizationId
                && candidate.BranchId == target.BranchId
                && candidate.DeviceId != target.DeviceId
                && candidate.EnrollmentState == DeviceEnrollmentStateNames.Approved
                && candidate.NetworkSubnet == target.NetworkSubnet
                && candidate.LastHeartbeatAtUtc != null
                && candidate.LastHeartbeatAtUtc >= aliveSince)
            .OrderByDescending(candidate => candidate.LastHeartbeatAtUtc)
            .Select(candidate => candidate.DeviceId)
            .FirstOrDefaultAsync(cancellationToken);
        if (helper == Guid.Empty)
        {
            return new WakePlan(Guid.Empty, null, (DeviceCommandErrorCodeNames.NoWakeHelper,
                "No powered-on PC in the same network can wake this one."));
        }

        return new WakePlan(
            helper,
            new CreateDeviceCommandRequest(
                DeviceCommandTypeNames.WakeNeighbor,
                new Dictionary<string, string>
                {
                    ["mac"] = target.NetworkMacAddress!,
                    ["broadcast"] = target.NetworkBroadcastAddress ?? string.Empty,
                    ["targetDeviceId"] = target.DeviceId.ToString("D")
                }),
            null);
    }
}
