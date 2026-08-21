using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Что настройки филиала значат для конкретного игрока: нужна ли предоплата ему, сколько броней
/// доступно ему.
///
/// Один расчёт на две стороны — на приём брони и на ответ приложению. Порознь они разошлись бы в
/// первый же месяц, и игрок читал бы «доступна одна бронь», получая отказ на первой же.
/// </summary>
internal static class PlayerBookingRules
{
    /// <summary>Филиал не принимает брони из приложения.</summary>
    public const string BookingDisabledCode = "booking_disabled";

    /// <summary>Клуб просит незнакомого гостя выбрать тариф и заморозить деньги.</summary>
    public const string PrepaymentRequiredCode = "prepayment_required";

    /// <summary>У новичка уже есть столько активных броней, сколько клуб ему позволяет.</summary>
    public const string ActiveReservationLimitCode = "active_reservation_limit";

    /// <summary>
    /// Отказ по решению клуба, а не по кривому запросу: такие приложение показывает словами
    /// «так решил клуб» и отвечает на них 409, а не 400.
    /// </summary>
    public static bool IsClubRuleRefusal(string? error) =>
        error is BookingDisabledCode or PrepaymentRequiredCode or ActiveReservationLimitCode;

    private static readonly string[] ActiveStates =
    [
        ReservationStateNames.Pending,
        ReservationStateNames.Confirmed
    ];

    public sealed record Evaluation(
        BranchBookingSettingsDto Settings,
        bool PrepaymentRequired,
        int ActiveBookings,
        int? MaxActiveBookings)
    {
        public bool AcceptsBookings =>
            !string.Equals(Settings.AcceptanceMode, BranchBookingAcceptanceModes.Off, StringComparison.Ordinal);

        public bool ConfirmsWithoutOperator =>
            string.Equals(Settings.AcceptanceMode, BranchBookingAcceptanceModes.Auto, StringComparison.Ordinal);

        public bool HasRoomForOneMoreBooking =>
            MaxActiveBookings is not { } limit || ActiveBookings < limit;

        public PlayerBookingRulesDto ToDto() => new(
            Settings.BranchId,
            Settings.AcceptanceMode,
            Settings.RespondWithinMinutes,
            PrepaymentRequired,
            ActiveBookings,
            MaxActiveBookings,
            Settings.HoldSeatAfterStartMinutes);
    }

    /// <summary>
    /// <paramref name="playerAccountId"/> пуст, когда счёта в этом клубе ещё нет: человек только
    /// смотрит правила перед первой бронью. Считать ему нечего — ноль визитов и ноль броней, — и
    /// это не заглушка, а правда: клубу он ровно тот новый гость, о котором говорят настройки.
    /// </summary>
    public static async Task<Evaluation> EvaluateAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var settings = await BranchBookingSettingsDefaults.ResolveAsync(
            dbContext, organizationId, branchId, cancellationToken);

        var visits = playerAccountId is { } account
            ? await CountVisitsAsync(dbContext, branchId, account, cancellationToken)
            : 0;
        var isNewGuest = settings.RegularAfterVisits > 0 && visits < settings.RegularAfterVisits;

        return new Evaluation(
            settings,
            PrepaymentRequired: isNewGuest && settings.RequirePrepaymentFromNewGuests,
            ActiveBookings: playerAccountId is { } booked
                ? await CountActiveBookingsAsync(dbContext, booked, now, cancellationToken)
                : 0,
            MaxActiveBookings: isNewGuest ? settings.MaxActiveReservationsForNewGuests : null);
    }

    /// <summary>
    /// Визит — это законченная сессия в этом филиале. Считаются именно они, а не брони: бронь
    /// показывает намерение, а знакомым клубу человека делает приход.
    /// </summary>
    private static Task<int> CountVisitsAsync(
        PlatformDbContext dbContext,
        Guid branchId,
        Guid playerAccountId,
        CancellationToken cancellationToken) =>
        dbContext.Sessions
            .AsNoTracking()
            .CountAsync(
                session =>
                    session.PlayerAccountId == playerAccountId &&
                    session.BranchId == branchId &&
                    session.State == SessionStateNames.Ended,
                cancellationToken);

    /// <summary>
    /// Бронь на компанию считается одной, а не по местам: потолок в «одну бронь» не должен
    /// запрещать новичку прийти вчетвером — это одно обещание клубу, а не четыре.
    /// </summary>
    private static async Task<int> CountActiveBookingsAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.PlayerAccountId == playerAccountId &&
                ActiveStates.Contains(reservation.State) &&
                reservation.EndsAtUtc > now)
            .Select(reservation => new { reservation.ReservationId, reservation.ReservationGroupId })
            .ToListAsync(cancellationToken);

        return active
            .Select(reservation => reservation.ReservationGroupId ?? reservation.ReservationId)
            .Distinct()
            .Count();
    }
}
