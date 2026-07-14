using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Reservations;

public sealed record StartReservationSessionRequest(
    Guid OrganizationId,
    int ExpectedVersion,
    string TariffRuleVersionId,
    string IdempotencyKey,
    string DurationMode = SessionDurationModes.Open,
    int? DurationMinutes = null,
    string BillingMode = "",
    Guid? TariffVersionId = null,
    Guid? PlayerPackageId = null,
    bool IsComp = false,
    string? CompReason = null);
