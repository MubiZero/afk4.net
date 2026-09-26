using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Reviews;
using Microsoft.EntityFrameworkCore;
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

    private static string Review(Guid reviewId, string action) =>
        $"/api/organizations/{TestIds.OrganizationId:D}/branches/{TestIds.BranchId:D}/reviews/{reviewId:D}/{action}";

    private static string PublicRoute => $"/api/public/organizations/{TestIds.OrganizationId:D}/reviews";

    [Fact]
    public async Task TheClubAnswers_AndPlayersSeeTheAnswerUnderTheReview()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var reviewId = (await SeedAsync(factory, (2, "Мышь липкая")))[0];

        var reply = await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest("  Поменяли мышь, приходите.  "));
        Assert.Equal(HttpStatusCode.NoContent, reply.StatusCode);

        var staff = Assert.Single((await client.GetFromJsonAsync<BranchReviewsPageDto>(Route()))!.Items);
        Assert.Equal("Поменяли мышь, приходите.", staff.Reply);
        Assert.NotNull(staff.RepliedAtUtc);

        using var guest = factory.CreateClient();
        var shown = Assert.Single((await guest.GetFromJsonAsync<ClubReviewsPageDto>(PublicRoute))!.Items);
        Assert.Equal("Поменяли мышь, приходите.", shown.ClubReply);
        Assert.Equal("Мышь липкая", shown.Comment);

        // Пустой ответ снимает прежний: передумали — ответа больше нет.
        await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest(" "));
        Assert.Null(Assert.Single((await guest.GetFromJsonAsync<ClubReviewsPageDto>(PublicRoute))!.Items).ClubReply);
    }

    // Игрок пишет отзыв один раз и в список отзывов клуба сам не возвращается: ответ, о котором
    // ему не сказали, он прочитает разве что случайно.
    [Fact]
    public async Task TheFirstAnswer_TellsTheAuthorWithOnePush()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var reviewId = (await SeedAsync(factory, (2, "Мышь липкая")))[0];

        var reply = await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest("Поменяли мышь, приходите."));
        Assert.Equal(HttpStatusCode.NoContent, reply.StatusCode);

        var push = Assert.Single(await RepliedPushesAsync(factory));
        Assert.Equal(await AuthorOfAsync(factory, reviewId), push.PlayerAccountId);
        Assert.Equal("Push", push.Channel);
        Assert.Equal(TestIds.OrganizationId, push.OrganizationId);
        Assert.Equal(TestIds.BranchId, push.BranchId);
        Assert.Equal("Клуб ответил на ваш отзыв", push.Subject);
        Assert.Equal("Demo Branch: Поменяли мышь, приходите.", push.BodyText);
    }

    // Правка опечатки и снятие ответа — не новость. Написать заново после снятия — тоже: пуш
    // один на отзыв, иначе клуб, который правит ответ удалением, будил бы игрока каждый раз.
    [Fact]
    public async Task EditingOrRemovingTheAnswer_SendsNothingMore()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var reviewId = (await SeedAsync(factory, (2, "Мышь липкая")))[0];
        await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest("Поменяли мыш."));
        Assert.Single(await RepliedPushesAsync(factory));

        var edited = await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest("Поменяли мышь."));
        Assert.Equal(HttpStatusCode.NoContent, edited.StatusCode);
        Assert.Single(await RepliedPushesAsync(factory));

        var removed = await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest(" "));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Single(await RepliedPushesAsync(factory));

        var again = await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest("Ждём вас снова."));
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Single(await RepliedPushesAsync(factory));
    }

    // В шторку уходит начало ответа, обрезанное по слову: целиком его читают в приложении.
    [Fact]
    public async Task ALongAnswer_ArrivesAsItsBeginning_CutBetweenWords()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var reviewId = (await SeedAsync(factory, (2, "Мышь липкая")))[0];
        var answer = "Спасибо, что написали.\nМышь поменяли в тот же вечер, коврик тоже. "
            + string.Join(' ', Enumerable.Repeat("Приходите", 30));

        await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest(answer));

        var body = Assert.Single(await RepliedPushesAsync(factory)).BodyText;
        Assert.StartsWith("Demo Branch: Спасибо, что написали. Мышь поменяли", body);
        Assert.EndsWith(" Приходите…", body);
        Assert.True(body.Length <= "Demo Branch: ".Length + PlayerPushNotifier.ReplyExcerptLength + 1, body);
    }

    [Theory]
    [InlineData("Спасибо!", 20, "Спасибо!")]
    [InlineData("  Спасибо,\n\nприходите  ", 40, "Спасибо, приходите")]
    [InlineData("Поменяли мышь, приходите", 15, "Поменяли мышь…")]
    [InlineData("Ааааааааааааааааааааа", 5, "Ааааа…")]
    public void TheExcerpt_CutsBetweenWords_AndFlattensLines(string text, int limit, string expected) =>
        Assert.Equal(expected, PlayerPushNotifier.Excerpt(text, limit));

    private static async Task<List<NotificationOutboxEntity>> RepliedPushesAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.NotificationOutbox.AsNoTracking()
            .Where(row => row.TemplateKey == NotificationTemplateKeys.PlayerReviewReplied)
            .ToListAsync();
    }

    private static async Task<Guid> AuthorOfAsync(PlatformApiFactory factory, Guid reviewId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.ClubReviews.AsNoTracking()
            .Where(review => review.ReviewId == reviewId)
            .Select(review => review.PlayerAccountId)
            .SingleAsync();
    }

    [Fact]
    public async Task ATooLongAnswer_IsRefused()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var reviewId = (await SeedAsync(factory, (5, null)))[0];

        var response = await client.PostAsJsonAsync(
            Review(reviewId, "reply"), new ReplyToReviewRequest(new string('а', ReviewLimits.ReplyMax + 1)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Оскорбление прячется от игроков, но звёзды остаются в оценке: иначе клуб убирал бы
    // неудобные единицы, а рейтинг перестал бы что-то значить.
    [Fact]
    public async Task AHiddenComment_LeavesTheStarsInTheRating()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var ids = await SeedAsync(factory, (1, "Админ — дурак, телефон его +992 900 00 00 00"), (5, "Отлично"));

        var hide = await client.PostAsJsonAsync(Review(ids[0], "hide-comment"), new HideReviewCommentRequest(ReviewHideReasonNames.PersonalData));
        Assert.Equal(HttpStatusCode.NoContent, hide.StatusCode);

        using var guest = factory.CreateClient();
        var page = (await guest.GetFromJsonAsync<ClubReviewsPageDto>(PublicRoute))!;
        Assert.Equal(3, page.Rating);
        var hidden = page.Items.Single(item => item.ReviewId == ids[0]);
        Assert.Null(hidden.Comment);
        Assert.True(hidden.CommentHidden);
        Assert.Equal(1, hidden.Rating);

        // Клуб текст по-прежнему видит — с причиной, чтобы было что вернуть.
        var staff = (await client.GetFromJsonAsync<BranchReviewsPageDto>(Route()))!.Items.Single(item => item.ReviewId == ids[0]);
        Assert.StartsWith("Админ", staff.Comment);
        Assert.Equal(ReviewHideReasonNames.PersonalData, staff.CommentHiddenReason);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync(Review(ids[0], "show-comment"), null)).StatusCode);
        var shown = (await guest.GetFromJsonAsync<ClubReviewsPageDto>(PublicRoute))!.Items.Single(item => item.ReviewId == ids[0]);
        Assert.False(shown.CommentHidden);
        Assert.StartsWith("Админ", shown.Comment);
    }

    [Fact]
    public async Task HidingNeedsAKnownReason_AndATextToHide()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, OrganizationRoleNames.OrganizationOwner);
        var ids = await SeedAsync(factory, (1, "Плохо"), (1, null));

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(Review(ids[0], "hide-comment"), new HideReviewCommentRequest("не нравится"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync(Review(ids[1], "hide-comment"), new HideReviewCommentRequest(ReviewHideReasonNames.Insult))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsJsonAsync(Review(Guid.NewGuid(), "reply"), new ReplyToReviewRequest("Спасибо"))).StatusCode);
    }

    [Theory]
    [InlineData(OrganizationRoleNames.Operator, HttpStatusCode.Forbidden)]
    [InlineData(OrganizationRoleNames.BranchManager, HttpStatusCode.NoContent)]
    public async Task OnlyThoseWhoLeadTheClub_AnswerAndHide(string role, HttpStatusCode expected)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, role);
        var reviewId = (await SeedAsync(factory, (3, "Нормально")))[0];

        Assert.Equal(expected, (await client.PostAsJsonAsync(Review(reviewId, "reply"), new ReplyToReviewRequest("Спасибо"))).StatusCode);
        Assert.Equal(expected,
            (await client.PostAsJsonAsync(Review(reviewId, "hide-comment"), new HideReviewCommentRequest(ReviewHideReasonNames.Spam))).StatusCode);
    }

    private static async Task<IReadOnlyList<Guid>> SeedAsync(PlatformApiFactory factory, params (int Rating, string? Comment)[] reviews)
    {
        var ids = new List<Guid>();
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
            var reviewId = Guid.NewGuid();
            ids.Add(reviewId);
            db.ClubReviews.Add(new ClubReviewEntity
            {
                ReviewId = reviewId,
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
        return ids;
    }
}
