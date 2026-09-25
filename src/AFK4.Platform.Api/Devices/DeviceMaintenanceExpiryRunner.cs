using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Endpoints;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

/// <summary>Настройки автоснятия обслуживания.</summary>
public sealed class DeviceMaintenanceExpiryOptions
{
    /// <summary>Как часто искать забытые ПК. Восемь часов плюс минута никому не повредят.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Возвращает в зал ПК, простоявшие на обслуживании дольше восьми часов, и пишет это в журнал.
/// Агент узнаёт об этом из ближайшего сердцебиения — признак обслуживания пропадёт, и он закроет
/// рабочий стол сам; Панель — из события карты.
/// </summary>
public sealed class DeviceMaintenanceExpiryRunner(
    PlatformDbContext dbContext,
    IAuditRecordWriter auditRecordWriter,
    TimeProvider timeProvider,
    IHubContext<DeviceHub>? hubContext = null)
{
    /// <summary>Один проход. Возвращает число возвращённых в зал ПК.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now - DeviceMaintenance.MaxDuration;
        var expired = await dbContext.Devices
            .Where(device => device.MaintenanceSinceUtc != null && device.MaintenanceSinceUtc <= cutoff)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var device in expired)
        {
            var details = JsonSerializer.Serialize(new
            {
                device.DeviceId,
                device.MaintenanceSinceUtc,
                device.MaintenanceByStaffUserId,
                device.MaintenanceByName,
                Automatic = true
            });
            DeviceMaintenance.Clear(device);
            await dbContext.SaveChangesAsync(cancellationToken);
            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                OrganizationId: device.OrganizationId,
                BranchId: device.BranchId,
                ActorStaffUserId: null,
                Action: AuditActionNames.ExpireDeviceMaintenance,
                TargetType: "Device",
                TargetId: device.DeviceId.ToString("D"),
                Outcome: AuditOutcome.Succeeded,
                SourceApp: "PlatformApi",
                DetailsJson: details),
                cancellationToken);
        }

        if (hubContext is not null)
        {
            await EndpointHelpers.NotifyDeviceChangesAsync(
                hubContext, dbContext, expired.Select(device => device.DeviceId), now, cancellationToken);
        }

        return expired.Count;
    }
}
