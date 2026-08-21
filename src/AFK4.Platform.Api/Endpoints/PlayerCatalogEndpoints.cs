using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Packages;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

internal static class PlayerCatalogEndpoints
{
    public static void MapPlayerCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/me/branches/{branchId:guid}/tariffs", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            IOperatorReferenceDataService referenceData,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            if (!await BranchInOrgAsync(dbContext, branchId, player.OrganizationId, ct)) return Results.NotFound();
            return Results.Ok(await referenceData.GetTariffOptionsAsync(player.OrganizationId, branchId, ct));
        }).RequireRateLimiting("player-me");

        app.MapGet("/api/me/branches/{branchId:guid}/packages", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            IOperatorReferenceDataService referenceData,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            if (!await BranchInOrgAsync(dbContext, branchId, player.OrganizationId, ct)) return Results.NotFound();
            return Results.Ok(await referenceData.GetPackageOptionsAsync(player.OrganizationId, branchId, ct));
        }).RequireRateLimiting("player-me");

        // Покупка пакета самим игроком. Открытой смены не требует намеренно: пакет — это
        // предоплаченное время, деньги уже лежат в кошельке и просто переходят в часы, а
        // предоплачивать игрок хочет как раз тогда, когда до клуба ещё не дошёл.
        app.MapPost("/api/me/branches/{branchId:guid}/packages/{packageDefinitionId:guid}/purchase", async (
            Guid branchId,
            Guid packageDefinitionId,
            PurchasePackageFromAppRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            IPackageService packageService,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                return Results.BadRequest(new { error = "idempotency_key_required" });
            }

            if (!await BranchInOrgAsync(dbContext, branchId, player.OrganizationId, ct)) return Results.NotFound();

            var result = await packageService.PurchasePackageAsPlayerAsync(
                player.PlayerAccountId,
                branchId,
                new PurchasePackageRequest(player.OrganizationId, packageDefinitionId, request.IdempotencyKey),
                ct);

            if (result.NotFound) return Results.NotFound();
            if (!result.Succeeded) return Results.Conflict(new { error = result.Error });
            return Results.Ok(result.Response);
        }).RequireRateLimiting("player-me");

        // Свои купленные пакеты с остатком времени. Отдаются все, включая истёкшие и потраченные:
        // «куда делись мои часы» — такой же законный вопрос, как «сколько осталось», а исчезнувшая
        // из списка покупка читается как пропажа денег.
        app.MapGet("/api/me/packages", async (
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var packages = await dbContext.PlayerPackages
                .AsNoTracking()
                .Where(package =>
                    package.PlayerAccountId == player.PlayerAccountId &&
                    package.OrganizationId == player.OrganizationId)
                .OrderByDescending(package => package.PurchasedAtUtc)
                .ToListAsync(ct);

            var response = new List<PlayerPackageDto>(packages.Count);
            foreach (var package in packages)
            {
                var remaining = await LedgerBalanceProjector.GetPackageRemainingSecondsAsync(
                    dbContext,
                    package.PlayerPackageId,
                    ct);

                response.Add(new PlayerPackageDto(
                    package.PlayerPackageId,
                    package.PackageDefinitionId,
                    package.PlayerAccountId,
                    package.Name,
                    new MoneyDto(package.CurrencyCode, package.PurchasedPriceMinorUnits),
                    package.IncludedSeconds,
                    package.BonusSeconds,
                    remaining.IncludedSeconds,
                    remaining.BonusSeconds,
                    package.PurchasedAtUtc,
                    package.ExpiresAtUtc));
            }

            return Results.Ok(response);
        }).RequireRateLimiting("player-me");

        // Правила брони этого филиала для этого игрока. Тем же расчётом, которым сервер брони и
        // принимает, — иначе приложение обещало бы одно, а отказ приходил бы по другому правилу.
        //
        // Счёта в клубе может ещё не быть: предоплата, ручной приём и потолок броней нужнее всего
        // тому, кто только собирается забронировать впервые. Такому человеку числа считаются как
        // новому гостю — ноль визитов, ноль активных броней, — а счёта чтение правил не открывает.
        app.MapGet("/api/me/branches/{branchId:guid}/booking-rules", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            IPlatformPersonContextAccessor personContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            var organizationId = player?.OrganizationId ?? personContextAccessor.Current?.SelectedOrganizationId;
            if (organizationId is null) return Results.Unauthorized();
            if (!await BranchInOrgAsync(dbContext, branchId, organizationId.Value, ct)) return Results.NotFound();

            var rules = await PlayerBookingRules.EvaluateAsync(
                dbContext,
                organizationId.Value,
                branchId,
                player?.PlayerAccountId,
                timeProvider.GetUtcNow(),
                ct);

            return Results.Ok(rules.ToDto());
        }).RequireRateLimiting("player-me").AllowsGuestWithoutClubAccount();

        // Места зала: за какое можно сесть прямо сейчас и какое занято.
        //
        // Занятые места не прячутся: «PC-07 занят» — это ответ, а пропавшее из списка место
        // выглядит как сбой приложения. Показываются только места с привязанным и принятым
        // компьютером — за место без машины сесть всё равно нельзя.
        app.MapGet("/api/me/branches/{branchId:guid}/seats", async (
            Guid branchId,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();
            if (!await BranchInOrgAsync(dbContext, branchId, player.OrganizationId, ct)) return Results.NotFound();

            var now = timeProvider.GetUtcNow();

            var seats = await (
                from assignment in dbContext.DeviceSeatAssignments.AsNoTracking()
                join device in dbContext.Devices.AsNoTracking() on assignment.DeviceId equals device.DeviceId
                join seat in dbContext.Seats.AsNoTracking() on assignment.SeatId equals seat.SeatId
                join zone in dbContext.Zones.AsNoTracking() on seat.ZoneId equals zone.ZoneId
                where assignment.BranchId == branchId &&
                      assignment.OrganizationId == player.OrganizationId &&
                      assignment.DetachedAtUtc == null &&
                      device.EnrollmentState == DeviceEnrollmentStateNames.Approved &&
                      device.Role == DeviceRoleNames.GamingPc
                orderby zone.SortOrder, seat.SortOrder, seat.Name
                select new
                {
                    seat.SeatId,
                    device.DeviceId,
                    SeatName = seat.Name,
                    ZoneName = zone.Name,
                    device.IsOnline
                }).ToListAsync(ct);

            var seatIds = seats.Select(seat => seat.SeatId).ToList();

            var busySeatIds = await dbContext.Sessions.AsNoTracking()
                .Where(session =>
                    seatIds.Contains(session.SeatId) &&
                    (session.State == SessionStateNames.Active ||
                     session.State == SessionStateNames.Paused ||
                     session.State == SessionStateNames.Ending))
                .Select(session => session.SeatId)
                .Distinct()
                .ToListAsync(ct);

            // Чужая бронь на ближайший час — тоже причина не пускать: место обещано другому,
            // и посадить сюда игрока значит поссорить клуб с обоими.
            var soon = now.AddHours(1);
            var reservedSeatIds = await dbContext.Reservations.AsNoTracking()
                .Where(reservation =>
                    reservation.SeatId != null &&
                    seatIds.Contains(reservation.SeatId!.Value) &&
                    reservation.PlayerAccountId != player.PlayerAccountId &&
                    (reservation.State == ReservationStateNames.Confirmed ||
                     reservation.State == ReservationStateNames.Pending) &&
                    reservation.StartsAtUtc < soon &&
                    reservation.EndsAtUtc > now)
                .Select(reservation => reservation.SeatId!.Value)
                .Distinct()
                .ToListAsync(ct);

            var busy = busySeatIds.ToHashSet();
            var reserved = reservedSeatIds.ToHashSet();

            return Results.Ok(seats
                .Select(seat =>
                {
                    var reason = busy.Contains(seat.SeatId) ? PlayerSeatUnavailableReasons.Session
                        : reserved.Contains(seat.SeatId) ? PlayerSeatUnavailableReasons.Reservation
                        : !seat.IsOnline ? PlayerSeatUnavailableReasons.Offline
                        : null;
                    return new PlayerSeatDto(
                        seat.SeatId, seat.SeatName, seat.ZoneName, reason is null, reason);
                })
                .ToList());
        }).RequireRateLimiting("player-me");

        // Сколько будет стоить бронь по выбранному тарифу — до того, как игрок её подтвердит.
        // Считает сервер, а не приложение: минимальное оплачиваемое время и шаг округления живут в
        // биллинге, и вторая реализация в клиенте разошлась бы с настоящим списанием.
        app.MapPost("/api/me/reservations/quote", async (
            ReservationQuoteRequest request,
            IPlayerContextAccessor playerContextAccessor,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            if (request.StartsAtUtc >= request.EndsAtUtc)
            {
                return Results.BadRequest(new { error = "invalid_duration" });
            }

            if (!PlayerReservationGroupLimits.IsAllowedSeatCount(request.SeatCount))
            {
                return Results.BadRequest(new { error = "invalid_seat_count" });
            }

            var now = timeProvider.GetUtcNow();
            var row = await dbContext.TariffVersions.AsNoTracking()
                .Where(version =>
                    version.TariffVersionId == request.TariffVersionId &&
                    version.OrganizationId == player.OrganizationId &&
                    version.EffectiveFromUtc <= now &&
                    (version.RetiredAtUtc == null || version.RetiredAtUtc > now))
                .Join(
                    dbContext.Tariffs.AsNoTracking(),
                    version => version.TariffId,
                    tariff => tariff.TariffId,
                    (version, tariff) => new { Version = version, tariff.Name })
                .FirstOrDefaultAsync(ct);

            // Снятая с публикации или чужая версия — не повод посчитать по «похожему» тарифу:
            // приложение должно перечитать прайс, а не показать цену, которой уже нет.
            if (row is null) return Results.NotFound(new { error = "invalid_tariff" });

            var requestedMinutes = (int)Math.Round((request.EndsAtUtc - request.StartsAtUtc).TotalMinutes);
            var charge = TariffBilling.ComputeForMinutes(
                requestedMinutes,
                new TariffPricing(
                    row.Version.PricePerMinuteMinorUnits,
                    row.Version.MinimumBillableMinutes,
                    row.Version.RoundingIncrementMinutes,
                    row.Version.CurrencyCode));
            if (charge is null) return Results.BadRequest(new { error = "invalid_duration" });

            return Results.Ok(new ReservationQuoteDto(
                row.Version.TariffVersionId,
                row.Name,
                requestedMinutes,
                charge.BillableMinutes,
                // Цена за всю компанию: столько и заморозится.
                charge.AmountMinorUnits * request.SeatCount,
                charge.CurrencyCode,
                request.SeatCount));
        }).RequireRateLimiting("player-me");
    }

    private static Task<bool> BranchInOrgAsync(
        PlatformDbContext db, Guid branchId, Guid orgId, CancellationToken ct) =>
        db.Branches.AsNoTracking()
            .AnyAsync(b => b.BranchId == branchId && b.OrganizationId == orgId, ct);
}
