namespace AFK4.Shared.Contracts.Reviews;

/// A review as the club's shop window shows it: who, how many stars, and what they wrote.
public sealed record ClubReviewDto(
    Guid ReviewId,
    string AuthorName,
    int Rating,
    // Пусто и при скрытом клубом тексте — тогда CommentHidden.
    string? Comment,
    DateTimeOffset CreatedAtUtc,
    // Ответ клуба — виден всем, как и сам отзыв.
    string? ClubReply = null,
    DateTimeOffset? ClubRepliedAtUtc = null,
    // Клуб скрыл текст (оскорбления, чужие данные, реклама). Звёзды остаются в оценке.
    bool CommentHidden = false);

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
    string? SeatName,
    string? Reply = null,
    DateTimeOffset? RepliedAtUtc = null,
    // Текст скрыт от игроков; клуб его по-прежнему видит, чтобы вернуть, если ошибся.
    DateTimeOffset? CommentHiddenAtUtc = null,
    // Одно из ReviewHideReasonNames
    string? CommentHiddenReason = null);

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


/// <summary>Ответ клуба на отзыв. Пустой — снять ответ.</summary>
public sealed record ReplyToReviewRequest(string? Reply);

/// <summary>Скрыть текст отзыва от игроков. Звёзды остаются в оценке: скрыть плохую оценку нельзя.</summary>
public sealed record HideReviewCommentRequest(
    // Одно из ReviewHideReasonNames
    string Reason);

public static class ReviewHideReasonNames
{
    public const string Insult = "insult";

    /// <summary>Телефон, имя сотрудника, чужие данные.</summary>
    public const string PersonalData = "personal_data";

    /// <summary>Реклама, ссылки, спам.</summary>
    public const string Spam = "spam";

    public const string Other = "other";

    public static readonly IReadOnlyList<string> All = [Insult, PersonalData, Spam, Other];
}

public static class ReviewLimits
{
    public const int ReplyMax = 1000;
}
