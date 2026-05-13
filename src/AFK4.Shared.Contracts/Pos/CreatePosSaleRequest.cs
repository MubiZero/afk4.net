namespace AFK4.Shared.Contracts.Pos;

public sealed record CreatePosSaleRequest(
    Guid OrganizationId,
    Guid ShiftId,
    IReadOnlyList<PosSaleLineDto> Lines,
    string IdempotencyKey);
