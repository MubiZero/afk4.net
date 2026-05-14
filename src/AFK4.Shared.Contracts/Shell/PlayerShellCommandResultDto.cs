namespace AFK4.Shared.Contracts.Shell;

public sealed record PlayerShellCommandResultDto(
    Guid CommandId,
    string Status,
    string Message,
    DateTimeOffset ObservedAtUtc);
