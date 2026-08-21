using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Заморозка денег под бронь.
///
/// Холд — обычная отрицательная запись кошелька: баланс сразу показывает деньги как занятые, и
/// второй бронью их потратить уже нельзя. Ничем «особенным» он не является намеренно — весь учёт
/// остаётся одним журналом, и любая сводка баланса видит холд без отдельного знания о бронях.
///
/// Холд НИКОГДА не превращается в оплату: при посадке он снимается, и сессию считает обычный
/// биллинг. Иначе пришлось бы вести деньги в двух местах сразу и сводить их между собой — а это
/// ровно тот класс ошибок, из-за которого игроку списывают дважды.
///
/// Связь с бронью идёт через <see cref="Reason"/>, без колонки на записи журнала: холд читается
/// только по своей брони, и отдельный внешний ключ на журнал ради этого не нужен.
/// </summary>
internal static class ReservationHold
{
    public const string EntryType = LedgerEntryTypeNames.ReservationHold;

    /// <summary>Причина записи холда. По ней же он и находится при снятии.</summary>
    public static string Reason(Guid reservationId) => $"reservation_hold:{reservationId:D}";

    /// <summary>Причина снятия. Несёт то же имя брони, чтобы пара читалась в журнале глазами.</summary>
    public static string ReleaseReason(Guid reservationId, string cause) =>
        $"reservation_hold_release:{reservationId:D}:{cause}";

    public static LedgerEntryEntity Create(ReservationEntity reservation, long amountMinorUnits, string currencyCode, DateTimeOffset now) =>
        BillingEntryFactory.Create(
            reservation.OrganizationId,
            reservation.BranchId,
            reservation.PlayerAccountId!.Value,
            sessionId: null,
            playerPackageId: null,
            EntryType,
            LedgerAccountTypeNames.Wallet,
            // Отрицательная сумма: деньги уходят из доступного баланса, оставаясь у игрока.
            -amountMinorUnits,
            quantitySeconds: 0,
            currencyCode,
            description: EntryType,
            Reason(reservation.ReservationId),
            reversesLedgerEntryId: null,
            // Guid.Empty — действие игрока, а не кассира: холд ставит самообслуживание.
            actorStaffUserId: Guid.Empty,
            now);

    /// <summary>
    /// Снимает холд по брони, если он ещё держится. Идемпотентно: повторный вызов ничего не
    /// добавляет — иначе фоновая задача и посадка вернули бы деньги дважды.
    /// </summary>
    public static async Task<LedgerEntryEntity?> ReleaseAsync(
        PlatformDbContext dbContext,
        Guid reservationId,
        string cause,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var reason = Reason(reservationId);
        var hold = await dbContext.LedgerEntries
            .Where(entry => entry.EntryType == EntryType && entry.Reason == reason)
            .OrderBy(entry => entry.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (hold is null)
        {
            return null;
        }

        var alreadyReleased = await dbContext.LedgerEntries
            .AnyAsync(entry => entry.ReversesLedgerEntryId == hold.LedgerEntryId, cancellationToken);
        if (alreadyReleased)
        {
            return null;
        }

        var release = BillingEntryFactory.Create(
            hold.OrganizationId,
            hold.BranchId,
            hold.PlayerAccountId,
            sessionId: null,
            playerPackageId: null,
            LedgerEntryTypeNames.Reversal,
            LedgerAccountTypeNames.Wallet,
            -hold.AmountMinorUnits,
            quantitySeconds: 0,
            hold.CurrencyCode,
            description: LedgerEntryTypeNames.Reversal,
            ReleaseReason(reservationId, cause),
            reversesLedgerEntryId: hold.LedgerEntryId,
            actorStaffUserId: Guid.Empty,
            now);

        dbContext.LedgerEntries.Add(release);
        return release;
    }

    /// <summary>Причина удержания. По ней удержание находится в журнале рядом со своей бронью.</summary>
    public static string NoShowFeeReason(Guid reservationId) =>
        $"reservation_no_show_fee:{reservationId:D}";

    /// <summary>
    /// Удержание предоплаты за неявку — выручка клуба.
    ///
    /// Пишется всегда парой к снятию заморозки, а не вместо него: «деньги просто не вернулись» —
    /// это состояние, которое нельзя объяснить и нельзя посчитать в кассе. Здесь же в журнале
    /// читается «заморозка снята» и «удержано за неявку», кошелёк остаётся там, где был при живой
    /// брони, а смена получает строку выручки.
    ///
    /// <paramref name="shiftId"/> — смена, при которой это случилось, или <c>null</c>, если клуб
    /// в этот момент был закрыт: ни одна смена за такое удержание не отвечала.
    /// </summary>
    public static LedgerEntryEntity CreateNoShowFee(
        LedgerEntryEntity release,
        Guid reservationId,
        Guid? shiftId,
        DateTimeOffset now) =>
        BillingEntryFactory.Create(
            release.OrganizationId,
            release.BranchId,
            release.PlayerAccountId,
            sessionId: null,
            playerPackageId: null,
            LedgerEntryTypeNames.ReservationNoShowFee,
            LedgerAccountTypeNames.Wallet,
            // Снятие вернуло сумму в остаток — удержание забирает ровно её же.
            -release.AmountMinorUnits,
            quantitySeconds: 0,
            release.CurrencyCode,
            description: LedgerEntryTypeNames.ReservationNoShowFee,
            NoShowFeeReason(reservationId),
            reversesLedgerEntryId: null,
            // Guid.Empty — удержала автоматика по правилу филиала, а не кассир.
            actorStaffUserId: Guid.Empty,
            now,
            shiftId);
}

/// <summary>Почему холд сняли. Пишется в журнал, чтобы деньги можно было объяснить без догадок.</summary>
internal static class ReservationHoldCauses
{
    public const string Cancelled = "cancelled";
    public const string Seated = "seated";
    public const string NoShow = "no_show";

    /// <summary>Клуб не ответил на заявку в обещанный срок. Возврат всегда полный.</summary>
    public const string RequestExpired = "request_expired";
}
