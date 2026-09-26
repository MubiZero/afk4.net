namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>
/// Машинные имена лимитов тарифа и код отказа. Фразу для человека собирает клиент —
/// сервер отдаёт только код и числа.
/// </summary>
public static class PlanLimitNames
{
    public const string ReachedCode = "plan_limit_reached";

    /// <summary>
    /// Отказ запустить сессию на ПК «вне тарифа» — сверх предела ПК бесплатного тарифа (спека
    /// тарифов клуба, §5a). Числа — в <see cref="PlanLimitExceededDto"/> с пределом <see cref="Devices"/>.
    /// </summary>
    public const string DeviceOutsidePlanCode = "device_outside_plan";

    public const string Branches = "branches";

    public const string DevicesPerBranch = "devices_per_branch";

    /// <summary>Игровые ПК на весь клуб, без деления по залам.</summary>
    public const string Devices = "devices";

    public const string ConcurrentSessions = "concurrent_sessions";

    public const string StaffUsersPerBranch = "staff_users_per_branch";
}
