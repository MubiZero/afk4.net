using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class SmsChannelTests
{
    private static NotificationOutboxEntity Row(string phone = "+992937380070", string body = "код 123456") => new()
    {
        NotificationOutboxId = Guid.NewGuid(),
        Channel = "Sms",
        RecipientAddress = phone,
        BodyText = body,
    };

    [Fact]
    public void Channel_IsSms()
    {
        var channel = new SmsChannel(new StubSmsTransport());
        Assert.Equal(NotificationChannel.Sms, channel.Channel);
    }

    [Fact]
    public async Task SendAsync_DeliversTextToTransport()
    {
        var transport = new StubSmsTransport();
        var channel = new SmsChannel(transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.True(result.Success);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal("+992937380070", sent.ToPhoneNumber);
        Assert.Equal("код 123456", sent.Text);
    }

    [Fact]
    public async Task SendAsync_MissingPhone_IsPermanentFailure()
    {
        var channel = new SmsChannel(new StubSmsTransport());

        var result = await channel.SendAsync(Row(phone: ""), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task SendAsync_PermanentTransportError_IsPermanent()
    {
        var channel = new SmsChannel(new StubSmsTransport(new SmsTransportException(isPermanent: true, "bad")));

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task SendAsync_TransientTransportError_IsRetryable()
    {
        var channel = new SmsChannel(new StubSmsTransport(new SmsTransportException(isPermanent: false, "later")));

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
    }

    private sealed class StubSmsTransport(Exception? throwOnSend = null) : ISmsTransport
    {
        public List<SmsMessage> Sent { get; } = [];

        public Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            if (throwOnSend is not null)
            {
                throw throwOnSend;
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
