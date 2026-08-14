using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Напоминания, у которых нет события: время само подходит. Про заканчивающуюся сессию и про
/// бронь через час никто не «нажимает кнопку» — эти два повода приходится искать по часам.
///
/// Повторов бояться не нужно: ключ идемпотентности в очереди прибит к сессии и к брони, так что
/// сколько бы раз задание ни тикнуло внутри окна, игрок получит одно уведомление.
/// </summary>
public sealed class PlayerReminderRunner(
    PlatformDbContext dbContext,
    INotificationService notifications,
    TimeProvider timeProvider)
{
    /// <summary>За сколько до конца оплаченного времени предупреждаем. Успеть продлить — вот весь смысл.</summary>
    public static readonly TimeSpan SessionWarning = TimeSpan.FromMinutes(10);

    /// <summary>За сколько до брони напоминаем. Час — это «пора выходить», а не «уже опоздали».</summary>
    public static readonly TimeSpan ReservationWarning = TimeSpan.FromHours(1);

    /// <summary>
    /// Сколько тянется окно поиска. Задание тикает чаще, но если сервер простоял полчаса,
    /// напоминание всё равно уйдёт — пока не поздно.
    /// </summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await WarnAboutEndingSessionsAsync(now, cancellationToken)
            + await RemindAboutReservationsAsync(now, cancellationToken);
    }

    private async Task<int> WarnAboutEndingSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var until = now + SessionWarning;
        var ending = await dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.State == SessionStateNames.Active &&
                session.PlayerAccountId != null &&
                session.EndsAtUtc != null &&
                session.EndsAtUtc <= until &&
                session.EndsAtUtc > now - Window)
            .Select(session => new
            {
                session.SessionId,
                session.OrganizationId,
                session.BranchId,
                session.SeatId,
                session.PlayerAccountId,
                session.EndsAtUtc,
            })
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var session in ending)
        {
            var seatName = await dbContext.Seats
                .AsNoTracking()
                .Where(seat => seat.SeatId == session.SeatId)
                .Select(seat => seat.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

            var minutes = Math.Max(1, (int)Math.Round((session.EndsAtUtc!.Value - now).TotalMinutes));

            await notifications.SendAsync(
                new NotificationRequest(
                    NotificationTemplateKeys.PlayerSessionEnding,
                    // Про деньги и место — уведомление по делу, а не рассылка: игрок ждёт его,
                    // чтобы успеть продлить. Но заглушить его всё равно можно, это Operational.
                    NotificationCategory.Operational,
                    await RecipientAsync(session.PlayerAccountId!.Value, cancellationToken),
                    new Dictionary<string, string>
                    {
                        ["minutes"] = minutes.ToString(),
                        ["seat"] = seatName,
                    },
                    IdempotencyKey: $"player.session_ending:{session.SessionId}",
                    PreferredChannels: [NotificationChannel.Push],
                    OrganizationId: session.OrganizationId,
                    BranchId: session.BranchId),
                cancellationToken);
            sent++;
        }

        return sent;
    }

    private async Task<int> RemindAboutReservationsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var until = now + ReservationWarning;
        var upcoming = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                (reservation.State == ReservationStateNames.Confirmed || reservation.State == ReservationStateNames.Pending) &&
                reservation.PlayerAccountId != null &&
                reservation.StartsAtUtc <= until &&
                reservation.StartsAtUtc > now - Window)
            .Select(reservation => new
            {
                reservation.ReservationId,
                reservation.OrganizationId,
                reservation.BranchId,
                reservation.PlayerAccountId,
                reservation.StartsAtUtc,
            })
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var reservation in upcoming)
        {
            var branch = await dbContext.Branches
                .AsNoTracking()
                .Where(candidate => candidate.BranchId == reservation.BranchId)
                .Select(candidate => new { candidate.Name, TimeZone = candidate.PreferredTimeZone })
                .FirstOrDefaultAsync(cancellationToken);

            await notifications.SendAsync(
                new NotificationRequest(
                    NotificationTemplateKeys.PlayerReservationSoon,
                    NotificationCategory.Operational,
                    await RecipientAsync(reservation.PlayerAccountId!.Value, cancellationToken),
                    new Dictionary<string, string>
                    {
                        ["club"] = branch?.Name ?? string.Empty,
                        ["time"] = LocalTime(reservation.StartsAtUtc, branch?.TimeZone),
                    },
                    IdempotencyKey: $"player.reservation_soon:{reservation.ReservationId}",
                    PreferredChannels: [NotificationChannel.Push],
                    OrganizationId: reservation.OrganizationId,
                    BranchId: reservation.BranchId),
                cancellationToken);
            sent++;
        }

        return sent;
    }

    private async Task<NotificationRecipient> RecipientAsync(Guid playerAccountId, CancellationToken cancellationToken)
    {
        var locale = await dbContext.PlayerAccounts
            .AsNoTracking()
            .Where(account => account.PlayerAccountId == playerAccountId)
            .Select(account => account.PreferredLocale)
            .FirstOrDefaultAsync(cancellationToken);

        return new NotificationRecipient(locale ?? string.Empty, PlayerAccountId: playerAccountId);
    }

    /// <summary>Время брони — по часам клуба, а не по UTC: игрок собирается выходить из дома.</summary>
    private static string LocalTime(DateTimeOffset startsAtUtc, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return startsAtUtc.ToString("HH:mm");
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(startsAtUtc, zone).ToString("HH:mm");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Неизвестная зона в профиле клуба — не повод не напомнить о брони вовсе.
            return startsAtUtc.ToString("HH:mm");
        }
    }
}
