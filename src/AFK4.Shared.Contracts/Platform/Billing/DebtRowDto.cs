namespace AFK4.Shared.Contracts.Platform.Billing;

/// <summary>One club that needs a money decision: either it owes money, or it is still suspended
/// after settling. Days overdue and the dunning stage answer "how long has this been ignored".</summary>
public sealed record DebtRowDto(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string OrganizationStatus,
    string SubscriptionStatus,
    long OutstandingMinorUnits,
    string CurrencyCode,
    int? OldestOverdueInvoiceNumber,
    Guid? OldestOverdueInvoiceId,
    int DaysOverdue,
    int DunningStage,
    DateTimeOffset? GraceUntilUtc,
    bool SettledButSuspended);
