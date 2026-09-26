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
    int SortOrder,
    // Не переданы — остаются прежними: старый редактор тарифов о них не знает.
    long? PricePerDeviceMinorUnits = null,
    int? IncludedDevices = null,
    // Не передан — остаётся прежним; снять предел — RemoveMaxDevices.
    int? MaxDevices = null,
    bool RemoveMaxDevices = false,
    // Передан — заменяет набор функций тарифа целиком.
    IReadOnlyList<string>? IncludedFeatures = null,
    // Клубы на этом тарифе получают его новые лимиты. Без отметки лимиты клубов не меняются:
    // платформа могла задать клубу свои.
    bool ApplyLimitsToClubs = false);
