using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reviews;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

/// Отзыв привязан к визиту — этим он и отличается от комментария в интернете: написать его может
/// только тот, кто здесь играл, и только один раз про один вечер.
public sealed class ClubReviewEndpointTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private sealed record ReviewContext(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone, Guid SessionId);

    private static async Task<ReviewContext> SeedVisitAsync(
        PlatformApiFactory factory,
        string state = SessionStateNames.Ended,
        DateTimeOffset? endedAtUtc = null,
        string displayName = "Иван",
        ReviewContext? sameClubAs = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var orgId = sameClubAs?.OrgId ?? Guid.NewGuid();
        var branchId = sameClubAs?.BranchId ?? Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var seatId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var phone = TestPhones.Next();

        if (sameClubAs is null)
        {
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = orgId,
                Slug = $"club-{orgId.ToString("N")[..6]}",
                Name = "CyberX",
                Status = "active",
                CreatedAtUtc = Now,
                UpdatedAtUtc = Now
            });
            db.Branches.Add(new BranchEntity
            {
                BranchId = branchId,
                OrganizationId = orgId,
                Slug = "main",
                Name = "На Рудаки",
                City = "Душанбе",
                CreatedAtUtc = Now
            });
        }

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId,
            OrganizationId = orgId,
            HomeBranchId = branchId,
            DisplayName = displayName,
            PhoneNumber = phone,
            IsActive = true,
            CreatedAtUtc = Now
        });

        db.Seats.Add(new SeatEntity
        {
            SeatId = seatId,
            OrganizationId = orgId,
            BranchId = branchId,
            ZoneId = Guid.NewGuid(),
            Name = "PC-07",
            SortOrder = 1,
            CreatedAtUtc = Now
        });

        db.Sessions.Add(new SessionEntity
        {
            SessionId = sessionId,
            OrganizationId = orgId,
            BranchId = branchId,
            SeatId = seatId,
            DeviceId = Guid.NewGuid(),
            CreatedByStaffUserId = Guid.NewGuid(),
            PlayerKind = "account",
            PlayerAccountId = playerId,
            State = state,
            RequestedAtUtc = Now.AddHours(-3),
            StartedAtUtc = Now.AddHours(-3),
            EndedAtUtc = state == SessionStateNames.Ended ? endedAtUtc ?? Now.AddHours(-1) : null
        });

        await db.SaveChangesAsync();
        await PlayerPinTestData.AttachPersonWithPinAsync(factory, playerId, phone, "1234");
        return new ReviewContext(orgId, branchId, playerId, phone, sessionId);
    }

    private static async Task AuthenticateAsync(HttpClient client, ReviewContext context)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(context.OrgId, context.Phone, "1234"));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    [Fact]
    public async Task PendingReview_OffersTheVisitThatHasNotBeenRatedYet()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedVisitAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        var pending = await client.GetFromJsonAsync<PendingClubReviewDto>("/api/me/reviews/pending");

        Assert.NotNull(pending);
        Assert.Equal(context.SessionId, pending!.SessionId);
        Assert.Equal("PC-07", pending.SeatName);
        Assert.Equal("На Рудаки", pending.BranchName);
    }

    /// Приглашение оценить вечер, которого не было, раздражает сильнее, чем его отсутствие.
    [Fact]
    public async Task PendingReview_SaysNothingWhenTheVisitIsStillRunning()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedVisitAsync(factory, state: SessionStateNames.Active);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        var response = await client.GetAsync("/api/me/reviews/pending");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// Через месяц вопрос «как вам вчера?» звучит нелепо, а оценка по такой памяти ничего не стоит.
    [Fact]
    public async Task PendingReview_ForgetsVisitsOlderThanTwoWeeks()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedVisitAsync(factory, endedAtUtc: Now.AddDays(-30));
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        var response = await client.GetAsync("/api/me/reviews/pending");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task PostReview_StoresRatingAndStopsAskingAboutThatVisit()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedVisitAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        var response = await client.PostAsJsonAsync(
            "/api/me/reviews", new CreateClubReviewRequest(context.SessionId, 5, "Отличный зал"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var summary = await response.Content.ReadFromJsonAsync<ClubReviewsPageDto>();
        Assert.Equal(5, summary!.Rating);
        Assert.Equal(1, summary.ReviewCount);

        var pending = await client.GetAsync("/api/me/reviews/pending");
        Assert.Equal(HttpStatusCode.NoContent, pending.StatusCode);
    }

    /// Один визит — один отзыв: иначе один игрок нарисует клубу любую среднюю оценку.
    [Fact]
    public async Task PostReview_SecondTimeAboutTheSameVisit_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedVisitAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        await client.PostAsJsonAsync("/api/me/reviews", new CreateClubReviewRequest(context.SessionId, 5, null));
        var again = await client.PostAsJsonAsync("/api/me/reviews", new CreateClubReviewRequest(context.SessionId, 1, null));

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// Отзыв о чужом визите — это отзыв о клубе, в котором не был. Чужая сессия отсюда
    /// неотличима от несуществующей, и остаётся неотличимой в ответе.
    [Fact]
    public async Task PostReview_AboutSomeoneElsesVisit_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        var mine = await SeedVisitAsync(factory);
        var stranger = await SeedVisitAsync(factory, sameClubAs: mine);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, mine);

        var response = await client.PostAsJsonAsync(
            "/api/me/reviews", new CreateClubReviewRequest(stranger.SessionId, 1, null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task PostReview_RatingOutsideTheScale_ReturnsBadRequest(int rating)
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedVisitAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);

        var response = await client.PostAsJsonAsync(
            "/api/me/reviews", new CreateClubReviewRequest(context.SessionId, rating, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// Отзывы читают до входа в клуб — за этим их и пишут.
    [Fact]
    public async Task PublicReviews_ShowTheAverageAndWhatPlayersWrote()
    {
        await using var factory = new PlatformApiFactory();
        var first = await SeedVisitAsync(factory, displayName: "Иван");
        var second = await SeedVisitAsync(factory, displayName: "Пётр", sameClubAs: first);

        using var author = factory.CreateClient();
        await AuthenticateAsync(author, first);
        await author.PostAsJsonAsync("/api/me/reviews", new CreateClubReviewRequest(first.SessionId, 5, "Отличный зал"));

        using var secondAuthor = factory.CreateClient();
        await AuthenticateAsync(secondAuthor, second);
        await secondAuthor.PostAsJsonAsync("/api/me/reviews", new CreateClubReviewRequest(second.SessionId, 4, null));

        using var guest = factory.CreateClient();
        var page = await guest.GetFromJsonAsync<ClubReviewsPageDto>(
            $"/api/public/organizations/{first.OrgId:D}/reviews");

        Assert.Equal(4.5, page!.Rating);
        Assert.Equal(2, page.ReviewCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, review => review.AuthorName == "Иван" && review.Comment == "Отличный зал");
    }

    /// Оценка — часть витрины: по ней выбирают клуб наравне с ценой и адресом.
    [Fact]
    public async Task Directory_CarriesTheClubRating()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedVisitAsync(factory);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, context);
        await client.PostAsJsonAsync("/api/me/reviews", new CreateClubReviewRequest(context.SessionId, 4, null));

        using var guest = factory.CreateClient();
        var catalogue = await guest.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        var club = Assert.Single(catalogue!, entry => entry.OrganizationId == context.OrgId);
        Assert.Equal(4, club.Rating);
        Assert.Equal(1, club.ReviewCount);
    }

    /// Ни одного отзыва — это «оценок пока нет», а не «ноль звёзд».
    [Fact]
    public async Task Directory_LeavesRatingEmptyUntilSomeoneWritesAReview()
    {
        await using var factory = new PlatformApiFactory();
        var context = await SeedVisitAsync(factory);

        using var guest = factory.CreateClient();
        var catalogue = await guest.GetFromJsonAsync<OrganizationDirectoryEntryDto[]>("/api/public/organizations");

        var club = Assert.Single(catalogue!, entry => entry.OrganizationId == context.OrgId);
        Assert.Null(club.Rating);
        Assert.Equal(0, club.ReviewCount);
    }
}
