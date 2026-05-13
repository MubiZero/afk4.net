namespace AFK4.Shared.Contracts.Pos;

public sealed record VoidPosSaleRequest(
    Guid OrganizationId,
    string Reason,
    string IdempotencyKey);
