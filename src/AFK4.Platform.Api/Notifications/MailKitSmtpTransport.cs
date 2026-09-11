using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// <see cref="ISmtpTransport"/> over MailKit against the founder's configured SMTP server. Maps
/// MailKit's SMTP exceptions to <see cref="SmtpTransportException"/>: a 5xx / rejected-address reply
/// is permanent; protocol hiccups are transient; anything else (timeouts, sockets, auth) propagates
/// and is treated as transient by <see cref="SmtpEmailChannel"/>.
/// </summary>
public sealed class MailKitSmtpTransport(IOptions<NotificationOptions> options) : ISmtpTransport
{
    private readonly NotificationOptions options = options.Value;

    public async Task SendAsync(SmtpMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(message.FromName ?? string.Empty, message.FromAddress));
        mimeMessage.To.Add(MailboxAddress.Parse(message.ToAddress));
        mimeMessage.Subject = message.Subject;
        var bodyBuilder = new BodyBuilder
        {
            TextBody = message.BodyText,
            HtmlBody = message.BodyHtml,
        };
        if (message.Attachments is { Count: > 0 } attachments)
        {
            foreach (var attachment in attachments)
            {
                bodyBuilder.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
            }
        }

        mimeMessage.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(
                options.SmtpHost, options.SmtpPort, ResolveSecureOptions(options.UseStartTls, options.SmtpPort), cancellationToken);

            if (!string.IsNullOrWhiteSpace(options.Username))
            {
                await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
        }
        catch (SmtpCommandException exception)
        {
            var permanent = (int)exception.StatusCode >= 500
                || exception.ErrorCode is SmtpErrorCode.RecipientNotAccepted or SmtpErrorCode.SenderNotAccepted;
            throw new SmtpTransportException(permanent, $"{(int)exception.StatusCode} {exception.Message}");
        }
        catch (SmtpProtocolException exception)
        {
            throw new SmtpTransportException(isPermanent: false, exception.Message);
        }
    }

    /// <summary>
    /// Как шифровать соединение. Порт 465 — TLS сразу при подключении (RFC 8314), и назвать это
    /// явно важнее, чем короче: <see cref="SecureSocketOptions.Auto"/> при неудаче с TLS молча
    /// продолжает без шифрования, а следом уходит логин и пароль ящика.
    /// </summary>
    internal static SecureSocketOptions ResolveSecureOptions(bool useStartTls, int port) => (useStartTls, port) switch
    {
        (true, _) => SecureSocketOptions.StartTls,
        (false, 465) => SecureSocketOptions.SslOnConnect,
        _ => SecureSocketOptions.Auto,
    };
}
