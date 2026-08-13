namespace AFK4.Platform.Api.Data;

public sealed class BranchEntity
{
    public Guid BranchId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    // Club-profile fields shown on the operator "Клуб" screen; all optional, existing branches
    // are unaffected until an operator fills them in.
    public string? Description { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Telegram { get; set; }

    public string? Website { get; set; }

    public string? Instagram { get; set; }

    public string? LogoUrl { get; set; }

    public Guid? LogoMediaId { get; set; }

    /// <summary>
    /// Photo of the hall shown on the club card in the player app's picker. A club is chosen by
    /// eye before it is chosen by name, and a logo on a coloured square says nothing about the room.
    /// </summary>
    public string? CoverImageUrl { get; set; }

    public Guid? CoverMediaId { get; set; }

    // Where the club physically is, for the map in the player app's picker. Nullable because an
    // existing branch has no coordinates until its owner drops a pin: a club without them stays in
    // the list, it just has nothing to show on the map.
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? WorkingHoursJson { get; set; }

    public bool RequireManualDeviceApproval { get; set; }

    /// <summary>
    /// BCP-47-ish locale ("ru" | "en" | "tg") that drives the customer-facing shell language
    /// for this branch and the default locale for its receipts and notifications.
    /// </summary>
    public string PreferredLocale { get; set; } = "ru";

    /// <summary>
    /// IANA timezone identifier ("Asia/Dushanbe" by default) for this branch's wall-clock — the basis
    /// for branch-local day boundaries (reports, schedules) and operator-entered times. Today every
    /// branch is single-region (UTC+5, no DST) so this is just a marker; it makes a future multi-region
    /// rollout a code change rather than a guess about how existing rows were stored.
    /// </summary>
    public string PreferredTimeZone { get; set; } = "Asia/Dushanbe";

    public TimeOnly OrganizationAdminMaintenanceWindowStart { get; set; } = new(4, 0);

    public TimeOnly OrganizationAdminMaintenanceWindowEnd { get; set; } = new(5, 0);

    // Per-branch default ceiling for an open postpaid tab; null means unbounded.
    public long? PostpaidCreditLimitMinorUnits { get; set; }

    // Per-branch offline grace window (minutes) overriding the global SessionLeaseOptions default;
    // null means use the global default. Resolved + clamped to [1,120] by GraceLeasePolicy.
    public int? GraceLeaseMinutes { get; set; }

    // Anti-fraud money-control settings (anti-fraud spec §6). All nullable overrides resolved at the
    // read boundary by MoneyControlPolicy; null means use the global default.

    // D3: refund / manual-correction / debt-write-off above this amount needs a second pair of eyes.
    public long? HighRiskApprovalThresholdMinorUnits { get; set; }

    // D7: a comp valued above this needs the same approval; null reuses the high-risk threshold.
    public long? CompApprovalThresholdMinorUnits { get; set; }

    // D6: shift close with |discrepancy| above this needs manager sign-off.
    public long? ShiftDiscrepancyToleranceMinorUnits { get; set; }

    // D8: optional refund-reason whitelist. JSON array of allowed reason codes; off unless enabled.
    public string? RefundReasonWhitelistJson { get; set; }

    public bool RefundReasonWhitelistEnabled { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
