namespace AFK4.Shared.Contracts.Updates;

public static class LocalUpdateCoordinationOperations
{
    public const string QueryState = "query-state";
    public const string RequestShutdown = "request-shutdown";
}

public static class LocalUpdateCoordinationStatuses
{
    public const string NotRunning = "not-running";
    public const string Idle = "idle";
    public const string CriticalCommandActive = "critical-command-active";
    public const string ShutdownAcknowledged = "shutdown-acknowledged";
    public const string Rejected = "rejected";
}

public sealed record LocalUpdateCoordinationRequest(
    string Secret,
    string Operation,
    Guid? UpdateRolloutId,
    Guid? UpdatePackageId);

public sealed record LocalUpdateCoordinationResponse(string Status, string Message);
