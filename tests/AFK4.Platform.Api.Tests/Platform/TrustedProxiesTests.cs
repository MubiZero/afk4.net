using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Platform.Http;
using AFK4.Shared.Contracts.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Адрес клиента за Traefik. Без разбора X-Forwarded-For каждое ограничение «по IP» считало бы
/// адрес прокси — один счётчик на всю сеть.
/// </summary>
public sealed class TrustedProxiesTests
{
    [Fact]
    public async Task BehindTheInternalProxy_TheClientAddressIsTheForwardedOne()
    {
        var address = await ResolveAsync(peer: "10.0.3.7", forwardedFor: "203.0.113.9");

        Assert.Equal(IPAddress.Parse("203.0.113.9"), address);
    }

    /// <summary>От внешнего адреса заголовок не значит ничего: иначе лимит обходил бы любой.</summary>
    [Fact]
    public async Task FromAPublicPeer_AForgedHeaderIsIgnored()
    {
        var address = await ResolveAsync(peer: "198.51.100.4", forwardedFor: "203.0.113.9");

        Assert.Equal(IPAddress.Parse("198.51.100.4"), address);
    }

    /// <summary>Traefik перезаписывает заголовок сам; верим ровно одному шагу, а не цепочке, которую прислал клиент.</summary>
    [Fact]
    public async Task OnlyTheLastHopIsTrusted()
    {
        var address = await ResolveAsync(peer: "172.18.0.2", forwardedFor: "1.2.3.4, 203.0.113.9");

        Assert.Equal(IPAddress.Parse("203.0.113.9"), address);
    }

    [Fact]
    public async Task TheTrustedNetworks_CanBeNarrowedByConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [$"{TrustedProxies.KnownNetworksKey}:0"] = "10.0.1.0/24" })
            .Build();

        Assert.Equal(IPAddress.Parse("10.0.3.7"), await ResolveAsync("10.0.3.7", "203.0.113.9", configuration));
        Assert.Equal(IPAddress.Parse("203.0.113.9"), await ResolveAsync("10.0.1.5", "203.0.113.9", configuration));
    }

    /// <summary>Два клиента за одним прокси не делят лимит входа сотрудника (10 в минуту на адрес).</summary>
    [Fact]
    public async Task TwoClientsBehindTheProxy_DoNotShareTheStaffSignInLimit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 10; attempt++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await SignInFromAsync(client, "203.0.113.10")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, (await SignInFromAsync(client, "203.0.113.10")).StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await SignInFromAsync(client, "203.0.113.11")).StatusCode);
    }

    private static Task<HttpResponseMessage> SignInFromAsync(HttpClient client, string clientAddress)
    {
        var message = new HttpRequestMessage(HttpMethod.Post, StaffAuthRoutes.SignInByPhone)
        {
            Content = JsonContent.Create(new StaffSignInByPhoneRequest("+992900000001", "000000"))
        };
        message.Headers.Add("X-Forwarded-For", clientAddress);
        return client.SendAsync(message);
    }

    private static async Task<IPAddress?> ResolveAsync(string peer, string forwardedFor, IConfiguration? configuration = null)
    {
        var options = new ForwardedHeadersOptions();
        TrustedProxies.Configure(options, configuration ?? new ConfigurationBuilder().Build());
        IPAddress? seen = null;
        var middleware = new ForwardedHeadersMiddleware(
            context =>
            {
                seen = context.Connection.RemoteIpAddress;
                return Task.CompletedTask;
            },
            NullLoggerFactory.Instance,
            Options.Create(options));
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        httpContext.Request.Headers["X-Forwarded-For"] = forwardedFor;

        await middleware.Invoke(httpContext);
        return seen;
    }
}
