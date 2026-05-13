namespace AFK4.Shared.Contracts.Pos;

public sealed record RefundPosSaleRequest(
    Guid OrganizationId,
    string Reason,
    string IdempotencyKey);
