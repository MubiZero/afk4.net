namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record UpdatePlanRequest(
    string Name,
    long PriceMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch,
    bool IsActive,
    int SortOrder);
