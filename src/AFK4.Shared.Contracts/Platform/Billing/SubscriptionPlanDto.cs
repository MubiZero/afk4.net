namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record SubscriptionPlanDto(
    string PlanCode,
    string Name,
    long PriceMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch,
    bool IsActive,
    int SortOrder,
    // Цена каждого ПК сверх включённых — у тарифа за ПК; у прочих ноль.
    long PricePerDeviceMinorUnits = 0,
    int IncludedDevices = 0);
