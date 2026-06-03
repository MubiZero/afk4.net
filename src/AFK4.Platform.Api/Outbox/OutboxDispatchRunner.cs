using AFK4.Platform.Api.Data;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Outbox;

/// <summary>
/// Testable core of the billing outbox: claim due <see cref="OutboxMessageStatus.Pending"/> rows,
/// route each to its <see cref="IOutboxMessageHandler"/>, and record the result with capped
/// exponential backoff. Success -> <see cref="OutboxMessageStatus.Dispatched"/>; a permanent failure
/// (or hitting <see cref="OutboxOptions.MaxAttempts"/>) -> <see cref="OutboxMessageStatus.Failed"/>;
/// a transient failure -> back to <see cref="OutboxMessageStatus.Pending"/> with
/// <see cref="OutboxMessageEntity.AvailableAtUtc"/> advanced. The hosted <c>OutboxDispatcher</c>
/// simply ticks this on a schedule.
/// </summary>
public sealed class OutboxDispatchRunner(
    IBillingOutbox outbox,
    IEnumerable<IOutboxMessageHandler> handlers,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options)
{
    private readonly OutboxOptions options = options.Value;
    private readonly Dictionary<string, IOutboxMessageHandler> handlersByType =
        handlers.ToDictionary(handler => handler.Type, StringComparer.Ordinal);

    /// <summary>Claim up to <paramref name="max"/> due rows, dispatch each, persist, and return how many were processed.</summary>
    public async Task<int> RunAsync(int max, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var due = await outbox.ClaimDueAsync(now, max, cancellationToken);
        foreach (var row in due)
        {
            await DispatchAsync(row, cancellationToken);
        }

        await outbox.SaveAsync(cancellationToken);
        return due.Count;
    }

    /// <summary>Dispatch a single tracked row, mutating its status/attempt fields in place (caller persists).</summary>
    public async Task DispatchAsync(OutboxMessageEntity message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var now = timeProvider.GetUtcNow();

        if (!handlersByType.TryGetValue(message.Type, out var handler))
        {
            message.Status = OutboxMessageStatus.Failed;
            message.LastError = $"No outbox handler registered for type '{message.Type}'.";
            return;
        }

        message.AttemptCount++;

        OutboxHandlerResult result;
        try
        {
            result = await handler.HandleAsync(message, cancellationToken);
        }
        catch (Exception exception)
        {
            result = OutboxHandlerResult.TransientFailure(exception.Message);
        }

        if (result.Success)
        {
            message.Status = OutboxMessageStatus.Dispatched;
            message.DispatchedAtUtc = now;
            message.LastError = null;
            return;
        }

        message.LastError = result.Error;

        if (!result.Retryable || message.AttemptCount >= options.MaxAttempts)
        {
            message.Status = OutboxMessageStatus.Failed;
            return;
        }

        message.Status = OutboxMessageStatus.Pending;
        message.AvailableAtUtc = now + NextBackoff(message.AttemptCount);
    }

    private TimeSpan NextBackoff(int attemptCount)
    {
        var schedule = options.BackoffSchedule;
        if (schedule.Count == 0)
        {
            return TimeSpan.FromMinutes(1);
        }

        var index = Math.Min(attemptCount - 1, schedule.Count - 1);
        return schedule[index];
    }
}
