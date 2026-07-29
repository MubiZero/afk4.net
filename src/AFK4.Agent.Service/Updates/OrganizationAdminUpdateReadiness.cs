using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public static class OrganizationAdminUpdateReadinessNames
{
    public const string InstallNow = "install-now";
    public const string DeferredOutsideWindow = "deferred-outside-window";
    public const string DeferredCriticalCommand = "deferred-critical-command";
    public const string ReadyAfterShutdown = "ready-after-shutdown";
}

public sealed record OrganizationAdminUpdateReadinessResult(string Status, string Message)
{
    public bool CanInstall => Status is OrganizationAdminUpdateReadinessNames.InstallNow
        or OrganizationAdminUpdateReadinessNames.ReadyAfterShutdown;
}

public interface IOrganizationAdminUpdateReadiness
{
    Task<OrganizationAdminUpdateReadinessResult> EvaluateAsync(
        ComponentUpdateInstructionDto instruction,
        OrganizationAdminUpdatePreferenceDto? preference,
        DateTimeOffset serverTimeUtc,
        CancellationToken cancellationToken);
}

public sealed class OrganizationAdminUpdateReadiness(
    IOrganizationAdminUpdateCoordinatorClient coordinatorClient,
    IOptions<AgentOptions> options) : IOrganizationAdminUpdateReadiness
{
    public async Task<OrganizationAdminUpdateReadinessResult> EvaluateAsync(
        ComponentUpdateInstructionDto instruction,
        OrganizationAdminUpdatePreferenceDto? preference,
        DateTimeOffset serverTimeUtc,
        CancellationToken cancellationToken)
    {
        var state = await coordinatorClient.QueryStateAsync(cancellationToken);
        if (state.Status == LocalUpdateCoordinationStatuses.NotRunning)
            return new(OrganizationAdminUpdateReadinessNames.InstallNow, "Organization Admin is not running.");
        bool insideWindow;
        try
        {
            insideWindow = preference is not null && IsInsideWindow(preference, serverTimeUtc);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return new(OrganizationAdminUpdateReadinessNames.DeferredOutsideWindow, "Organization Admin maintenance timezone is invalid.");
        }
        if (!insideWindow)
            return new(OrganizationAdminUpdateReadinessNames.DeferredOutsideWindow, "Organization Admin is open outside its maintenance window.");
        if (state.Status == LocalUpdateCoordinationStatuses.CriticalCommandActive)
            return new(OrganizationAdminUpdateReadinessNames.DeferredCriticalCommand, state.Message);

        var shutdown = await coordinatorClient.RequestShutdownAsync(
            instruction.UpdateRolloutId, instruction.UpdatePackageId, cancellationToken);
        if (shutdown.Status != LocalUpdateCoordinationStatuses.ShutdownAcknowledged)
            return new(OrganizationAdminUpdateReadinessNames.DeferredCriticalCommand, shutdown.Message);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(1, options.Value.OrganizationAdminUpdateExitTimeoutSeconds));
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(Math.Max(10, options.Value.OrganizationAdminUpdateExitPollMilliseconds), cancellationToken);
            state = await coordinatorClient.QueryStateAsync(cancellationToken);
            if (state.Status == LocalUpdateCoordinationStatuses.NotRunning)
                return new(OrganizationAdminUpdateReadinessNames.ReadyAfterShutdown, "Organization Admin exited normally.");
        }

        return new(OrganizationAdminUpdateReadinessNames.DeferredCriticalCommand, "Organization Admin did not exit before the safety timeout.");
    }

    public static bool IsInsideWindow(OrganizationAdminUpdatePreferenceDto preference, DateTimeOffset utcNow)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(preference.TimeZone);
        var localTime = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, zone).DateTime);
        var start = preference.MaintenanceWindowStart;
        var end = preference.MaintenanceWindowEnd;
        return start < end
            ? localTime >= start && localTime < end
            : localTime >= start || localTime < end;
    }
}
