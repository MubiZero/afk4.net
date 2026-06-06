using System.Net;
using AFK4.Platform.Api.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class PayomSmsTransportTests
{
    [Fact]
    public async Task SendAsync_PostsExpectedRequest()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Created)
        {
            Content = new StringContent("{\"deliveryStatus\":\"ACCEPTED\"}"),
        });
        var transport = CreateTransport(handler);

        await transport.SendAsync(new SmsMessage("+992937380070", "код 123456"), CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://gateway.payom.tj/api/message", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer test-token", handler.AuthorizationRaw);
        Assert.Contains("\"telephone\":\"+992937380070\"", handler.Body);
        Assert.Contains("\"senderName\":\"AFK4.NET\"", handler.Body);
        Assert.Contains("\"type\":\"SMS\"", handler.Body);
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
        var transport = CreateTransport(handler);

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => transport.SendAsync(new SmsMessage("+992900000000", "x"), CancellationToken.None));
        Assert.Equal(expectedPermanent, exception.IsPermanent);
    }

    [Fact]
    public async Task SendAsync_NetworkError_IsTransient()
    {
        var transport = CreateTransport(new ThrowingHandler(new HttpRequestException("boom")));

        var exception = await Assert.ThrowsAsync<SmsTransportException>(
            () => transport.SendAsync(new SmsMessage("+992900000000", "x"), CancellationToken.None));
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
