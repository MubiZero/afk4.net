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
    Guid? PlayerPackageId = null);
