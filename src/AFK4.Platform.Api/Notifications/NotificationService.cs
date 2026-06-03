using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Default <see cref="INotificationService"/>. Resolves the recipient's effective locale (D12),
/// renders the template once, and writes one idempotent outbox row per chosen channel (D2/D3). A
/// channel with no usable address yields a <see cref="NotificationOutboxStatus.Suppressed"/> row
/// (recorded for audit, never delivered) rather than a silent drop.
/// </summary>
public sealed class NotificationService(
    INotificationOutbox outbox,
    ITemplateProvider templateProvider,
    INotificationRenderer renderer,
    INotificationPreferenceService preferences,
    NotificationDispatchRunner dispatchRunner,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options) : INotificationService
{
    private static readonly IReadOnlyList<NotificationChannel> DefaultChannels = [NotificationChannel.Email];

    private readonly NotificationOptions options = options.Value;

    public async Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var locale = ResolveLocale(request.Recipient.Locale);
        var template = templateProvider.Get(request.TemplateKey, locale);
        var rendered = renderer.Render(template, request.Tokens);
        var now = timeProvider.GetUtcNow();

        var channels = request.PreferredChannels is { Count: > 0 } preferred ? preferred : DefaultChannels;

        var ids = new List<Guid>(channels.Count);
        var anyCreated = false;

        foreach (var channel in channels)
        {
            var (address, addressSuppressionReason) = ResolveAddress(channel, request.Recipient);

            var suppressionReason = addressSuppressionReason;
            if (suppressionReason is null
                && await preferences.IsSuppressedAsync(request.Recipient, request.Category, channel, cancellationToken))
            {
                suppressionReason = $"Recipient opted out of {request.Category} notifications on this channel.";
            }

            var row = new NotificationOutboxEntity
            {
                NotificationOutboxId = Guid.NewGuid(),
                IdempotencyKey = $"{request.IdempotencyKey}:{channel.ToString().ToLowerInvariant()}",
                Channel = channel.ToString(),
                Category = request.Category.ToString(),
                TemplateKey = request.TemplateKey,
                Locale = locale,
                RecipientAddress = address ?? string.Empty,
                StaffUserId = request.Recipient.StaffUserId,
                PlayerAccountId = request.Recipient.PlayerAccountId,
                OrganizationId = request.OrganizationId,
                BranchId = request.BranchId,
                Subject = rendered.Subject,
                BodyText = rendered.BodyText,
                BodyHtml = rendered.BodyHtml,
                Status = suppressionReason is null ? NotificationOutboxStatus.Pending : NotificationOutboxStatus.Suppressed,
                LastError = suppressionReason,
                AttemptCount = 0,
                NextAttemptUtc = now,
                CreatedUtc = now,
            };

            if (suppressionReason is null && request.Attachments is { Count: > 0 } attachments)
            {
                foreach (var attachment in attachments)
                {
                    row.Attachments.Add(new NotificationOutboxAttachmentEntity
                    {
                        NotificationOutboxAttachmentId = Guid.NewGuid(),
                        FileName = attachment.FileName,
                        ContentType = attachment.ContentType,
                        Content = attachment.Content,
                    });
                }
            }

            var result = await outbox.AddIfAbsentAsync(row, cancellationToken);
            ids.Add(result.Row.NotificationOutboxId);
            anyCreated |= result.Created;
        }

        return new NotificationHandle(ids, anyCreated);
    }

    public async Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        var handle = await SendAsync(request, cancellationToken);

        var rows = await outbox.GetByIdsAsync(handle.OutboxIds, cancellationToken);
        foreach (var row in rows.Where(row => row.Status == NotificationOutboxStatus.Pending))
        {
            await dispatchRunner.DispatchAsync(row, cancellationToken);
        }

        await outbox.SaveAsync(cancellationToken);

        var refreshed = await outbox.GetByIdsAsync(handle.OutboxIds, cancellationToken);
        var delivered = refreshed.Count > 0 && refreshed.All(row => row.Status == NotificationOutboxStatus.Sent);
        var error = refreshed.FirstOrDefault(row => row.Status != NotificationOutboxStatus.Sent)?.LastError;

        return new NotificationDeliveryResult(handle, delivered, error);
    }

    private string ResolveLocale(string? recipientLocale) =>
        string.IsNullOrWhiteSpace(recipientLocale) ? options.DefaultLocale : recipientLocale;

    private static (string? Address, string? SuppressionReason) ResolveAddress(NotificationChannel channel, NotificationRecipient recipient) =>
        channel switch
        {
            NotificationChannel.Email => string.IsNullOrWhiteSpace(recipient.EmailAddress)
                ? (null, "No email address on file for the recipient.")
                : (recipient.EmailAddress, null),
            NotificationChannel.Sms => string.IsNullOrWhiteSpace(recipient.PhoneNumber)
                ? (null, "No phone number on file for the recipient.")
                : (recipient.PhoneNumber, null),
            // In-app uses the linkage ids rather than an address; no address suppression in Stage 1.
            NotificationChannel.InApp => (null, null),
            _ => (null, $"Unsupported channel '{channel}'."),
        };
}
