using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class HealthEndpoints
{
    private static readonly TimeSpan DatabaseProbeTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Отвечает ли база за <see cref="DatabaseProbeTimeout"/>.</summary>
    internal static async Task<bool> DatabaseAnswersAsync(PlatformDbContext db, CancellationToken cancellationToken)
    {
        using var probe = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        probe.CancelAfter(DatabaseProbeTimeout);
        try
        {
            return await db.Database.CanConnectAsync(probe.Token);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // CanConnectAsync не всегда отвечает «false»: отказ соединения Npgsql бросает
            // исключением (через стратегию повторов), таймаут — отменой. Для готовности это одно.
            return false;
        }
    }

    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Жив ли процесс. Базу намеренно не трогает: по этой проверке Coolify перезапускает
        // контейнер, а перезапуск API лежащую базу не поднимет — только добавит простоя.
        app.MapGet("/api/health", (TimeProvider timeProvider) =>
        {
            return Results.Ok(new HealthResponse("ok", timeProvider.GetUtcNow()));
        });

        // Готов ли API работать: отвечает ли база. Её смотрит внешний монитор аптайма — раньше
        // падение базы он не видел, потому что /api/health отвечал «ok» и без неё.
        app.MapGet("/api/health/ready", async (PlatformDbContext db, TimeProvider timeProvider, CancellationToken cancellationToken) =>
            await DatabaseAnswersAsync(db, cancellationToken)
                ? Results.Ok(new HealthResponse("ok", timeProvider.GetUtcNow()))
                : Results.Json(
                    new HealthResponse("database_unavailable", timeProvider.GetUtcNow()),
                    statusCode: StatusCodes.Status503ServiceUnavailable));
    }
}
