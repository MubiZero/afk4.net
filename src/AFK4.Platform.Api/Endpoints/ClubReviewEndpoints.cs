using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Players;
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

    public static void MapClubReviewEndpoints(this WebApplication app)
    {
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
                        review.ReviewId, account.DisplayName, review.Rating, review.Comment, review.CreatedAtUtc))
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
        var ratings = await dbContext.ClubReviews
            .AsNoTracking()
            .Where(review => review.OrganizationId == organizationId)
            .Select(review => review.Rating)
            .ToListAsync(cancellationToken);

        return ratings.Count == 0
            ? (null, 0)
            : (Math.Round(ratings.Average(), 1), ratings.Count);
    }
}
