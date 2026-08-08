namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>
/// Машинные имена лимитов тарифа и код отказа. Фразу для человека собирает клиент —
/// сервер отдаёт только код и числа.
/// </summary>
public static class PlanLimitNames
{
    public const string ReachedCode = "plan_limit_reached";

    public const string Branches = "branches";

    public const string DevicesPerBranch = "devices_per_branch";

    public const string ConcurrentSessions = "concurrent_sessions";

    public const string StaffUsersPerBranch = "staff_users_per_branch";
}
