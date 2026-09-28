namespace AFK4.Shared.Contracts.Platform.Organizations;

/// <summary>
/// Сводка по программе «Приведи клуб» (ClubReferrals, спека тарифов клуба) для карточки клуба в
/// Platform Control. Раньше поддержке нечем было ответить на жалобу «обещали месяц за друга» —
/// <see cref="OrganizationDetailDto"/> не нёс ни своего кода клуба, ни того, кто его привёл, ни
/// списка приведённых.
/// </summary>
/// <param name="Code">
/// Свой код клуба. Сервер выдаёт его лениво при первом обращении к тарифу (см. ClubReferrals.
/// EnsureCodeAsync) — до этого момента null, а не пустая строка.
/// </param>
/// <param name="ReferredByOrganizationId">Кто привёл этот клуб, если он пришёл по чужому коду.</param>
/// <param name="ReferredByOrganizationName">Имя пригласившего клуба — то же условие, что и выше.</param>
/// <param name="RewardedAtUtc">
/// Когда пригласившему начислен месяц за то, что этот клуб оплатил первый счёт подписки. Null —
/// либо клуб пришёл не по коду, либо ещё не оплатил первый счёт (ClubReferrals.RewardIfFirstPaidAsync).
/// </param>
public sealed record OrganizationReferralDto(
    string? Code,
    Guid? ReferredByOrganizationId,
    string? ReferredByOrganizationName,
    DateTimeOffset? RewardedAtUtc,
    IReadOnlyList<ReferredOrganizationDto> Referred);

/// <summary>Клуб, приведённый этим клубом по его коду.</summary>
public sealed record ReferredOrganizationDto(
    Guid OrganizationId,
    string Name,
    DateTimeOffset CreatedAtUtc,
    bool Rewarded);
