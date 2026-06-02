namespace AFK4.Shared.Contracts.Pos;

public sealed record CreatePosSaleRequest(
    Guid OrganizationId,
    Guid ShiftId,
    IReadOnlyList<PosSaleLineDto> Lines,
    string IdempotencyKey,
    Guid? PlayerAccountId = null,
    // When set, attaches this sale to an open session tab (settled at checkout).
    Guid? SessionId = null);
