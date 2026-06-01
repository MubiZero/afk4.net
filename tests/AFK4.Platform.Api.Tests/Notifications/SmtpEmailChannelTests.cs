using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class SmtpEmailChannelTests
{
    private static NotificationOutboxEntity Row(string address = "player@example.com") => new()
    {
        NotificationOutboxId = Guid.NewGuid(),
        Channel = "Email",
        RecipientAddress = address,
        Subject = "Subject line",
        BodyText = "plain body",
        BodyHtml = "<p>html body</p>",
    };

    private static SmtpEmailChannel CreateChannel(ISmtpTransport transport) =>
        new(transport, Options.Create(new NotificationOptions
        {
            FromAddress = "noreply@afk4.net",
            FromName = "AFK4",
        }));

    [Fact]
    public void Channel_IsEmail()
    {
        var channel = CreateChannel(new StubTransport());

        Assert.Equal(NotificationChannel.Email, channel.Channel);
    }

    [Fact]
    public async Task SendAsync_DeliversAndPassesRenderedMessageToTransport()
    {
        var transport = new StubTransport();
        var channel = CreateChannel(transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.True(result.Success);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal("noreply@afk4.net", sent.FromAddress);
        Assert.Equal("AFK4", sent.FromName);
        Assert.Equal("player@example.com", sent.ToAddress);
        Assert.Equal("Subject line", sent.Subject);
        Assert.Equal("plain body", sent.BodyText);
        Assert.Equal("<p>html body</p>", sent.BodyHtml);
    }

    [Fact]
    public async Task SendAsync_PassesOutboxAttachmentsToTransport()
    {
        var transport = new StubTransport();
        var channel = CreateChannel(transport);
        var row = Row();
        row.Attachments.Add(new NotificationOutboxAttachmentEntity
        {
            NotificationOutboxAttachmentId = Guid.NewGuid(),
            FileName = "shifts.csv",
            ContentType = "text/csv",
            Content = "a,b\r\n1,2\r\n"u8.ToArray(),
        });

        var result = await channel.SendAsync(row, CancellationToken.None);

        Assert.True(result.Success);
        var sent = Assert.Single(transport.Sent);
        var attachment = Assert.Single(sent.Attachments!);
        Assert.Equal("shifts.csv", attachment.FileName);
        Assert.Equal("text/csv", attachment.ContentType);
        Assert.Equal("a,b\r\n1,2\r\n"u8.ToArray(), attachment.Content);
    }

    [Fact]
    public async Task SendAsync_MapsPermanentTransportFailureToPermanentResult()
    {
        var transport = new StubTransport(_ => throw new SmtpTransportException(isPermanent: true, "550 mailbox unavailable"));
        var channel = CreateChannel(transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.Contains("550", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_MapsTransientTransportFailureToRetryableResult()
    {
        var transport = new StubTransport(_ => throw new SmtpTransportException(isPermanent: false, "451 try again"));
        var channel = CreateChannel(transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
        Assert.Contains("451", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_TreatsUnexpectedExceptionAsTransient()
    {
        var transport = new StubTransport(_ => throw new InvalidOperationException("socket blew up"));
        var channel = CreateChannel(transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
    }

    [Fact]
    public async Task SendAsync_FailsPermanentlyWhenRecipientAddressMissing()
    {
        var transport = new StubTransport();
        var channel = CreateChannel(transport);

        var result = await channel.SendAsync(Row(address: string.Empty), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.Empty(transport.Sent);
    }

    private sealed class StubTransport(Action<SmtpMessage>? onSend = null) : ISmtpTransport
    {
        public List<SmtpMessage> Sent { get; } = [];

        public Task SendAsync(SmtpMessage message, CancellationToken cancellationToken)
        {
            onSend?.Invoke(message);
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
