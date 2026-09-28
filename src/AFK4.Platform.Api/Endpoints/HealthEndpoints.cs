namespace AFK4.Platform.Api.Endpoints;

internal static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/api/health", (TimeProvider timeProvider) =>
        {
            return Results.Ok(new HealthResponse("ok", timeProvider.GetUtcNow()));
        });

    }
}
