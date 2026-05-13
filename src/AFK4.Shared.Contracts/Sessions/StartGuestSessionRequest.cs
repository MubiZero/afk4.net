namespace AFK4.Shared.Contracts.Sessions;

public sealed record StartGuestSessionRequest(
    Guid OrganizationId,
    Guid SeatId,
    int DurationMinutes,
    string TariffRuleVersionId,
    string IdempotencyKey,
    Guid? PlayerAccountId = null,
    string BillingMode = "",
    Guid? TariffVersionId = null,
    Guid? PlayerPackageId = null);
