using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.Extensions.Options;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

/// Шлюз принимает не текст, а одобренный шаблон со значениями плейсхолдеров. Канал обязан
/// перевести наше уведомление на этот язык — и внятно отказаться, если переводить нечем.
public sealed class SmsChannelTests
{
    private const string CodeTemplateId = "df83ca2e-1405-45db-9762-f9398c529fe4";

    private static NotificationOutboxEntity Row(
        string phone = "+992937380070",
        string templateKey = NotificationTemplateKeys.PlayerSignInCode,
        string? code = "123456") => new()
    {
        NotificationOutboxId = Guid.NewGuid(),
        Channel = "Sms",
        TemplateKey = templateKey,
        RecipientAddress = phone,
        BodyText = "AFK4.NET: код входа 123456. Никому не сообщайте.",
        TokensJson = code is null ? null : JsonSerializer.Serialize(new Dictionary<string, string> { ["code"] = code }),
    };

    private static SmsChannel Channel(StubSmsTransport transport, SmsOptions? options = null) =>
        new(transport, Options.Create(options ?? new SmsOptions
        {
            SenderLabel = "AFK4.NET",
            TemplateIds = { [NotificationTemplateKeys.PlayerSignInCode] = CodeTemplateId },
        }));

    [Fact]
    public void Channel_IsSms()
    {
        Assert.Equal(NotificationChannel.Sms, Channel(new StubSmsTransport()).Channel);
    }

    /// Код обязан ехать в code-1: модерация отклоняет код, засунутый в текстовый плейсхолдер.
    [Fact]
    public async Task SendAsync_SendsApprovedTemplateWithCodeInItsOwnPlaceholder()
    {
        var transport = new StubSmsTransport();

        var result = await Channel(transport).SendAsync(Row(), CancellationToken.None);

        Assert.True(result.Success);
        var sent = Assert.Single(transport.Sent);
        Assert.Equal("+992937380070", sent.ToPhoneNumber);
        Assert.Equal(CodeTemplateId, sent.TemplateId);
        Assert.Equal("123456", sent.Variables["code-1"]);
        Assert.Equal("AFK4.NET", sent.Variables["text-1"]);
    }

    /// Кто пишет — из наших настроек, а не из тела запроса: иначе достаточно подделать вызов,
    /// чтобы разослать SMS, подписанные чужим брендом.
    [Fact]
    public async Task SendAsync_TooLongSenderLabel_IsTrimmedToTheSegmentBudget()
    {
        var transport = new StubSmsTransport();
        var options = new SmsOptions
        {
            SenderLabel = new string('Я', 60),
            SenderLabelMaxLength = 40,
            TemplateIds = { [NotificationTemplateKeys.PlayerSignInCode] = CodeTemplateId },
        };

        await Channel(transport, options).SendAsync(Row(), CancellationToken.None);

        Assert.Equal(40, transport.Sent.Single().Variables["text-1"].Length);
    }

    /// Без одобренного шаблона отправлять нечего, и повтор не поможет: нужен человек.
    [Fact]
    public async Task SendAsync_UnconfiguredTemplate_IsPermanentFailure()
    {
        var transport = new StubSmsTransport();

        var result = await Channel(transport)
            .SendAsync(Row(templateKey: NotificationTemplateKeys.StaffPasswordResetSms), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.Contains("Sms__TemplateIds__", result.Error);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task SendAsync_WithoutCode_IsPermanentFailure()
    {
        var transport = new StubSmsTransport();

        var result = await Channel(transport).SendAsync(Row(code: null), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task SendAsync_MissingPhone_IsPermanentFailure()
    {
        var result = await Channel(new StubSmsTransport()).SendAsync(Row(phone: ""), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task SendAsync_PermanentTransportError_IsPermanent()
    {
        var result = await Channel(new StubSmsTransport(new SmsTransportException(isPermanent: true, "bad")))
            .SendAsync(Row(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
    }

    [Fact]
    public async Task SendAsync_TransientTransportError_IsRetryable()
    {
        var result = await Channel(new StubSmsTransport(new SmsTransportException(isPermanent: false, "later")))
            .SendAsync(Row(), CancellationToken.None);

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
