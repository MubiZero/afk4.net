using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Platform.Api.Players;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Reviews;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

/// Отзывы о клубе. Отзыв привязан к визиту — этим он и отличается от комментария в интернете:
/// написать его может только тот, кто здесь играл, и только один раз про один вечер.
internal static class ClubReviewEndpoints
{
    /// Насколько свежим должен быть визит, чтобы про него ещё спрашивали. Через месяц вопрос
    /// «как вам вчера?» звучит нелепо, а оценка по такой памяти ничего не стоит.
    private static readonly TimeSpan RecentVisit = TimeSpan.FromDays(14);

    private const int MaxCommentLength = 1000;
    private const int PublicPageSize = 20;

    /// <summary>Страница отзывов в Панели: больше публичной — клубу нужна вся история, а не витрина.</summary>
    private const int StaffPageSize = 50;

    public static void MapClubReviewEndpoints(this WebApplication app, IEndpointRouteBuilder organizations)
    {
        // Отзывы филиала для Панели: итог, разбивка по звёздам и страница с ПК, за которым сидели.
        organizations.MapGet("branches/{branchId:guid}/reviews", async (
            Guid branchId,
            int? rating,
            bool? withComment,
            DateTimeOffset? before,
            StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, OrganizationPermissionNames.ViewReviews, cancellationToken);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (rating is < 1 or > 5) return Results.BadRequest(new { Error = "rating is 1..5." });

            var organizationId = authorization.StaffContext!.OrganizationId;
            var all = dbContext.ClubReviews.AsNoTracking()
                .Where(review => review.OrganizationId == organizationId && review.BranchId == branchId);
            var counts = await all.GroupBy(review => review.Rating)
                .Select(group => new { Rating = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
            var total = counts.Sum(entry => entry.Count);
            double? average = total == 0
                ? null
                : Math.Round(counts.Sum(entry => entry.Rating * (double)entry.Count) / total, 1);

            var filtered = all;
            if (rating is { } stars) filtered = filtered.Where(review => review.Rating == stars);
            if (withComment == true) filtered = filtered.Where(review => review.Comment != null && review.Comment != "");
            if (before is { } cursor) filtered = filtered.Where(review => review.CreatedAtUtc < cursor);

            var page = await filtered
                .OrderByDescending(review => review.CreatedAtUtc)
                .Take(StaffPageSize + 1)
                .Select(review => new BranchReviewDto(
                    review.ReviewId,
                    review.PlayerAccountId,
                    dbContext.PlayerAccounts.Where(account => account.PlayerAccountId == review.PlayerAccountId)
                        .Select(account => account.DisplayName).FirstOrDefault() ?? string.Empty,
                    review.Rating,
                    review.Comment,
                    review.CreatedAtUtc,
                    review.SessionId,
                    dbContext.Sessions.Where(session => session.SessionId == review.SessionId)
                        .SelectMany(session => dbContext.Seats.Where(seat => seat.SeatId == session.SeatId).Select(seat => seat.Name))
                        .FirstOrDefault(),
                    review.Reply,
                    review.RepliedAtUtc,
                    review.CommentHiddenAtUtc,
                    review.CommentHiddenReason))
                .ToListAsync(cancellationToken);

            var more = page.Count > StaffPageSize;
            var items = more ? page.Take(StaffPageSize).ToList() : page;
            return Results.Ok(new BranchReviewsPageDto(
                average,
                total,
                Enumerable.Range(1, 5).Select(stars => counts.FirstOrDefault(entry => entry.Rating == stars)?.Count ?? 0).ToList(),
                items,
                more ? items[^1].CreatedAtUtc : null));
        }).AllowPlatformSupportAccess(OrganizationPermissionNames.ViewReviews);

        // Ответ клуба на отзыв — виден игрокам вместе с отзывом. Пустой ответ снимает прежний.
        // Автору уходит пуш, но только о первом ответе: правка опечатки или снятие ответа —
        // не новость, и будить ради неё телефон незачем.
        organizations.MapPost("branches/{branchId:guid}/reviews/{reviewId:guid}/reply", (
            Guid branchId, Guid reviewId, ReplyToReviewRequest request, StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext, IAuditRecordWriter audit, PlayerPushNotifier playerPush, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var firstReply = false;
            return ModerateAsync(branchId, reviewId, authorizationService, dbContext, audit, AuditActionNames.ReplyToReview, cancellationToken,
                (review, actor) =>
                {
                    var reply = string.IsNullOrWhiteSpace(request.Reply) ? null : request.Reply.Trim();
                    if (reply is { Length: > ReviewLimits.ReplyMax }) return $"A reply is at most {ReviewLimits.ReplyMax} characters.";
                    firstReply = string.IsNullOrWhiteSpace(review.Reply) && reply is not null;
                    review.Reply = reply;
                    review.RepliedAtUtc = reply is null ? null : clock.GetUtcNow();
                    review.RepliedByStaffUserId = reply is null ? null : actor;
                    return null;
                },
                review => firstReply
                    ? playerPush.ReviewRepliedAsync(
                        review.PlayerAccountId, review.OrganizationId, review.BranchId, review.ReviewId, review.Reply!, cancellationToken)
                    : Task.CompletedTask);
        });

        // Скрыть текст отзыва от игроков: оскорбления, чужие данные, реклама. Звёзды остаются в оценке.
        organizations.MapPost("branches/{branchId:guid}/reviews/{reviewId:guid}/hide-comment", (
            Guid branchId, Guid reviewId, HideReviewCommentRequest request, StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext, IAuditRecordWriter audit, TimeProvider clock, CancellationToken cancellationToken) =>
            ModerateAsync(branchId, reviewId, authorizationService, dbContext, audit, AuditActionNames.HideReviewComment, cancellationToken,
                (review, actor) =>
                {
                    if (!ReviewHideReasonNames.All.Contains(request.Reason)) return "Unknown reason.";
                    if (string.IsNullOrWhiteSpace(review.Comment)) return "The review has no text to hide.";
                    review.CommentHiddenAtUtc = clock.GetUtcNow();
                    review.CommentHiddenByStaffUserId = actor;
                    review.CommentHiddenReason = request.Reason;
                    return null;
                }));

        organizations.MapPost("branches/{branchId:guid}/reviews/{reviewId:guid}/show-comment", (
            Guid branchId, Guid reviewId, StaffAuthorizationService authorizationService,
            PlatformDbContext dbContext, IAuditRecordWriter audit, CancellationToken cancellationToken) =>
            ModerateAsync(branchId, reviewId, authorizationService, dbContext, audit, AuditActionNames.ShowReviewComment, cancellationToken,
                (review, _) =>
                {
                    review.CommentHiddenAtUtc = null;
                    review.CommentHiddenByStaffUserId = null;
                    review.CommentHiddenReason = null;
                    return null;
                }));

        // Витрина клуба до входа: средняя оценка и последние отзывы. Публично — их и читают
        // до того, как выбрать клуб.
        app.MapGet("/api/public/organizations/{organizationId:guid}/reviews", async (
            Guid organizationId,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var isActive = await dbContext.Organizations
                .AsNoTracking()
                .AnyAsync(o => o.OrganizationId == organizationId && o.Status == "active", cancellationToken);
            if (!isActive)
            {
                return Results.NotFound();
            }

            var reviews = await dbContext.ClubReviews
                .AsNoTracking()
                .Where(review => review.OrganizationId == organizationId)
                .OrderByDescending(review => review.CreatedAtUtc)
                .Take(PublicPageSize)
                .Join(
                    dbContext.PlayerAccounts.AsNoTracking(),
                    review => review.PlayerAccountId,
                    account => account.PlayerAccountId,
                    (review, account) => new ClubReviewDto(
                        review.ReviewId, account.DisplayName, review.Rating,
                        review.CommentHiddenAtUtc == null ? review.Comment : null,
                        review.CreatedAtUtc, review.Reply, review.RepliedAtUtc, review.CommentHiddenAtUtc != null))
                .ToListAsync(cancellationToken);

            var summary = await SummarizeAsync(dbContext, organizationId, cancellationToken);
            return Results.Ok(new ClubReviewsPageDto(summary.Rating, summary.Count, reviews));
        }).RequireRateLimiting("player-public");

        // Что предложить оценить. Пусто — значит спрашивать не о чем, и приложение молчит:
        // приглашение оценить визит, которого не было, раздражает сильнее, чем его отсутствие.
        app.MapGet("/api/me/reviews/pending", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var since = timeProvider.GetUtcNow() - RecentVisit;
            var visit = await dbContext.Sessions
                .AsNoTracking()
                .Where(session =>
                    session.PlayerAccountId == player.PlayerAccountId &&
                    session.State == SessionStateNames.Ended &&
                    session.EndedAtUtc != null &&
                    session.EndedAtUtc >= since &&
                    !dbContext.ClubReviews.Any(review => review.SessionId == session.SessionId))
                .OrderByDescending(session => session.EndedAtUtc)
                .Select(session => new { session.SessionId, session.BranchId, session.SeatId, session.EndedAtUtc })
                .FirstOrDefaultAsync(cancellationToken);

            if (visit is null)
            {
                // Пусто — 204, а не пустое тело со «200 OK»: клиенту не приходится разбирать,
                // отзыв ли ему предлагают или ответ не доехал.
                return Results.NoContent();
            }

            var branchName = await dbContext.Branches.AsNoTracking()
                .Where(branch => branch.BranchId == visit.BranchId)
                .Select(branch => branch.Name)
                .FirstOrDefaultAsync(cancellationToken);
            var seatName = await dbContext.Seats.AsNoTracking()
                .Where(seat => seat.SeatId == visit.SeatId)
                .Select(seat => seat.Name)
                .FirstOrDefaultAsync(cancellationToken);

            return Results.Ok(new PendingClubReviewDto(
                visit.SessionId,
                branchName ?? string.Empty,
                seatName ?? string.Empty,
                visit.EndedAtUtc!.Value));
        }).RequireRateLimiting("player-me");

        // Стаж игрока: уровень и достижения. Живёт рядом с отзывами, потому что «оставил отзыв» —
        // одно из достижений, и считаются они из одной и той же истории визитов.
        app.MapGet("/api/me/achievements", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var achievements = await PlayerAchievementsProjector.GetAsync(
                dbContext, player.PlayerAccountId, cancellationToken);
            return Results.Ok(achievements);
        }).RequireRateLimiting("player-me");

