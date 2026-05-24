using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class FloorMapBulkUpdateEndpointTests
{
    [Fact]
    public async Task Get_EmitsETagHeader_StableAcrossUnchangedReads()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedZoneAsync(factory);

        var first = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");
        var second = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstEtag = first.Headers.ETag?.Tag;
        var secondEtag = second.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrWhiteSpace(firstEtag));
        Assert.Equal(firstEtag, secondEtag);
    }

    [Fact]
    public async Task Put_WithoutIfMatch_Returns428()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedZoneAsync(factory);

        var request = new FloorMapBulkUpdateRequest(
            TestIds.OrganizationId,
            Zones: [new FloorMapBulkZoneRequest(null, "z-1", "Hall", 1)],
            Seats: []);
        var response = await client.PutAsJsonAsync($"/api/branches/{TestIds.BranchId:D}/floor-map", request);

        Assert.Equal((HttpStatusCode)428, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithStaleIfMatch_Returns412AndCurrentETag()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedZoneAsync(factory);

        var current = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");
        var currentEtag = current.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrWhiteSpace(currentEtag));

        var staleEtag = "\"0000000000000000000000000000000000000000000000000000000000000000\"";
        var put = BuildPut(staleEtag, new FloorMapBulkUpdateRequest(
            TestIds.OrganizationId,
            Zones: [new FloorMapBulkZoneRequest(null, "z-1", "Lounge", 1)],
            Seats: []));
        var response = await client.SendAsync(put);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(currentEtag, response.Headers.ETag?.Tag);
    }

    [Fact]
    public async Task Put_HappyPath_CreatesZonesAndSeats_AndETagChanges()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedZoneAsync(factory);

        var current = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");
        var startingEtag = current.Headers.ETag?.Tag;
        Assert.False(string.IsNullOrWhiteSpace(startingEtag));

        var request = new FloorMapBulkUpdateRequest(
            TestIds.OrganizationId,
            Zones:
            [
                new FloorMapBulkZoneRequest(null, "z-new-1", "Lounge", 1),
                new FloorMapBulkZoneRequest(null, "z-new-2", "VIP", 2)
            ],
            Seats:
            [
                new FloorMapBulkSeatRequest(null, "s-1", "z-new-1", "PC-001", 1),
                new FloorMapBulkSeatRequest(null, "s-2", "z-new-1", "PC-002", 2),
                new FloorMapBulkSeatRequest(null, "s-3", "z-new-2", "VIP-1", 1)
            ]);

        var put = BuildPut(startingEtag!, request);
        var response = await client.SendAsync(put);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FloorMapBulkUpdateResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(startingEtag, body.ETag);
        Assert.Equal(2, body.Zones.Count);
        Assert.Equal(3, body.Seats.Count);
        Assert.All(body.Zones, z => Assert.NotEqual(Guid.Empty, z.ZoneId));
        Assert.All(body.Seats, s => Assert.NotEqual(Guid.Empty, s.SeatId));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persistedSeats = await dbContext.Seats
            .AsNoTracking()
            .Where(s => s.BranchId == TestIds.BranchId)
            .ToListAsync();
        Assert.Equal(3, persistedSeats.Count);
    }

    [Fact]
    public async Task Put_RemovesSeatsAbsentFromPayload()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        var (zoneId, seatId) = await SeedZoneAndSeatAsync(factory);

        var current = await client.GetAsync($"/api/branches/{TestIds.BranchId:D}/floor-map");
        var etag = current.Headers.ETag?.Tag;

        var request = new FloorMapBulkUpdateRequest(
            TestIds.OrganizationId,
            Zones: [new FloorMapBulkZoneRequest(zoneId, zoneId.ToString("D"), "Main Hall", 1)],
            Seats: []);
        var response = await client.SendAsync(BuildPut(etag!, request));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stillThere = await dbContext.Seats
            .AsNoTracking()
            .AnyAsync(s => s.SeatId == seatId);
        Assert.False(stillThere);
    }

    [Fact]
    public async Task Put_WithStaffWithoutManageLayout_ReturnsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedZoneAsync(factory);

        var request = new FloorMapBulkUpdateRequest(
            TestIds.OrganizationId,
            Zones: [new FloorMapBulkZoneRequest(null, "z-1", "Lounge", 1)],
            Seats: []);
        var put = BuildPut("*", request);
        var response = await client.SendAsync(put);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static HttpRequestMessage BuildPut(string ifMatch, FloorMapBulkUpdateRequest request)
    {
        var message = new HttpRequestMessage(HttpMethod.Put, $"/api/branches/{TestIds.BranchId:D}/floor-map")
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        return message;
    }

    private static async Task SeedZoneAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
        });
        await dbContext.SaveChangesAsync();
    }

    private static async Task<(Guid zoneId, Guid seatId)> SeedZoneAndSeatAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var now = DateTimeOffset.Parse("2026-05-12T00:00:00Z");
        dbContext.Zones.Add(new ZoneEntity
        {
            ZoneId = zoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = now
        });
        dbContext.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = zoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync();
        return (zoneId, seatId);
    }
}
