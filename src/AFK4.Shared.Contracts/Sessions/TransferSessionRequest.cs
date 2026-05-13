namespace AFK4.Shared.Contracts.Sessions;

public sealed record TransferSessionRequest(
    Guid TargetSeatId,
    string IdempotencyKey);
