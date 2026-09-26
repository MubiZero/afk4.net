namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record CreatePlanRequest(
    string PlanCode,
    string Name,
    long PriceMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch,
    int SortOrder,
    long PricePerDeviceMinorUnits = 0,
    int IncludedDevices = 0,
    int? MaxDevices = null,
    // Ключи функций, которые тариф включает; пусто — решают значения функций по умолчанию.
    IReadOnlyList<string>? IncludedFeatures = null);
