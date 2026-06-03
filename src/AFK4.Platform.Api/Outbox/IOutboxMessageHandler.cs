using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Outbox;

/// <summary>
/// Performs the downstream effect of one committed money command. Implementations are keyed by
/// <see cref="Type"/> (matching <see cref="OutboxMessageEntity.Type"/>) and MUST be idempotent: a row
/// may be redelivered after a crash, so handling the same <see cref="OutboxMessageEntity.IdempotencyKey"/>
/// twice must collapse to a single effect.
/// </summary>
public interface IOutboxMessageHandler
{
    string Type { get; }

    Task<OutboxHandlerResult> HandleAsync(OutboxMessageEntity message, CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of dispatching one outbox row. <c>Retryable</c> transient failures go back to
/// <see cref="OutboxMessageStatus.Pending"/> with backoff; non-retryable failures (or exhausted
/// attempts) go to <see cref="OutboxMessageStatus.Failed"/>.
/// </summary>
public sealed record OutboxHandlerResult(bool Success, bool Retryable, string? Error)
{
    public static OutboxHandlerResult Ok() => new(Success: true, Retryable: false, Error: null);

    public static OutboxHandlerResult TransientFailure(string error) => new(Success: false, Retryable: true, Error: error);

    public static OutboxHandlerResult PermanentFailure(string error) => new(Success: false, Retryable: false, Error: error);
}
