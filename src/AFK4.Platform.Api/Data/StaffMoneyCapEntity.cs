namespace AFK4.Platform.Api.Data;

/// <summary>
/// Per-(branch, role, action-scope) money-action cap override (anti-fraud spec §5.3). When absent,
/// the code-level D4/D5 defaults in <c>StaffMoneyCapPolicy</c> apply; a row overrides them for that
/// role/scope. A null amount means unlimited. Editable by Owner/BranchManager via branch settings.
/// </summary>
public sealed class StaffMoneyCapEntity
{
    public Guid StaffMoneyCapId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string RoleName { get; set; } = string.Empty;

    // MoneyActionScopes.* — currently always "high_risk" (refund + correction + write-off share it).
    public string ActionScope { get; set; } = string.Empty;

    public long? PerTransactionMinorUnits { get; set; }

    public long? DailyMinorUnits { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
