using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Email <see cref="INotificationChannel"/> over the configured SMTP server (D8). Builds the rendered
/// outbox snapshot into an <see cref="SmtpMessage"/> and delegates to <see cref="ISmtpTransport"/>,
/// mapping the transport's classified outcome to a <see cref="ChannelResult"/>: permanent failures
/// fail fast, transient and unexpected failures retry.
/// </summary>
public sealed class SmtpEmailChannel(ISmtpTransport transport, IOptions<NotificationOptions> options) : INotificationChannel
{
    private readonly NotificationOptions options = options.Value;

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<ChannelResult> SendAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (string.IsNullOrWhiteSpace(row.RecipientAddress))
        {
            return ChannelResult.PermanentFailure("No recipient email address on the outbox row.");
        }

        if (string.IsNullOrWhiteSpace(options.FromAddress))
        {
            return ChannelResult.PermanentFailure("Notification SMTP FromAddress is not configured.");
        }

        var attachments = row.Attachments.Count == 0
            ? null
            : row.Attachments
                .Select(attachment => new SmtpAttachment(attachment.FileName, attachment.ContentType, attachment.Content))
                .ToList();

        var message = new SmtpMessage(
            FromAddress: options.FromAddress,
            FromName: options.FromName,
            ToAddress: row.RecipientAddress,
            Subject: row.Subject,
            BodyText: row.BodyText,
            BodyHtml: row.BodyHtml,
            Attachments: attachments);

        try
        {
            await transport.SendAsync(message, cancellationToken);
            return ChannelResult.Sent();
        }
        catch (SmtpTransportException exception)
        {
            return exception.IsPermanent
                ? ChannelResult.PermanentFailure(exception.Message)
                : ChannelResult.TransientFailure(exception.Message);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Unexpected errors are treated as transient so the dispatcher retries rather than dropping.
            return ChannelResult.TransientFailure(exception.Message);
        }
    }
}
