using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.FloorMap;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AFK4.Platform.Api.Tests;

public sealed class FloorMapEndpointTests
{
    [Fact]
    public async Task FloorMap_ReturnsInitialSeatCards()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/floor-map");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = await response.Content.ReadFromJsonAsync<FloorMapDto>();
        Assert.NotNull(map);
        Assert.Equal("Demo Branch", map.BranchName);
        Assert.Contains(map.Seats, seat => seat.SeatName == "PC-001" && seat.State == "Free");
    }
}
