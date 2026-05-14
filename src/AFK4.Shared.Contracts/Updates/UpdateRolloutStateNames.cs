namespace AFK4.Shared.Contracts.Updates;

public static class UpdateRolloutStateNames
{
    public const string Draft = "draft";

    public const string Active = "active";

    public const string Paused = "paused";

    public const string Completed = "completed";

    public const string RollbackRequested = "rollback-requested";

    public const string RolledBack = "rolled-back";

    public const string Cancelled = "cancelled";
}
