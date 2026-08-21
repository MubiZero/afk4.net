using AFK4.Platform.Api.Branches;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Branches;
using AFK4.Shared.Contracts.Reservations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reservations;

/// <summary>Настройки разбора неявок.</summary>
public sealed class ReservationNoShowOptions
{
    /// <summary>Как часто проверяются брони, чьё время началось.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Разбирает брони, в которые игрок не пришёл: освобождает место и решает судьбу предоплаты.
///
/// Без этого забытая бронь держала бы замороженную сумму до конца времён: игрок не пришёл, отменить
/// забыл, а деньги на кошельке выглядят как списанные.
///
/// Сколько ждать опаздывающего и оставлять ли себе предоплату — решает филиал
/// (<c>HoldSeatAfterStartMinutes</c>, <c>KeepPrepaymentOnNoShow</c>), а не зашитые в код числа:
/// клуб у вокзала и клуб в спальном районе ждут по-разному.
///
/// Неявка — своё состояние (<c>no_show</c>), а не отмена с пометкой в свободном тексте: слот
/// освобождается так же, как при отмене, но исход остаётся отличим и в журнале, и на экране
/// оператора, и в сетевой репутации.
///
/// Заявка, на которую клуб так и не ответил, неявкой не считается ни при каких настройках: человек
/// ждал ответа, ответа не было, и платить за чужое молчание он не должен. Такая заявка закрывается
/// здесь так же, как её закрыл бы <see cref="ReservationRequestExpiryRunner"/> — полным возвратом и
/// своей причиной, — чтобы она не легла в сетевую репутацию неявкой, которой не было.
/// </summary>
public sealed class ReservationNoShowRunner(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IOpenShiftResolver openShiftResolver)
{
    /// <summary>Один проход. Возвращает число разобранных броней.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Разбираются только брони, за которые заморожены деньги: у брони без холда неявка
        // ничего не стоит игроку, и трогать её автоматикой не за чем — с ней разберётся оператор.
        //
        // Отбор идёт по «время началось», а не «время началось плюс grace»: сколько именно ждать,
        // знает только филиал, и его настройки читаются ниже — по одной на филиал, а не на бронь.
        var started = await dbContext.Reservations
            .Where(reservation =>
                reservation.StartsAtUtc <= now &&
                reservation.EstimatedCostMinorUnits != null &&
                reservation.SeatedAtUtc == null &&
                (reservation.State == ReservationStateNames.Confirmed ||
                 reservation.State == ReservationStateNames.Pending))
            .ToListAsync(cancellationToken);
        if (started.Count == 0)
        {
            return 0;
        }

        var settingsByBranch = new Dictionary<Guid, BranchBookingSettingsDto>();
        var shiftByBranch = new Dictionary<Guid, Guid?>();
        var handled = 0;

        foreach (var reservation in started)
        {
            if (!settingsByBranch.TryGetValue(reservation.BranchId, out var settings))
            {
                settings = await BranchBookingSettingsDefaults.ResolveAsync(
                    dbContext, reservation.OrganizationId, reservation.BranchId, cancellationToken);
                settingsByBranch[reservation.BranchId] = settings;
            }

            if (now < reservation.StartsAtUtc.AddMinutes(settings.HoldSeatAfterStartMinutes))
            {
                continue;
            }

            // Подтверждения нет — значит клуб на заявку не ответил, и всё дальнейшее решается
            // не настройкой неявки, а этим фактом: деньги возвращаются целиком.
            if (reservation.State == ReservationStateNames.Pending)
            {
                await ReservationHold.ReleaseAsync(
                    dbContext,
                    reservation.ReservationId,
                    ReservationHoldCauses.RequestExpired,
                    now,
                    cancellationToken);

                reservation.State = ReservationStateNames.Cancelled;
                reservation.CancelReason = ReservationRequestExpiryRunner.CancelReason;
                reservation.CancelledAtUtc = now;
                // Guid.Empty — сделала система, а не сотрудник: в журнале это должно быть видно.
                reservation.UpdatedByStaffUserId = Guid.Empty;
                reservation.UpdatedAtUtc = now;
                reservation.Version++;
                handled++;
                continue;
            }

            if (!shiftByBranch.TryGetValue(reservation.BranchId, out var shiftId))
            {
                var openShift = await openShiftResolver.GetOpenShiftIdAsync(
                    reservation.OrganizationId, reservation.BranchId, cancellationToken);
                shiftId = openShift.Succeeded && openShift.Response != Guid.Empty
                    ? openShift.Response
                    : null;
                shiftByBranch[reservation.BranchId] = shiftId;
            }

            // Тем же кодом, которым неявку отмечает администратор: два представления о том, чем
            // неявка отличается от отмены, разъехались бы на первом же исправлении.
            await ReservationNoShow.MarkAsync(
                dbContext, reservation, settings, shiftId, Guid.Empty, now, cancellationToken);
            handled++;
        }

        if (handled == 0)
        {
            return 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return handled;
    }
}
