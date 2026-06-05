namespace AFK4.Platform.Api.Notifications;

public interface ISmsTransport
{
    Task SendAsync(SmsMessage message, CancellationToken cancellationToken);
}

public sealed record SmsMessage(string ToPhoneNumber, string Text);

public sealed class SmsTransportException(bool isPermanent, string message) : Exception(message)
{
    public bool IsPermanent { get; } = isPermanent;
}
