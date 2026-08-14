using System.Net;
using System.Text.Json.Nodes;
using AFK4.Platform.Api.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class PayomSmsTransportTests
{
    private const string CodeTemplateId = "df83ca2e-1405-45db-9762-f9398c529fe4";

    private static SmsMessage CodeMessage(string phone = "+992937380070") =>
        new(phone, CodeTemplateId, new Dictionary<string, string>
        {
            ["text-1"] = "AFK4.NET",
            ["code-1"] = "123456",
        });

    /// Свободный текст шлюз отклоняет: в теле едет идентификатор одобренного шаблона
    /// и значения плейсхолдеров, а формулировка живёт на стороне payom.
    [Fact]
    public async Task SendAsync_PostsTemplateWithVariables()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{\"id\":\"msg-1\",\"deliveryStatus\":\"ACCEPTED\"}"),
        });

        await CreateTransport(handler).SendAsync(CodeMessage(), CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://gateway.payom.tj/api/message", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer test-token", handler.AuthorizationRaw);

        var body = JsonNode.Parse(handler.Body)!.AsObject();
        Assert.Equal("+992937380070", body["telephone"]!.ToString());
        Assert.Equal("AFK4.NET", body["senderName"]!.ToString());
        Assert.Equal("SMS", body["type"]!.ToString());
        Assert.Equal(CodeTemplateId, body["templateMessage"]!["templateId"]!.ToString());
        Assert.Equal("123456", body["templateMessage"]!["variables"]!["code-1"]!.ToString());
        // Ни в каком виде: поле text — это 422 «Свободный текст запрещён».
        Assert.Null(body["text"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, true)]
    [InlineData(HttpStatusCode.Forbidden, true)]
    [InlineData(HttpStatusCode.UnprocessableEntity, true)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    [InlineData((HttpStatusCode)429, false)]
    public async Task SendAsync_MapsStatusToPermanence(HttpStatusCode status, bool expectedPermanent)
    {
        var handler = new CapturingHandler(new HttpResponseMessage(status)
        {
            Content = new StringContent("error"),
        });

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => CreateTransport(handler).SendAsync(CodeMessage(), CancellationToken.None));

        Assert.Equal(expectedPermanent, exception.IsPermanent);
    }

    /// Полезное payom кладёт в detail (RFC 7807) — без него отклонённый шаблон не отличить
    /// от сетевого сбоя, и в очереди осталась бы бесполезная строка «422 error».
    [Fact]
    public async Task SendAsync_RejectedTemplate_CarriesTheGatewayDetail()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = new StringContent(
                "{\"detail\":\"templateMessage.variables[date-1]: должно быть в формате Y-m-d\"}"),
        });

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => CreateTransport(handler).SendAsync(CodeMessage(), CancellationToken.None));

        Assert.Contains("Y-m-d", exception.Message);
    }

    /// Успех без идентификатора сообщения — не успех: отличить принятое от проглоченного нечем.
    [Fact]
    public async Task SendAsync_SuccessWithoutMessageId_IsRetryable()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"deliveryStatus\":\"ACCEPTED\"}"),
        });

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => CreateTransport(handler).SendAsync(CodeMessage(), CancellationToken.None));

        Assert.False(exception.IsPermanent);
    }

    /// Ненастроенный шлюз — не сетевой сбой: повторять нечего, чинится конфигурацией.
    [Fact]
    public async Task SendAsync_WithoutApiToken_IsPermanent()
    {
        var transport = new PayomSmsTransport(
            new HttpClient(new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)))
            {
                BaseAddress = new Uri("https://gateway.payom.tj"),
            },
            apiToken: string.Empty,
            senderName: "AFK4.NET");

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => transport.SendAsync(CodeMessage(), CancellationToken.None));

        Assert.True(exception.IsPermanent);
    }

    [Fact]
    public async Task SendAsync_NetworkError_IsTransient()
    {
        var transport = CreateTransport(new ThrowingHandler(new HttpRequestException("boom")));

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => transport.SendAsync(CodeMessage(), CancellationToken.None));

        Assert.False(exception.IsPermanent);
    }

    private static PayomSmsTransport CreateTransport(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://gateway.payom.tj") },
            apiToken: "test-token",
            senderName: "AFK4.NET");

    private sealed class CapturingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;
        public string? AuthorizationRaw { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            AuthorizationRaw = request.Headers.TryGetValues("Authorization", out var values)
                ? string.Join(",", values)
                : null;
            if (request.Content is not null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return response;
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => throw exception;
    }
}
