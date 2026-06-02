namespace AFK4.Shared.Contracts.Sessions;

public sealed record StartGuestSessionRequest(
    Guid OrganizationId,
    Guid SeatId,
    string TariffRuleVersionId,
    string IdempotencyKey,
    string DurationMode = SessionDurationModes.Open,
    int? DurationMinutes = null,
    Guid? PlayerAccountId = null,
    string BillingMode = "",
    Guid? TariffVersionId = null,
    Guid? PlayerPackageId = null,
    // Anti-fraud §5.4: an explicit comp (free session). Requires a reason; routes to a session.comp audit.
    bool IsComp = false,
    string? CompReason = null);
