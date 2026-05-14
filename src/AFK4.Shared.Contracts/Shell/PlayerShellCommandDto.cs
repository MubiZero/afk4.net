namespace AFK4.Shared.Contracts.Shell;

public sealed record PlayerShellCommandDto(
    Guid CommandId,
    string Type,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyDictionary<string, string> Payload);
