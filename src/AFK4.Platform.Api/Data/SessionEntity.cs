namespace AFK4.Platform.Api.Data;

public sealed class SessionEntity
{
    public Guid SessionId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid SeatId { get; set; }

    public Guid DeviceId { get; set; }

    public Guid CreatedByStaffUserId { get; set; }

    public string PlayerKind { get; set; } = "guest";

    public Guid? PlayerAccountId { get; set; }

    public string TariffRuleVersionId { get; set; } = string.Empty;

    // The billing mode the session was started with (guest / prepaid_wallet / postpaid_debt /
    // package). Persisted so an extend that omits the mode inherits it instead of silently
    // defaulting to a free guest top-up. Empty for legacy rows started before this column.
    public string BillingMode { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public DateTimeOffset RequestedAtUtc { get; set; }

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? EndsAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public Guid? CurrentLeaseId { get; set; }

    // Anti-fraud §5.4: a comp (free) session and its would-be charge at the standard tariff.
    // A comp never bills; the value is kept for owner-summary / review visibility and daily-cap counting.
    public bool IsComp { get; set; }

    public long? CompValueMinorUnits { get; set; }

    // Auto-protection bookkeeping so a warning/lock is issued at most once.
    public DateTimeOffset? AutoWarnedAtUtc { get; set; }

    public DateTimeOffset? AutoLockedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    // Optimistic-concurrency token: bumped on every mutation (start sets it to 1). A mutating
    // command may carry the version it last saw; a mismatch loses the race and gets a 409 so two
    // operators can never silently double-act on the same session.
    public int Version { get; set; }
}
