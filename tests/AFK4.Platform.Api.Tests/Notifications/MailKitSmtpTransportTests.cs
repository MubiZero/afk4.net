using AFK4.Platform.Api.Notifications;
using MailKit.Security;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class MailKitSmtpTransportTests
{
    [Fact]
    public void ResolveSecureOptions_StartTlsRequested_DemandsStartTls()
    {
        Assert.Equal(SecureSocketOptions.StartTls, MailKitSmtpTransport.ResolveSecureOptions(useStartTls: true, port: 587));
    }

    // 465 — implicit TLS. Без явного SslOnConnect сюда попадал бы Auto, а он при неудаче с TLS
    // продолжает без шифрования — и следом уходит пароль ящика.
    [Fact]
    public void ResolveSecureOptions_ImplicitTlsPort_ConnectsOverTlsFromTheStart()
    {
        Assert.Equal(SecureSocketOptions.SslOnConnect, MailKitSmtpTransport.ResolveSecureOptions(useStartTls: false, port: 465));
    }

    [Fact]
    public void ResolveSecureOptions_PlainPortWithoutStartTls_LeavesTheChoiceToMailKit()
    {
        Assert.Equal(SecureSocketOptions.Auto, MailKitSmtpTransport.ResolveSecureOptions(useStartTls: false, port: 25));
    }
}
