namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record PlatformBillingMetricsDto(
    long MrrMinorUnits,
    string CurrencyCode,
    int ActiveSubscriptions,
    long OutstandingMinorUnits,
    int OutstandingCount,
    long OverdueMinorUnits,
    int OverdueCount);
