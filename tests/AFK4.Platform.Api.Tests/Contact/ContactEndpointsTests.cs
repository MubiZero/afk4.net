using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Endpoints;
using AFK4.Platform.Api.Notifications.Telegram;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AFK4.Platform.Api.Tests.Contact;

/// <summary>
/// Форма связи на публичном сайте: заявка владельца клуба уходит владельцу проекта в Telegram. Ручка открытая,
/// поэтому важны ловушка для ботов, предел на адрес, чужой текст в HTML Telegram и CORS только для сайта.
/// </summary>
public sealed class ContactEndpointsTests
{
    private const string Route = "/api/public/contact";
    private const long OwnerChat = 4242;

    [Fact]
    public async Task ARequest_GoesToTheOwnerChat_WithEverythingTheVisitorWrote()
    {
        var bot = new FakeBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, new ContactRequestDto("Мухаммад", "Pixel, Худжанд", "+992 90 111 22 33", "Нужна касса без интернета", "ru", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var sent = Assert.Single(bot.Sent);
        Assert.Equal(OwnerChat, sent.ChatId);
        Assert.Contains("Мухаммад", sent.Html, StringComparison.Ordinal);
        Assert.Contains("+992 90 111 22 33", sent.Html, StringComparison.Ordinal);
        Assert.Contains("Pixel, Худжанд", sent.Html, StringComparison.Ordinal);
        Assert.Contains("Нужна касса без интернета", sent.Html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANameIsOptional_OnlyAContactIsRequired()
    {
        var bot = new FakeBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, new ContactRequestDto(null, null, "+992 90 111 22 33", null, "tg", null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = Assert.Single(bot.Sent).Html;
        Assert.Contains("+992 90 111 22 33", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Имя:", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TextWithTags_ReachesTelegramEscaped()
    {
        var bot = new FakeBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(Route, new ContactRequestDto("<b>Имя</b>", null, "@user", "<a href=\"http://evil\">нажми</a>", "en", null));

        var html = Assert.Single(bot.Sent).Html;
        Assert.DoesNotContain("<a href", html, StringComparison.Ordinal);
        Assert.Contains("&lt;a href=", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;Имя&lt;/b&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheBotTrap_IsAnsweredWithOk_ButNothingIsSent()
    {
        var bot = new FakeBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, new ContactRequestDto("Бот", null, "spam", "купите", "ru", "http://spam.example"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(bot.Sent);
    }

    [Theory]
    [InlineData("Мухаммад", null)]
    [InlineData("Мухаммад", "")]
    [InlineData(null, "   ")]
    public async Task WithoutAContact_TheRequestIsRefused(string? name, string? contact)
    {
        var bot = new FakeBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, new ContactRequestDto(name, null, contact, "привет", "ru", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(bot.Sent);
    }

    [Fact]
    public async Task LongText_IsCutBeforeItIsSent()
    {
        var bot = new FakeBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync(Route, new ContactRequestDto("М", null, "@m", new string('я', 5000), "ru", null));

        Assert.True(Assert.Single(bot.Sent).Html.Length < 1300);
    }

    [Fact]
    public async Task WithoutAChatOrABot_TheSiteIsToldItIsNotConfigured()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, new ContactRequestDto("М", null, "@m", "привет", "ru", null));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("not-configured", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhenTelegramIsDown_TheVisitorGetsAnErrorInsteadOfALostMessage()
    {
        var bot = new FakeBot { Failure = new TelegramApiException("down", isPermanent: false) };
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Route, new ContactRequestDto("М", null, "@m", "привет", "ru", null));

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task OneAddress_CannotFloodTheOwner()
    {
        var bot = new FakeBot();
        await using var factory = CreateFactory(bot);
        using var client = factory.CreateClient();
        var request = new ContactRequestDto("М", null, "@m", "привет", "ru", null);

        for (var i = 0; i < PublicRateLimits.ContactPerTenMinutes; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(Route, request)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync(Route, request)).StatusCode);
        Assert.Equal(PublicRateLimits.ContactPerTenMinutes, bot.Sent.Count);
    }

    [Theory]
    [InlineData("https://afk4.net", true)]
    [InlineData("https://www.afk4.net", true)]
    [InlineData("https://evil.example", false)]
    public async Task OnlyTheSiteMayCallItFromABrowser(string origin, bool allowed)
    {
        await using var factory = CreateFactory(new FakeBot());
        using var client = factory.CreateClient();
        using var preflight = new HttpRequestMessage(HttpMethod.Options, Route);
        preflight.Headers.Add("Origin", origin);
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        preflight.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(preflight);

        Assert.Equal(allowed, response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) && values.Contains(origin));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    private static PlatformApiFactory CreateFactory(FakeBot bot) => new(extraServices: services =>
    {
        services.RemoveAll<ITelegramBotApi>();
        services.AddSingleton<ITelegramBotApi>(bot);
        services.Configure<TelegramOptions>(options => options.BotToken = "test-token");
        services.Configure<ContactOptions>(options => options.TelegramChatId = OwnerChat);
    });

    private sealed class FakeBot : ITelegramBotApi
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
