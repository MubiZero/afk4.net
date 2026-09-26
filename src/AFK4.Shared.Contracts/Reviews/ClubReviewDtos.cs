namespace AFK4.Shared.Contracts.Reviews;

/// A review as the club's shop window shows it: who, how many stars, and what they wrote.
public sealed record ClubReviewDto(
    Guid ReviewId,
    string AuthorName,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAtUtc);

/// The reviews page of a club: the average is what a player reads first, the reviews are why.
public sealed record ClubReviewsPageDto(
    // Пусто — оценок пока нет. Это не ноль звёзд.
    double? Rating,
    int ReviewCount,
    IReadOnlyList<ClubReviewDto> Items);

/// A finished visit that has not been reviewed yet — what the app offers to rate.
///
/// Оценить предлагается один раз и только пока вечер свежий в памяти.
public sealed record PendingClubReviewDto(
    Guid SessionId,
    string BranchName,
    string SeatName,
    DateTimeOffset EndedAtUtc);

public sealed record CreateClubReviewRequest(
    Guid SessionId,
    int Rating,
    string? Comment);

/// <summary>
/// Отзыв для клуба: кто, за каким ПК и когда — чтобы «мышь липкая» можно было найти на ПК 07,
/// а не гадать, о каком из тридцати речь.
/// </summary>
public sealed record BranchReviewDto(
    Guid ReviewId,
    Guid PlayerAccountId,
    string AuthorName,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAtUtc,
    Guid SessionId,
    string? SeatName);

/// <summary>Отзывы филиала для Панели: итог по всем оценкам и страница списка.</summary>
public sealed record BranchReviewsPageDto(
    // Пусто — оценок пока нет. Это не ноль звёзд.
    double? Rating,
    int ReviewCount,
    /// Сколько оценок на каждую звезду: [1★, 2★, 3★, 4★, 5★].
    IReadOnlyList<int> CountsByRating,
    IReadOnlyList<BranchReviewDto> Items,
    /// Следующая страница — отзывы раньше этого времени; null — дальше нет.
    DateTimeOffset? NextBefore);

