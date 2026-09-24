namespace AFK4.Shared.Contracts.Shell;

/// <summary>
/// Один кадр канала агент ↔ хост. Заполнено ровно то поле, которое называет <see cref="Type"/>:
/// так кадр читается одним типом, без второго разбора по виду сообщения.
/// </summary>
public sealed record ShellPipeMessage(
    // Одно из ShellPipeMessageTypeNames.
    string Type,
    ShellPipeHelloDto? Hello = null,
    PlayerShellStateDto? State = null,
    ShellPipeRequestDto? Request = null,
    ShellPipeReplyDto? Reply = null,
    // Почему агент попрощался; только у bye.
    string? Reason = null);

public sealed record ShellPipeHelloDto(
    int Protocol,
    string HostVersion,
    // Сессия Windows, в которой живёт хост. Агент сверяет её с консольной: хост из чужой
    // сессии получать состояние этого ПК не должен.
    int SessionId);

public sealed record ShellPipeRequestDto(
    Guid RequestId,
    // Одно из ShellPipeRequestTypeNames.
    string Type,
    IReadOnlyDictionary<string, string> Payload);

public sealed record ShellPipeReplyDto(
    Guid RequestId,
    bool Ok,
    // Одно из ShellPipeErrorCodeNames; пусто при успехе.
    string? ErrorCode = null,
    string? Message = null);
