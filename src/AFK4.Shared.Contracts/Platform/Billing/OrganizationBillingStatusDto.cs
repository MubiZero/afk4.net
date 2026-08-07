namespace AFK4.Shared.Contracts.Platform.Billing;

/// <summary>Compact arrears summary for the club's own admin banner: enough to say what is owed and
/// how late it is, without pulling the whole invoice list on every screen load.</summary>
public sealed record OrganizationBillingStatusDto(
    bool InArrears,
    long OutstandingMinorUnits,
    string CurrencyCode,
    int? OldestOverdueInvoiceNumber,
    int DaysOverdue,
    DateTimeOffset? GraceUntilUtc);
