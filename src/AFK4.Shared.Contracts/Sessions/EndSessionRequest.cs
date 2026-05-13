namespace AFK4.Shared.Contracts.Sessions;

public sealed record EndSessionRequest(
    string Reason,
    string IdempotencyKey);