        app.MapPost("/api/me/reviews", async (
            CreateClubReviewRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            if (request.Rating is < 1 or > 5)
            {
                return Results.BadRequest(new { error = "invalid_rating" });
            }

            var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
            if (comment is { Length: > MaxCommentLength })
            {
                return Results.BadRequest(new { error = "comment_too_long" });
            }

            // Отзыв пишут о своём визите. Чужая сессия отсюда неотличима от несуществующей —
            // и остаётся неотличимой в ответе: 404, а не «эта сессия не ваша».
            var visit = await dbContext.Sessions
                .AsNoTracking()
                .Where(session =>
                    session.SessionId == request.SessionId &&
                    session.PlayerAccountId == player.PlayerAccountId &&
                    session.State == SessionStateNames.Ended)
                .Select(session => new { session.OrganizationId, session.BranchId })
                .FirstOrDefaultAsync(cancellationToken);

            if (visit is null)
            {
                return Results.NotFound();
            }

            var alreadyReviewed = await dbContext.ClubReviews
                .AnyAsync(review => review.SessionId == request.SessionId, cancellationToken);
            if (alreadyReviewed)
            {
                return Results.Conflict(new { error = "already_reviewed" });
            }

            dbContext.ClubReviews.Add(new ClubReviewEntity
            {
                ReviewId = Guid.NewGuid(),
                OrganizationId = visit.OrganizationId,
                BranchId = visit.BranchId,
                PlayerAccountId = player.PlayerAccountId,
                SessionId = request.SessionId,
                Rating = request.Rating,
                Comment = comment,
                CreatedAtUtc = timeProvider.GetUtcNow()
            });
            await dbContext.SaveChangesAsync(cancellationToken);

            var summary = await SummarizeAsync(dbContext, visit.OrganizationId, cancellationToken);
            return Results.Ok(new ClubReviewsPageDto(summary.Rating, summary.Count, []));
        }).RequireRateLimiting("player-me");
    }

    /// Средняя оценка считается по всем отзывам клуба, а не по показанной странице: иначе
    /// «4,8» относилось бы к двадцати последним, а выглядело бы как оценка клуба целиком.
    private static async Task<(double? Rating, int Count)> SummarizeAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        // Считает база, а не мы: у популярного клуба это тысячи оценок, и поднимать их в
        // память ради одного числа значит платить всей историей клуба за каждое открытие его
        // карточки — на публичном маршруте, без авторизации.
        var reviews = dbContext.ClubReviews
            .AsNoTracking()
            .Where(review => review.OrganizationId == organizationId);

        var count = await reviews.CountAsync(cancellationToken);
        if (count == 0)
        {
            return (null, 0);
        }

        var average = await reviews.AverageAsync(review => (double)review.Rating, cancellationToken);
        return (Math.Round(average, 1), count);
    }

    /// <summary>
    /// Действие клуба над отзывом своего филиала: проверка права, само действие, журнал.
    /// <paramref name="afterSaved"/> зовётся последним, когда действие уже записано и попало в
    /// журнал: то, что идёт следом (пуш автору), не может ни отменить его, ни потерять запись.
    /// </summary>
    private static async Task<IResult> ModerateAsync(
        Guid branchId, Guid reviewId, StaffAuthorizationService authorizationService, PlatformDbContext dbContext,
        IAuditRecordWriter audit, string action, CancellationToken cancellationToken,
        Func<ClubReviewEntity, Guid, string?> apply, Func<ClubReviewEntity, Task>? afterSaved = null)
    {
        var authorization = await authorizationService.RequireBranchPermissionAsync(
            branchId, OrganizationPermissionNames.ManageReviews, cancellationToken);
        if (!authorization.IsAuthenticated) return Results.Unauthorized();
        if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

        var organizationId = authorization.StaffContext!.OrganizationId;
        var review = await dbContext.ClubReviews.SingleOrDefaultAsync(
            candidate => candidate.ReviewId == reviewId && candidate.OrganizationId == organizationId && candidate.BranchId == branchId,
            cancellationToken);
        if (review is null) return Results.NotFound();

        var actor = authorization.StaffContext.StaffUserId;
        if (apply(review, actor) is { } invalid) return Results.BadRequest(new { Error = invalid });
        await dbContext.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditRecordWriteRequest(
            organizationId, branchId, ActorStaffUserId: actor, Action: action, TargetType: "ClubReview",
            TargetId: reviewId.ToString("N"), Outcome: AuditOutcome.Succeeded, SourceApp: "OrganizationAdmin",
            DetailsJson: System.Text.Json.JsonSerializer.Serialize(new { review.Reply, review.CommentHiddenReason })), cancellationToken);
        if (afterSaved is not null) await afterSaved(review);
        return Results.NoContent();
    }
}
