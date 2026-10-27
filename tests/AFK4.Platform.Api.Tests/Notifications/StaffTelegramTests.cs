using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Notifications.Telegram;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Notifications;

/// <summary>
/// «Отчёты в Telegram»: сотрудник с правом на отчёты получает в Панели разовую ссылку на бота, бот
/// по коду из неё привязывает чат, и уведомления идут в этот чат. Владельцы входят по телефону,
/// почты у многих нет — без Telegram ежедневная сводка не доходила ни до кого.
/// </summary>
public sealed class StaffTelegramTests
{
    private static readonly string Route = $"/api/organizations/{TestIds.OrganizationId:D}/account/telegram";

    [Fact]
    public async Task TheLinkFromThePanel_ConnectsTheChatThatOpenedIt_Once()
    {
        var bot = new FakeTelegramBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        var link = await (await client.PostAsync($"{Route}/link", null)).Content.ReadFromJsonAsync<StaffTelegramLinkDto>();
        Assert.StartsWith("https://t.me/afk4_test_bot?start=", link!.DeepLink, StringComparison.Ordinal);
        var code = link.DeepLink.Split("start=")[1];

        await HandleAsync(factory, new TelegramIncomingMessage(1, 777, $"/start {code}", "ru"));
        var status = await client.GetFromJsonAsync<StaffTelegramStatusDto>(Route);
        Assert.True(status!.Linked);
        Assert.Contains(bot.Sent, message => message.ChatId == 777 && message.Html.Contains("подключён", StringComparison.Ordinal));

        // Код разовый: тот же QR, отсканированный другим телефоном, чужой чат не подключит.
        await HandleAsync(factory, new TelegramIncomingMessage(2, 888, $"/start {code}", "tg"));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(777, (await db.StaffUsers.SingleAsync(row => row.TelegramChatId != null)).TelegramChatId);
        Assert.Contains(bot.Sent, message => message.ChatId == 888 && message.Html.Contains("QR", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnExpiredLink_DoesNotConnect()
    {
        var bot = new FakeTelegramBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var link = await (await client.PostAsync($"{Route}/link", null)).Content.ReadFromJsonAsync<StaffTelegramLinkDto>();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var staff = await db.StaffUsers.SingleAsync(row => row.TelegramLinkCodeHash != null);
            staff.TelegramLinkCodeExpiresAtUtc = DateTimeOffset.UnixEpoch;
            await db.SaveChangesAsync();
        }

        await HandleAsync(factory, new TelegramIncomingMessage(1, 777, $"/start {link!.DeepLink.Split("start=")[1]}", "en"));

        Assert.False((await client.GetFromJsonAsync<StaffTelegramStatusDto>(Route))!.Linked);
    }

    [Fact]
    public async Task StopInTheBot_AndDisconnectInThePanel_BothUnlink()
    {
        var bot = new FakeTelegramBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Accountant);

        await LinkAsync(factory, client, chatId: 777);
        await HandleAsync(factory, new TelegramIncomingMessage(5, 777, "/stop", "ru"));
        Assert.False((await client.GetFromJsonAsync<StaffTelegramStatusDto>(Route))!.Linked);

        await LinkAsync(factory, client, chatId: 777);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(Route)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<StaffTelegramStatusDto>(Route))!.Linked);
    }

    // Отчёты о выручке — тем, кто видит их в Панели. Кассир свой чат к ним не подключит.
    [Fact]
    public async Task AnOperatorWithoutReports_CannotConnect()
    {
        await using var factory = CreateFactory(new FakeTelegramBot());
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"{Route}/link", null)).StatusCode);
    }

    [Fact]
    public async Task WithoutABot_ThePanelIsToldSo_InsteadOfAFailedLink()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);

        Assert.False((await client.GetFromJsonAsync<StaffTelegramStatusDto>(Route))!.BotConfigured);
        var response = await client.PostAsync($"{Route}/link", null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(StaffTelegramErrorCodeNames.NotConfigured, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANotification_GoesToTheLinkedChat_AndIsSuppressedForSomeoneWithoutTelegram()
    {
        await using var factory = CreateFactory(new FakeTelegramBot());
        await using var scope = factory.Services.CreateAsyncScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        await notifications.SendAsync(Request("with-chat", "777"), CancellationToken.None);
        await notifications.SendAsync(Request("without-chat", null), CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await db.NotificationOutbox.Where(row => row.Channel == nameof(NotificationChannel.Telegram)).ToListAsync();
        Assert.Equal("777", rows.Single(row => row.IdempotencyKey.StartsWith("with-chat", StringComparison.Ordinal)).RecipientAddress);
        Assert.Equal(
            NotificationOutboxStatus.Suppressed,
            rows.Single(row => row.IdempotencyKey.StartsWith("without-chat", StringComparison.Ordinal)).Status);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task TheChannel_RetriesOnlyWhatARetryCanFix(bool permanent, bool retryable)
    {
        var bot = new FakeTelegramBot { Failure = new TelegramApiException("boom", permanent) };
        var channel = new TelegramChannel(bot, Options.Create(new TelegramOptions { BotToken = "t" }));

        var result = await channel.SendAsync(
            new NotificationOutboxEntity { RecipientAddress = "777", Subject = "S", BodyHtml = "<p>B</p>" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(retryable, result.Retryable);
    }

    [Fact]
    public void AnEmail_BecomesATelegramMessage_WithOnlyTagsTelegramAccepts()
    {
        var text = TelegramText.FromEmail(
            "Итоги 27.10 <выручка>",
            "<p>Здравствуйте, Дилшод.</p><ul><li>Выручка: <strong>1 200 с.</strong></li><li>Смен: 2</li></ul><p>Конец &amp; всё</p>");

        Assert.Equal(
            "<b>Итоги 27.10 &lt;выручка&gt;</b>\n\nЗдравствуйте, Дилшод.\n\n• Выручка: <b>1 200 с.</b>\n• Смен: 2\nКонец &amp; всё",
            text);
    }

    private static PlatformApiFactory CreateFactory(FakeTelegramBot bot) => new(extraServices: services =>
    {
        services.RemoveAll<ITelegramBotApi>();
        services.AddSingleton<ITelegramBotApi>(bot);
        services.Configure<TelegramOptions>(options => options.BotToken = "test-token");
    });

    private static async Task LinkAsync(PlatformApiFactory factory, HttpClient client, long chatId)
    {
        var link = await (await client.PostAsync($"{Route}/link", null)).Content.ReadFromJsonAsync<StaffTelegramLinkDto>();
        await HandleAsync(factory, new TelegramIncomingMessage(chatId, chatId, $"/start {link!.DeepLink.Split("start=")[1]}", "ru"));
        Assert.True((await client.GetFromJsonAsync<StaffTelegramStatusDto>(Route))!.Linked);
    }

    private static async Task HandleAsync(PlatformApiFactory factory, TelegramIncomingMessage message)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TelegramBotDialog>().HandleAsync(message, CancellationToken.None);
    }

    private static NotificationRequest Request(string key, string? chatId) => new(
        TemplateKey: NotificationTemplateKeys.TelegramUnlinked,
        Category: NotificationCategory.Digest,
        Recipient: new NotificationRecipient(Locale: "ru", TelegramChatId: chatId),
        Tokens: new Dictionary<string, string>(),
        IdempotencyKey: key,
        PreferredChannels: [NotificationChannel.Telegram]);

    private sealed class FakeTelegramBot : ITelegramBotApi
    {
        public List<(long ChatId, string Html)> Sent { get; } = [];

        public Exception? Failure { get; init; }

        public Task<string> GetBotUserNameAsync(CancellationToken cancellationToken) => Task.FromResult("afk4_test_bot");

        public Task<IReadOnlyList<TelegramIncomingMessage>> GetUpdatesAsync(long offset, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TelegramIncomingMessage>>([]);

        public Task SendHtmlAsync(long chatId, string html, CancellationToken cancellationToken)
        {
            if (Failure is not null) throw Failure;
            lock (Sent) Sent.Add((chatId, html));
            return Task.CompletedTask;
        }
    }
}
