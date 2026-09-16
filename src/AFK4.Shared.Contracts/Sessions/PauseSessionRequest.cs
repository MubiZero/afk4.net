namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Пауза сессии: счётчик времени встаёт, ПК запирается, место остаётся за игроком. Стоять на
/// паузе бесконечно нельзя — филиал задаёт предел, после которого сессия закрывается сама.
/// </summary>
public sealed record PauseSessionRequest(
    string Reason,
    string IdempotencyKey,
    int? ExpectedVersion = null);

/// <summary>Снятие паузы: ПК отпирается, а конец фиксированной сессии сдвигается на простой.</summary>
public sealed record ResumeSessionRequest(
    string Reason,
    string IdempotencyKey,
    int? ExpectedVersion = null);
