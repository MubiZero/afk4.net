using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Pos;

public sealed record SettlePosSaleRequest(
    Guid OrganizationId,
    IReadOnlyList<PaymentPartDto> Payments,
    string Note,
    string IdempotencyKey);
