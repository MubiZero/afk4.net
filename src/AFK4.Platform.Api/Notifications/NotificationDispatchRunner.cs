using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// The testable core of notification delivery: claim due outbox rows, send each via its channel,
/// and record the result with capped exponential backoff (D11). Success -> <c>Sent</c>; a permanent
/// failure (or hitting <see cref="NotificationOptions.MaxAttempts"/>) -> <c>Failed</c>; a transient
/// failure -> back to <c>Pending</c> with <c>NextAttemptUtc</c> advanced. The hosted
/// <see cref="NotificationDispatcher"/> simply ticks this on a schedule.
/// </summary>
public sealed class NotificationDispatchRunner(
    EfNotificationOutbox outbox,
    IEnumerable<INotificationChannel> channels,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options)
{
    private readonly NotificationOptions options = options.Value;
    private readonly Dictionary<string, INotificationChannel> channelsByName =
        channels.ToDictionary(channel => channel.Channel.ToString(), StringComparer.Ordinal);

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
    public async Task DispatchAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var now = timeProvider.GetUtcNow();

        if (!channelsByName.TryGetValue(row.Channel, out var channel))
        {
            row.Status = NotificationOutboxStatus.Failed;
            row.FailedUtc = now;
            row.LastError = $"No channel registered for '{row.Channel}'.";
            return;
        }

        row.AttemptCount++;

        ChannelResult result;
        try
        {
            result = await channel.SendAsync(row, cancellationToken);
        }
        catch (Exception exception)
        {
            result = ChannelResult.TransientFailure(exception.Message);
        }

        if (result.Success)
        {
            row.Status = NotificationOutboxStatus.Sent;
            row.SentUtc = now;
            row.LastError = null;
            ScrubSmsSecrets(row);
            return;
        }

        row.LastError = result.Error;

        if (!result.Retryable || row.AttemptCount >= options.MaxAttempts)
        {
            row.Status = NotificationOutboxStatus.Failed;
            row.FailedUtc = now;
            ScrubSmsSecrets(row);
            return;
        }

        row.Status = NotificationOutboxStatus.Pending;
        row.NextAttemptUtc = now + NextBackoff(row.AttemptCount);
    }

    /// <summary>
    /// Код из SMS нужен очереди только пока сообщение ещё надо отправить: повтор берёт значения из
    /// строки. Отправленный или окончательно проваленный код лежал бы в базе открытым текстом —
    /// в значениях и в отрисованном тексте — и читался бы любым, у кого есть доступ к таблице.
    /// </summary>
    internal static void ScrubSmsSecrets(NotificationOutboxEntity row)
    {
        if (row.Channel != NotificationChannel.Sms.ToString())
        {
            return;
        }

        row.TokensJson = null;
        row.BodyText = string.Empty;
        row.BodyHtml = string.Empty;
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
