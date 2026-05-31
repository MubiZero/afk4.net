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
    int SortOrder);
