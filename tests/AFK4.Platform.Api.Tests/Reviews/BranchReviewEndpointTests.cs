using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Reviews;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Reviews;

/// <summary>Отзывы филиала в Панели: итог, разбивка по звёздам, отбор и ПК, за которым сидели.</summary>
public sealed class BranchReviewEndpointTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-20T18:00:00Z");

    private static string Route(string query = "") =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/reviews{query}";

    [Fact]
    public async Task TheClubSeesEveryReview_WithTheSeat_AndTheSummary()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await SeedAsync(factory, (5, "Отлично"), (4, null), (1, "Мышь липкая"), (5, null));

        var page = (await client.GetFromJsonAsync<BranchReviewsPageDto>(Route()))!;

        Assert.Equal(3.8, page.Rating);
        Assert.Equal(4, page.ReviewCount);
        Assert.Equal([1, 0, 0, 1, 2], page.CountsByRating);
        Assert.Equal(4, page.Items.Count);
        Assert.Equal("ПК 07", page.Items[0].SeatName);
        Assert.Equal("Азиз", page.Items[0].AuthorName);
        Assert.Null(page.NextBefore);
    }

    [Fact]
    public async Task OneStar_WithText_FindsTheComplaint()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await SeedAsync(factory, (5, "Отлично"), (1, null), (1, "Мышь липкая"));

        var page = (await client.GetFromJsonAsync<BranchReviewsPageDto>(Route("?rating=1&withComment=true")))!;

        var review = Assert.Single(page.Items);
        Assert.Equal("Мышь липкая", review.Comment);
        // Итог — по всем оценкам филиала, а не по отобранным.
        Assert.Equal(3, page.ReviewCount);
    }

    [Fact]
    public async Task ALongHistory_ComesPageByPage()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        await SeedAsync(factory, Enumerable.Range(0, 55).Select(_ => (4, (string?)null)).ToArray());

        var first = (await client.GetFromJsonAsync<BranchReviewsPageDto>(Route()))!;
        Assert.Equal(50, first.Items.Count);
        Assert.NotNull(first.NextBefore);

        var second = (await client.GetFromJsonAsync<BranchReviewsPageDto>(Route($"?before={Uri.EscapeDataString(first.NextBefore!.Value.ToString("O"))}")))!;
        Assert.Equal(5, second.Items.Count);
        Assert.Null(second.NextBefore);
    }

    // Отзыв бывает и о смене: стойка его не читает, читают владелец и управляющий.
    [Fact]
    public async Task AnOperator_DoesNotReadReviews()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.Operator);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Route())).StatusCode);
    }

    private static async Task SeedAsync(PlatformApiFactory factory, params (int Rating, string? Comment)[] reviews)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var zoneId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        db.Zones.Add(new ZoneEntity { ZoneId = zoneId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, Name = "Общий зал", SortOrder = 1, CreatedAtUtc = Start });
        db.Seats.Add(new SeatEntity { SeatId = seatId, OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId, ZoneId = zoneId, Name = "ПК 07", SortOrder = 1, CreatedAtUtc = Start });
        db.PlayerAccounts.Add(new PlayerAccountEntity { PlayerAccountId = playerId, OrganizationId = TestIds.OrganizationId, HomeBranchId = TestIds.BranchId, DisplayName = "Азиз" });
        for (var index = 0; index < reviews.Length; index++)
        {
            var sessionId = Guid.NewGuid();
            db.Sessions.Add(new SessionEntity
            {
                SessionId = sessionId,
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                SeatId = seatId,
                DeviceId = Guid.NewGuid(),
                PlayerAccountId = playerId,
                PlayerKind = "player",
                State = "Ended"
            });
            db.ClubReviews.Add(new ClubReviewEntity
            {
                ReviewId = Guid.NewGuid(),
                OrganizationId = TestIds.OrganizationId,
                BranchId = TestIds.BranchId,
                PlayerAccountId = playerId,
                SessionId = sessionId,
                Rating = reviews[index].Rating,
                Comment = reviews[index].Comment,
                // Первый в списке — самый свежий.
                CreatedAtUtc = Start.AddMinutes(-index)
            });
        }

        await db.SaveChangesAsync();
    }
}
