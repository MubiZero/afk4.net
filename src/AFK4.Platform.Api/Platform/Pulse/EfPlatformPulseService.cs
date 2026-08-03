using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Pulse;
using AFK4.Shared.Contracts.Platform.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Pulse;

// Computes the whole-fleet pulse in a fixed, small number of queries (one per entity kind),
// grouping in memory afterwards — never one query per club/organization. The alert rules
// (silent agent / stale shift / overdue payment / failed rollout) live here, server-side, so
// every client renders the same verdict instead of re-deriving alerts from raw counts.
public sealed class EfPlatformPulseService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<PlatformPulseOptions> pulseOptions) : IPlatformPulseService
{
    private readonly PlatformPulseOptions options = pulseOptions.Value;

    public async Task<PlatformPulseDto> GetPulseAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var onlineThreshold = now.AddMinutes(-options.AgentSilenceThresholdMinutes);
        var staleShiftThreshold = now.AddHours(-options.ShiftStaleHours);

        var organizations = await dbContext.Organizations
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var branches = await dbContext.Branches
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var devices = await dbContext.Devices
            .AsNoTracking()
            .Select(device => new { device.BranchId, device.LastHeartbeatAtUtc })
            .ToListAsync(cancellationToken);
        var activeSessions = await dbContext.Sessions
            .AsNoTracking()
            .Where(session => session.StartedAtUtc != null && session.EndedAtUtc == null)
            .Select(session => session.BranchId)
            .ToListAsync(cancellationToken);
        var seats = await dbContext.Seats
            .AsNoTracking()
            .Select(seat => seat.BranchId)
            .ToListAsync(cancellationToken);
        var openShifts = await dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.ClosedAtUtc == null)
            .Select(shift => new { shift.BranchId, shift.OpenedAtUtc })
            .ToListAsync(cancellationToken);
        var overdueInvoices = await dbContext.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatusNames.Overdue)
            .Select(invoice => new { invoice.OrganizationId, invoice.AmountMinorUnits, invoice.CurrencyCode })
            .ToListAsync(cancellationToken);

        // The real signal that a rollout is failing is the agent reporting a failed
        // install per device (DeviceUpdateStatusEntity.Status == Failed) — the rollout's
        // own State never changes when a device fails (see
        // EfUpdateService.ReportStatusAsync), so State alone cannot detect this. We count
        // distinct failed devices per organization across rollouts that are still Active
        // or already flagged RollbackRequested, in one grouped query — no per-organization
        // or per-club round trip.
        var trackedRolloutStates = await dbContext.UpdateRollouts
            .AsNoTracking()
            .Where(rollout => rollout.State == UpdateRolloutStateNames.Active || rollout.State == UpdateRolloutStateNames.RollbackRequested)
            .Select(rollout => new { rollout.UpdateRolloutId, rollout.State })
            .ToListAsync(cancellationToken);
        var trackedRolloutIds = trackedRolloutStates.Select(rollout => rollout.UpdateRolloutId).ToHashSet();

        var failedDeviceCountByOrganization = new Dictionary<Guid, int>();
        if (trackedRolloutIds.Count > 0)
        {
            var failedDeviceReports = await dbContext.DeviceUpdateStatuses
                .AsNoTracking()
                .Where(status => status.Status == UpdateStatusNames.Failed && trackedRolloutIds.Contains(status.UpdateRolloutId))
                .Select(status => new { status.OrganizationId, status.DeviceId })
                .Distinct()
                .ToListAsync(cancellationToken);
            failedDeviceCountByOrganization = failedDeviceReports
                .GroupBy(report => report.OrganizationId)
                .ToDictionary(group => group.Key, group => group.Select(report => report.DeviceId).Distinct().Count());
        }

        // RollbackRequested is also kept as a secondary, manual trigger: an admin can flag
        // a rollout as needing rollback (POST /rollouts/{id}/state) before any device has
        // had a chance to report a failure, and that organization should still light up.
        var manuallyFlaggedRolloutIds = trackedRolloutStates
            .Where(rollout => rollout.State == UpdateRolloutStateNames.RollbackRequested)
            .Select(rollout => rollout.UpdateRolloutId)
            .ToHashSet();
        var organizationIdsWithManualRollbackRequest = new HashSet<Guid>();
        if (manuallyFlaggedRolloutIds.Count > 0)
        {
            var manualTargets = await dbContext.UpdateRolloutTargets
                .AsNoTracking()
                .Where(target => manuallyFlaggedRolloutIds.Contains(target.UpdateRolloutId))
                .Select(target => new { target.TargetKind, target.OrganizationId, target.BranchId, target.DeviceId })
                .ToListAsync(cancellationToken);

            foreach (var target in manualTargets)
            {
                if (target.TargetKind == PlatformUpdateTargetKindNames.Organization && target.OrganizationId is { } organizationId)
                    organizationIdsWithManualRollbackRequest.Add(organizationId);
            }

            var manualBranchIds = manualTargets
                .Where(target => target.TargetKind == PlatformUpdateTargetKindNames.Branch && target.BranchId.HasValue)
                .Select(target => target.BranchId!.Value)
                .ToHashSet();
            var manualDeviceIds = manualTargets
                .Where(target => target.TargetKind == PlatformUpdateTargetKindNames.Device && target.DeviceId.HasValue)
                .Select(target => target.DeviceId!.Value)
                .ToHashSet();

            if (manualDeviceIds.Count > 0)
            {
                var deviceOrganizationIds = await dbContext.Devices
                    .AsNoTracking()
                    .Where(device => manualDeviceIds.Contains(device.DeviceId))
                    .Select(device => device.OrganizationId)
                    .ToListAsync(cancellationToken);
                foreach (var organizationId in deviceOrganizationIds) organizationIdsWithManualRollbackRequest.Add(organizationId);
            }

            if (manualBranchIds.Count > 0)
            {
                // Resolved from the branch roster already loaded below, no extra query needed.
                foreach (var branch in branches.Where(branch => manualBranchIds.Contains(branch.BranchId)))
                    organizationIdsWithManualRollbackRequest.Add(branch.OrganizationId);
            }
        }

        var devicesByBranch = devices.GroupBy(device => device.BranchId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var activeSessionCountByBranch = activeSessions
            .GroupBy(branchId => branchId)
            .ToDictionary(group => group.Key, group => group.Count());
        var seatCountByBranch = seats
            .GroupBy(branchId => branchId)
            .ToDictionary(group => group.Key, group => group.Count());
        var openShiftByBranch = openShifts
            .GroupBy(shift => shift.BranchId)
            .ToDictionary(group => group.Key, group => group.OrderBy(shift => shift.OpenedAtUtc).First());
        var overdueByOrganization = overdueInvoices
            .GroupBy(invoice => invoice.OrganizationId)
            .ToDictionary(
                group => group.Key,
                group => (OutstandingMinorUnits: group.Sum(invoice => invoice.AmountMinorUnits), CurrencyCode: group.First().CurrencyCode));
        var branchesByOrganization = branches
            .GroupBy(branch => branch.OrganizationId)
            .ToDictionary(group => group.Key, group => group.OrderBy(branch => branch.Name, StringComparer.Ordinal).ToList());

        var organizationDtos = new List<PulseOrganizationDto>();
        foreach (var organization in organizations.OrderBy(org => org.Name, StringComparer.Ordinal))
        {
            var clubDtos = new List<PulseClubDto>();
            branchesByOrganization.TryGetValue(organization.OrganizationId, out var organizationBranches);
            foreach (var branch in organizationBranches ?? [])
            {
                devicesByBranch.TryGetValue(branch.BranchId, out var branchDevices);
                branchDevices ??= [];
                var devicesTotal = branchDevices.Count;
                var devicesOnline = branchDevices.Count(device =>
                    device.LastHeartbeatAtUtc is not null && device.LastHeartbeatAtUtc >= onlineThreshold);
                var lastHeartbeatAtUtc = branchDevices
                    .Select(device => device.LastHeartbeatAtUtc)
                    .Where(heartbeat => heartbeat is not null)
                    .DefaultIfEmpty()
                    .Max();

                seatCountByBranch.TryGetValue(branch.BranchId, out var seatsTotal);
                activeSessionCountByBranch.TryGetValue(branch.BranchId, out var seatsOccupied);

                var hasOpenShift = openShiftByBranch.TryGetValue(branch.BranchId, out var openShift);
                var clubAlerts = new List<PulseAlertDto>();

                // A brand-new club with no devices enrolled yet is not "silent" — it simply
                // hasn't started operating, so it must not be flagged red on day one.
                if (devicesTotal > 0 && devicesOnline == 0)
                {
                    var minutesSinceHeartbeat = lastHeartbeatAtUtc is null
                        ? (int?)null
                        : (int)(now - lastHeartbeatAtUtc.Value).TotalMinutes;
                    clubAlerts.Add(new PulseAlertDto(PulseAlertKindNames.AgentSilent, PulseAlertLevelNames.Critical, DetailValue: minutesSinceHeartbeat));
                }

                if (hasOpenShift && openShift!.OpenedAtUtc <= staleShiftThreshold)
                {
                    var minutesOpen = (int)(now - openShift.OpenedAtUtc).TotalMinutes;
                    clubAlerts.Add(new PulseAlertDto(PulseAlertKindNames.ShiftNotClosed, PulseAlertLevelNames.Attention, DetailValue: minutesOpen));
                }

                clubDtos.Add(new PulseClubDto(
                    BranchId: branch.BranchId,
                    Name: branch.Name,
                    City: branch.City,
                    DevicesOnline: devicesOnline,
                    DevicesTotal: devicesTotal,
                    SeatsOccupied: seatsOccupied,
                    SeatsTotal: seatsTotal,
                    ShiftOpen: hasOpenShift,
                    ShiftOpenedAtUtc: hasOpenShift ? openShift!.OpenedAtUtc : null,
                    LastHeartbeatAtUtc: lastHeartbeatAtUtc,
                    Alerts: clubAlerts));
            }

            var organizationAlerts = new List<PulseAlertDto>();
            overdueByOrganization.TryGetValue(organization.OrganizationId, out var overdue);
            var outstandingMinorUnits = overdue.OutstandingMinorUnits;
            var currencyCode = overdue.CurrencyCode ?? "TJS";
            if (outstandingMinorUnits > 0)
            {
                organizationAlerts.Add(new PulseAlertDto(PulseAlertKindNames.PaymentOverdue, PulseAlertLevelNames.Attention, DetailValue: null));
            }

            var hasFailedDeviceReports = failedDeviceCountByOrganization.TryGetValue(organization.OrganizationId, out var failedDeviceCount);
            if (hasFailedDeviceReports || organizationIdsWithManualRollbackRequest.Contains(organization.OrganizationId))
            {
                organizationAlerts.Add(new PulseAlertDto(
                    PulseAlertKindNames.RolloutFailed,
                    PulseAlertLevelNames.Attention,
                    DetailValue: hasFailedDeviceReports ? failedDeviceCount : null));
            }

            var alertLevel = HighestAlertLevel(organizationAlerts, clubDtos);

            organizationDtos.Add(new PulseOrganizationDto(
                OrganizationId: organization.OrganizationId,
                Name: organization.Name,
                Status: organization.Status,
                PlanCode: organization.PlanCode,
                SubscriptionStatus: organization.SubscriptionStatus,
                AlertLevel: alertLevel,
                OutstandingMinorUnits: outstandingMinorUnits,
                CurrencyCode: currencyCode,
                Alerts: organizationAlerts,
                Clubs: clubDtos));
        }

        return new PlatformPulseDto(now, organizationDtos);
    }

    private static string HighestAlertLevel(IReadOnlyList<PulseAlertDto> organizationAlerts, IReadOnlyList<PulseClubDto> clubs)
    {
        var level = PulseAlertLevelNames.Normal;
        foreach (var alert in organizationAlerts)
        {
            level = MaxLevel(level, alert.Level);
        }

        foreach (var club in clubs)
        {
            foreach (var alert in club.Alerts)
            {
                level = MaxLevel(level, alert.Level);
            }
        }

        return level;
    }

    private static string MaxLevel(string a, string b) => Rank(a) >= Rank(b) ? a : b;

    private static int Rank(string level) => level switch
    {
        PulseAlertLevelNames.Critical => 2,
        PulseAlertLevelNames.Attention => 1,
        _ => 0
    };
}
