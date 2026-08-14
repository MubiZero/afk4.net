using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class PushChannelTests
{
    private static readonly Guid PlayerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static NotificationOutboxEntity Row(Guid? playerAccountId = null, string body = "Сессия кончится через 10 минут") => new()
    {
        NotificationOutboxId = Guid.NewGuid(),
        Channel = "Push",
        TemplateKey = "player.session_ending",
        Subject = "AFK4",
        BodyText = body,
        PlayerAccountId = playerAccountId ?? PlayerId,
    };

    [Fact]
    public void Channel_IsPush()
    {
        var channel = new PushChannel(new StubDeviceStore(), new StubPushTransport());
        Assert.Equal(NotificationChannel.Push, channel.Channel);
    }

    /// У одного игрока бывает телефон и планшет — уведомление должно прийти на оба.
    [Fact]
    public async Task SendAsync_DeliversToEveryRegisteredDevice()
    {
        var devices = new StubDeviceStore("token-phone", "token-tablet");
        var transport = new StubPushTransport();
        var channel = new PushChannel(devices, transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["token-phone", "token-tablet"], transport.Sent.Select(message => message.DeviceToken));
        Assert.Equal("Сессия кончится через 10 минут", transport.Sent[0].Body);
        Assert.Equal("player.session_ending", transport.Sent[0].Data!["template"]);
    }

    /// Пуш некуда нести: игрок не заходил с телефона или запретил уведомления.
    [Fact]
    public async Task SendAsync_NoDevices_IsPermanentFailure()
    {
        var channel = new PushChannel(new StubDeviceStore(), new StubPushTransport());

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task SendAsync_WithoutPlayerAccount_IsPermanentFailure()
    {
        var channel = new PushChannel(new StubDeviceStore("token"), new StubPushTransport());

        var row = Row();
        row.PlayerAccountId = null;
        var result = await channel.SendAsync(row, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
    }

    /// Приложение удалили: такой токен не оживёт, и копить на нём ошибки незачем.
    [Fact]
    public async Task SendAsync_UnregisteredDevice_ForgetsTheToken()
    {
        var devices = new StubDeviceStore("dead-token");
        var transport = new StubPushTransport(
            new PushTransportException(isPermanent: true, "UNREGISTERED", isUnregistered: true));
        var channel = new PushChannel(devices, transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.Equal(["dead-token"], devices.Forgotten);
    }

    /// Один телефон зазвонил — игрок уведомлён. Повтор ради второго прислал бы дубль на первый.
    [Fact]
    public async Task SendAsync_OneDeviceDelivered_CountsAsSent()
    {
        var devices = new StubDeviceStore("bad-token", "good-token");
        var transport = new StubPushTransport(
            new PushTransportException(isPermanent: false, "timeout"), failFirstOnly: true);
        var channel = new PushChannel(devices, transport);

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["good-token"], transport.Sent.Select(message => message.DeviceToken));
    }

    [Fact]
    public async Task SendAsync_TransientTransportError_IsRetryable()
    {
        var channel = new PushChannel(
            new StubDeviceStore("token"),
            new StubPushTransport(new PushTransportException(isPermanent: false, "network")));

        var result = await channel.SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
    }

    private sealed class StubDeviceStore(params string[] tokens) : IPlayerDeviceStore
    {
        private readonly List<string> tokens = [.. tokens];

        public List<string> Forgotten { get; } = [];

        public Task RegisterAsync(Guid playerAccountId, string pushToken, string platform, string? locale, CancellationToken cancellationToken)
        {
            tokens.Add(pushToken);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(Guid playerAccountId, string pushToken, CancellationToken cancellationToken)
        {
            tokens.Remove(pushToken);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PlayerDeviceEntity>> ListAsync(Guid playerAccountId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PlayerDeviceEntity>>(tokens
                .Select(token => new PlayerDeviceEntity { PushToken = token, PlayerAccountId = playerAccountId })
                .ToList());

        public Task ForgetAsync(string pushToken, CancellationToken cancellationToken)
        {
            Forgotten.Add(pushToken);
            tokens.Remove(pushToken);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPushTransport(Exception? throwOnSend = null, bool failFirstOnly = false) : IPushTransport
    {
        private int calls;

        public List<PushMessage> Sent { get; } = [];

        public Task SendAsync(PushMessage message, CancellationToken cancellationToken)
        {
            calls++;
            if (throwOnSend is not null && (!failFirstOnly || calls == 1))
            {
                throw throwOnSend;
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}
