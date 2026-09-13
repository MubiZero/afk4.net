using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Кнопка проверки почты существует ради одной вещи — причины отказа. Пока её не было, «письма не
/// уходят» разбирали сканированием портов снаружи и гаданием по счётчику провалов.
/// </summary>
public sealed class PlatformTestEmailEndpointTests
{
    private sealed class RefusingSmtp : ISmtpTransport
    {
        public Task SendAsync(SmtpMessage message, CancellationToken cancellationToken) =>
            throw new SmtpTransportException(isPermanent: true, "Connection refused");
    }

    private sealed class CapturingSmtp : ISmtpTransport
    {
        public List<SmtpMessage> Sent { get; } = [];

        public Task SendAsync(SmtpMessage message, CancellationToken cancellationToken)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static void ConfigureSender(IServiceCollection services) =>
        services.Configure<NotificationOptions>(options =>
        {
            options.FromAddress = "no-reply@afk4.net";
            options.FromName = "AFK4";
        });

    [Fact]
    public async Task POST_testEmail_SendsThroughTheRealChannel()
    {
        var smtp = new CapturingSmtp();
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmtpTransport>();
            services.AddSingleton<ISmtpTransport>(smtp);
            ConfigureSender(services);
        });
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            "/api/platform/health/test-email", new SendTestEmailRequest("owner@club.example"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SendTestEmailResultDto>();
        Assert.True(result!.Delivered);
        Assert.Null(result.Error);
        var message = Assert.Single(smtp.Sent);
        Assert.Equal("owner@club.example", message.ToAddress);
    }

    [Fact]
    public async Task POST_testEmail_WhenDeliveryFails_ReturnsTheReason()
    {
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmtpTransport>();
            services.AddSingleton<ISmtpTransport>(new RefusingSmtp());
            ConfigureSender(services);
        });
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            "/api/platform/health/test-email", new SendTestEmailRequest("owner@club.example"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SendTestEmailResultDto>();
        Assert.False(result!.Delivered);
        Assert.Contains("Connection refused", result.Error!, StringComparison.Ordinal);
    }

    // Ровно тот случай, ради которого кнопка и делалась: 12.09.2026 письма молча не уходили, потому
    // что переменные почты не доехали до Coolify, а причину было видно только в базе.
    [Fact]
    public async Task POST_testEmail_WhenMailIsNotConfigured_SaysExactlyThat()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            "/api/platform/health/test-email", new SendTestEmailRequest("owner@club.example"));

        var result = await response.Content.ReadFromJsonAsync<SendTestEmailResultDto>();
        Assert.False(result!.Delivered);
        Assert.Contains("FromAddress", result.Error!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task POST_testEmail_IsWrittenToTheAuditTrail()
    {
        await using var factory = new PlatformApiFactory(extraServices: services =>
        {
            services.RemoveAll<ISmtpTransport>();
            services.AddSingleton<ISmtpTransport>(new RefusingSmtp());
            ConfigureSender(services);
        });
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        await client.PostAsJsonAsync(
            "/api/platform/health/test-email", new SendTestEmailRequest("owner@club.example"));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = await db.AuditRecords
            .SingleAsync(row => row.Action == AuditActionNames.PlatformSendTestEmail);
        Assert.Equal(AuditOutcome.Failed, record.Outcome);
        Assert.Equal("owner@club.example", record.TargetId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    public async Task POST_testEmail_RejectsAnAddressItCannotSendTo(string email)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync(
            "/api/platform/health/test-email", new SendTestEmailRequest(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_testEmail_WithoutASession_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/platform/health/test-email", new SendTestEmailRequest("owner@club.example"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
