using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Notifications;

public sealed class SmsChannel(ISmsTransport transport) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Sms;

    public async Task<ChannelResult> SendAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.RecipientAddress))
        {
            return ChannelResult.PermanentFailure("SMS recipient phone number is missing.");
        }

        if (string.IsNullOrWhiteSpace(row.BodyText))
        {
            return ChannelResult.PermanentFailure("SMS body is empty.");
        }

        try
        {
            await transport.SendAsync(new SmsMessage(row.RecipientAddress, row.BodyText), cancellationToken);
            return ChannelResult.Sent();
        }
        catch (SmsTransportException exception)
        {
            return exception.IsPermanent
                ? ChannelResult.PermanentFailure(exception.Message)
                : ChannelResult.TransientFailure(exception.Message);
        }
        catch (Exception exception)
        {
            return ChannelResult.TransientFailure(exception.Message);
        }
    }
}
