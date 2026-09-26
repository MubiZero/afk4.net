namespace AFK4.Platform.Api.Data;

/// <summary>
/// A player's word about a visit. Tied to the session it is about — that is what separates a
/// review from a comment on the internet: it can only be written by someone who actually played
/// here, and only once per visit.
/// </summary>
public sealed class ClubReviewEntity
{
    public Guid ReviewId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid PlayerAccountId { get; set; }

    /// <summary>The visit being reviewed. Unique: one visit — one review.</summary>
    public Guid SessionId { get; set; }

    /// <summary>1..5. Enforced at the endpoint, not by the database.</summary>
    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Ответ клуба — виден всем вместе с отзывом.</summary>
    public string? Reply { get; set; }

    public DateTimeOffset? RepliedAtUtc { get; set; }

    public Guid? RepliedByStaffUserId { get; set; }

    /// <summary>
    /// Клуб скрыл текст от игроков. Только текст: звёзды остаются в оценке, иначе «скрыть» стало бы
    /// способом прятать плохие оценки.
    /// </summary>
    public DateTimeOffset? CommentHiddenAtUtc { get; set; }

    public Guid? CommentHiddenByStaffUserId { get; set; }

    public string? CommentHiddenReason { get; set; }
}
