namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Thin seam over the actual SMTP send so <see cref="SmtpEmailChannel"/> is unit-testable without a
/// live server. Implementations throw <see cref="SmtpTransportException"/> classifying the failure as
/// permanent (e.g. a 5xx bad-address reply) or transient (timeout, 4xx, network).
/// </summary>
public interface ISmtpTransport
{
    Task SendAsync(SmtpMessage message, CancellationToken cancellationToken);
}

/// <summary>An already-rendered outbound email ready to hand to SMTP.</summary>
public sealed record SmtpMessage(
    string FromAddress,
    string? FromName,
    string ToAddress,
    string Subject,
    string BodyText,
    string BodyHtml,
    IReadOnlyList<SmtpAttachment>? Attachments = null);

/// <summary>A file attached to an outbound email.</summary>
public sealed record SmtpAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>A classified SMTP delivery failure: permanent failures fail fast, transient ones retry.</summary>
public sealed class SmtpTransportException(bool isPermanent, string message) : Exception(message)
{
    public bool IsPermanent { get; } = isPermanent;
}
