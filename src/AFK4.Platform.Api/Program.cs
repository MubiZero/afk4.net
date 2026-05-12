using AFK4.Platform.Api.FloorMap;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IFloorMapReadService, InMemoryFloorMapReadService>();

var app = builder.Build();

app.MapGet("/api/health", () =>
{
    return Results.Ok(new HealthResponse("ok", DateTimeOffset.UtcNow));
});

app.MapGet("/api/branches/{branchId:guid}/floor-map", (
    Guid branchId,
    IFloorMapReadService floorMapReadService) =>
{
    return Results.Ok(floorMapReadService.GetFloorMap(branchId));
});

app.Run();

public sealed record HealthResponse(string Status, DateTimeOffset ServerTimeUtc);

public partial class Program;
