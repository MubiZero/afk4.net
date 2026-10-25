using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Health_ReturnsOk()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body.Status);
    }

    [Fact]
    public async Task Ready_ReturnsOk_WhenTheDatabaseAnswers()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", (await response.Content.ReadFromJsonAsync<HealthResponse>())!.Status);
    }

    /// <summary>
    /// База лежит — проба готовности говорит «нет», а не бросает исключение наружу (Npgsql при
    /// отказе соединения бросает его и из CanConnectAsync). Живучесть /api/health базу не трогает
    /// вовсе: по ней Coolify перезапускает контейнер, и перезапуск API лежащую базу не поднял бы.
    /// </summary>
    [Fact]
    public async Task ReadinessProbe_SaysNo_WhenTheDatabaseIsDown()
    {
        // Порт 1 никто не слушает: соединение отказывают сразу.
        await using var db = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=afk4_down;Username=nobody;Timeout=2")
            .Options);

        Assert.False(await AFK4.Platform.Api.Endpoints.HealthEndpoints.DatabaseAnswersAsync(db, CancellationToken.None));
    }

    private sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);
}
