using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reservations;

public sealed class EfReservationService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IReservationService
{
    private const int DefaultLimit = 40;
    private const int MaxLimit = 100;

    private static readonly string[] ActiveReservationStates =
    [
        ReservationStateNames.Pending,
        ReservationStateNames.Confirmed
    ];

    private static readonly string[] BlockingSessionStates =
    [
        SessionStateNames.Active,
        SessionStateNames.Paused,
        SessionStateNames.Ending
    ];

    public async Task<ReservationSearchResultDto> SearchAsync(
        Guid organizationId,
        Guid branchId,
        ReservationSearchQuery query,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var (fromUtc, toUtc) = NormalizeRange(query.FromUtc, query.ToUtc, now);
        var limit = NormalizeLimit(query.Limit);

        var reservationsQuery = dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.OrganizationId == organizationId &&
                reservation.BranchId == branchId &&
                reservation.StartsAtUtc <= toUtc &&
                reservation.EndsAtUtc >= fromUtc);

        if (!string.IsNullOrWhiteSpace(query.State))
        {
            reservationsQuery = reservationsQuery.Where(reservation => reservation.State == query.State);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            reservationsQuery = reservationsQuery.Where(reservation => reservation.Source == query.Source);
        }

        if (query.PlayerAccountId is Guid playerAccountId)
        {
            reservationsQuery = reservationsQuery.Where(reservation => reservation.PlayerAccountId == playerAccountId);
        }

        var reservations = await reservationsQuery
            .OrderBy(reservation => reservation.StartsAtUtc)
            .ThenBy(reservation => reservation.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return new ReservationSearchResultDto(
            await ProjectAsync(reservations, cancellationToken),
            limit);
    }

    public async Task<ReservationServiceResult<ReservationDto>> CreateAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateReservationShapeAsync(
            request.OrganizationId,
            branchId,
            request.PlayerAccountId,
            request.SeatId,
            request.CustomerName,
            request.StartsAtUtc,
            request.DurationMinutes,
            request.Source,
            cancellationToken);
        if (validation is not null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(validation);
        }

        var now = timeProvider.GetUtcNow();
        var endsAtUtc = request.StartsAtUtc.AddMinutes(request.DurationMinutes);
        var conflict = await FindConflictAsync(
            request.OrganizationId,
            branchId,
            request.SeatId,
            request.StartsAtUtc,
            endsAtUtc,
            excludedReservationId: null,
            cancellationToken);
        if (conflict is not null)
        {
            return ReservationServiceResult<ReservationDto>.RequestConflict(conflict);
        }

        var source = NormalizeSource(request.Source);
        var reservation = new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            PlayerAccountId = request.PlayerAccountId,
            SeatId = request.SeatId,
            CustomerName = request.CustomerName.Trim(),
            PhoneNumber = NormalizeNullable(request.PhoneNumber),
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = endsAtUtc,
            State = source == ReservationSourceNames.Online
                ? ReservationStateNames.Pending
                : ReservationStateNames.Confirmed,
            Source = source,
            Note = NormalizeText(request.Note),
            CreatedByStaffUserId = actorStaffUserId,
            UpdatedByStaffUserId = actorStaffUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CancelReason = string.Empty
        };

        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    public async Task<CreateReservationGroupResult> CreateGroupAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateReservationGroupRequest request,
        CancellationToken cancellationToken)
    {
        var seatIds = (request.SeatIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();
        if (seatIds.Count == 0)
        {
            return CreateReservationGroupResult.Invalid("At least one seat is required for a group reservation.");
        }

        var endsAtUtc = request.StartsAtUtc.AddMinutes(request.DurationMinutes);

        // Validate the shared shape against every seat (seat exists & belongs to the branch, time/source ok).
        foreach (var seatId in seatIds)
        {
            var validation = await ValidateReservationShapeAsync(
                request.OrganizationId,
                branchId,
                request.PlayerAccountId,
                seatId,
                request.CustomerName,
                request.StartsAtUtc,
                request.DurationMinutes,
                request.Source,
                cancellationToken);
            if (validation is not null)
            {
                return CreateReservationGroupResult.Invalid(validation);
            }
        }

        // All-or-nothing: a group is one booking, so any taken seat (active reservation OR blocking
        // session) rejects the whole group. Report every conflicting seat so the UI can adjust.
        var conflicts = new List<ReservationGroupConflictDto>();
        foreach (var seatId in seatIds)
        {
            var conflict = await FindConflictAsync(
                request.OrganizationId,
                branchId,
                seatId,
                request.StartsAtUtc,
                endsAtUtc,
                excludedReservationId: null,
                cancellationToken);
            if (conflict is not null)
            {
                conflicts.Add(new ReservationGroupConflictDto(seatId, conflict));
            }
        }
        if (conflicts.Count > 0)
        {
            return CreateReservationGroupResult.Conflict(new ReservationGroupResultDto(null, [], conflicts));
        }

        var now = timeProvider.GetUtcNow();
        var source = NormalizeSource(request.Source);
        var state = source == ReservationSourceNames.Online
            ? ReservationStateNames.Pending
            : ReservationStateNames.Confirmed;
        var groupId = Guid.NewGuid();
        var customerName = request.CustomerName.Trim();
        var phoneNumber = NormalizeNullable(request.PhoneNumber);
        var note = NormalizeText(request.Note);

        var reservations = seatIds.Select(seatId => new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            ReservationGroupId = groupId,
            PlayerAccountId = request.PlayerAccountId,
            SeatId = seatId,
            CustomerName = customerName,
            PhoneNumber = phoneNumber,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = endsAtUtc,
            State = state,
            Source = source,
            Note = note,
            CreatedByStaffUserId = actorStaffUserId,
            UpdatedByStaffUserId = actorStaffUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CancelReason = string.Empty
        }).ToList();

        dbContext.Reservations.AddRange(reservations);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await ProjectAsync(reservations, cancellationToken);
        return CreateReservationGroupResult.Created(new ReservationGroupResultDto(groupId, dtos, []));
    }

    public async Task<ReservationServiceResult<ReservationDto>> UpdateAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        UpdateReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await LoadForWriteAsync(request.OrganizationId, reservationId, cancellationToken);
        if (reservation is null)
        {
            return ReservationServiceResult<ReservationDto>.Missing("Reservation was not found.");
        }

        if (!CanChange(reservation))
        {
            return ReservationServiceResult<ReservationDto>.Invalid("Only pending or confirmed reservations can be changed.");
        }

        var nextCustomerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? reservation.CustomerName
            : request.CustomerName.Trim();
        var nextStartsAtUtc = request.StartsAtUtc ?? reservation.StartsAtUtc;
        var nextDurationMinutes = request.DurationMinutes ?? DurationMinutes(reservation);
        var nextSeatId = request.SeatId ?? reservation.SeatId;
        var nextSource = string.IsNullOrWhiteSpace(request.Source)
            ? reservation.Source
            : NormalizeSource(request.Source);

        var validation = await ValidateReservationShapeAsync(
            request.OrganizationId,
            reservation.BranchId,
            request.PlayerAccountId ?? reservation.PlayerAccountId,
            nextSeatId,
            nextCustomerName,
            nextStartsAtUtc,
            nextDurationMinutes,
            nextSource,
            cancellationToken);
        if (validation is not null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(validation);
        }

        var nextEndsAtUtc = nextStartsAtUtc.AddMinutes(nextDurationMinutes);
        var conflict = await FindConflictAsync(
            request.OrganizationId,
            reservation.BranchId,
            nextSeatId,
            nextStartsAtUtc,
            nextEndsAtUtc,
            reservation.ReservationId,
            cancellationToken);
        if (conflict is not null)
        {
            return ReservationServiceResult<ReservationDto>.RequestConflict(conflict);
        }

        reservation.PlayerAccountId = request.PlayerAccountId ?? reservation.PlayerAccountId;
        reservation.SeatId = nextSeatId;
        reservation.CustomerName = nextCustomerName;
        reservation.PhoneNumber = request.PhoneNumber is null
            ? reservation.PhoneNumber
            : NormalizeNullable(request.PhoneNumber);
        reservation.StartsAtUtc = nextStartsAtUtc;
        reservation.EndsAtUtc = nextEndsAtUtc;
        reservation.Source = nextSource;
        reservation.Note = request.Note is null
            ? reservation.Note
            : NormalizeText(request.Note);
        reservation.UpdatedByStaffUserId = actorStaffUserId;
        reservation.UpdatedAtUtc = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(cancellationToken);

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    public async Task<ReservationServiceResult<ReservationDto>> ConfirmAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        ConfirmReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await LoadForWriteAsync(request.OrganizationId, reservationId, cancellationToken);
        if (reservation is null)
        {
            return ReservationServiceResult<ReservationDto>.Missing("Reservation was not found.");
        }

        if (!CanChange(reservation))
        {
            return ReservationServiceResult<ReservationDto>.Invalid("Only pending or confirmed reservations can be confirmed.");
        }

        var conflict = await FindConflictAsync(
            request.OrganizationId,
            reservation.BranchId,
            reservation.SeatId,
            reservation.StartsAtUtc,
            reservation.EndsAtUtc,
            reservation.ReservationId,
            cancellationToken);
        if (conflict is not null)
        {
            return ReservationServiceResult<ReservationDto>.RequestConflict(conflict);
        }

        reservation.State = ReservationStateNames.Confirmed;
        reservation.UpdatedByStaffUserId = actorStaffUserId;
        reservation.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    public async Task<ReservationServiceResult<ReservationDto>> SeatAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        SeatReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await LoadForWriteAsync(request.OrganizationId, reservationId, cancellationToken);
        if (reservation is null)
        {
            return ReservationServiceResult<ReservationDto>.Missing("Reservation was not found.");
        }

        if (!CanChange(reservation))
        {
            return ReservationServiceResult<ReservationDto>.Invalid("Only pending or confirmed reservations can be seated.");
        }

        if (reservation.SeatId is null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid("Reservation must have a seat before seating.");
        }

        var now = timeProvider.GetUtcNow();
        if (await HasBlockingSessionAsync(
            request.OrganizationId,
            reservation.BranchId,
            reservation.SeatId.Value,
            now,
            now.AddMinutes(Math.Max(1, DurationMinutes(reservation))),
            cancellationToken))
        {
            return ReservationServiceResult<ReservationDto>.RequestConflict("Seat has an active, paused, or ending session.");
        }

        reservation.State = ReservationStateNames.Seated;
        reservation.SeatedAtUtc = now;
        reservation.UpdatedByStaffUserId = actorStaffUserId;
        reservation.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    public async Task<ReservationServiceResult<ReservationDto>> CancelAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        CancelReservationRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await LoadForWriteAsync(request.OrganizationId, reservationId, cancellationToken);
        if (reservation is null)
        {
            return ReservationServiceResult<ReservationDto>.Missing("Reservation was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ReservationServiceResult<ReservationDto>.Invalid("Cancel reason is required.");
        }

        if (reservation.State is not ReservationStateNames.Pending and not ReservationStateNames.Confirmed and not ReservationStateNames.Cancelled)
        {
            return ReservationServiceResult<ReservationDto>.Invalid("Only pending or confirmed reservations can be cancelled.");
        }

        if (reservation.State != ReservationStateNames.Cancelled)
        {
            var now = timeProvider.GetUtcNow();
            reservation.State = ReservationStateNames.Cancelled;
            reservation.CancelReason = request.Reason.Trim();
            reservation.CancelledAtUtc = now;
            reservation.UpdatedByStaffUserId = actorStaffUserId;
            reservation.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    private async Task<string?> ValidateReservationShapeAsync(
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        Guid? seatId,
        string customerName,
        DateTimeOffset startsAtUtc,
        int durationMinutes,
        string source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return "Customer name is required.";
        }

        if (customerName.Trim().Length > 160)
        {
            return "Customer name is too long.";
        }

        if (durationMinutes <= 0)
        {
            return "Reservation duration must be positive.";
        }

        if (durationMinutes > 24 * 60)
        {
            return "Reservation duration must be 24 hours or less.";
        }

        if (startsAtUtc == default)
        {
            return "Reservation start time is required.";
        }

        if (!IsKnownSource(source))
        {
            return "Reservation source is not supported.";
        }

        if (seatId is not null && !await dbContext.Seats.AnyAsync(
            seat =>
                seat.OrganizationId == organizationId &&
                seat.BranchId == branchId &&
                seat.SeatId == seatId,
            cancellationToken))
        {
            return "Seat was not found in this branch.";
        }

        if (playerAccountId is not null && !await dbContext.PlayerAccounts.AnyAsync(
            player =>
                player.OrganizationId == organizationId &&
                player.HomeBranchId == branchId &&
                player.PlayerAccountId == playerAccountId,
            cancellationToken))
        {
            return "Player account was not found in this branch.";
        }

        return null;
    }

    private async Task<string?> FindConflictAsync(
        Guid organizationId,
        Guid branchId,
        Guid? seatId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        Guid? excludedReservationId,
        CancellationToken cancellationToken)
    {
        if (seatId is null)
        {
            return null;
        }

        var hasReservationConflict = await dbContext.Reservations.AnyAsync(
            reservation =>
                reservation.OrganizationId == organizationId &&
                reservation.BranchId == branchId &&
                reservation.SeatId == seatId &&
                reservation.ReservationId != excludedReservationId &&
                ActiveReservationStates.Contains(reservation.State) &&
                reservation.StartsAtUtc < endsAtUtc &&
                reservation.EndsAtUtc > startsAtUtc,
            cancellationToken);
        if (hasReservationConflict)
        {
            return "Seat already has an overlapping active reservation.";
        }

        return await HasBlockingSessionAsync(
            organizationId,
            branchId,
            seatId.Value,
            startsAtUtc,
            endsAtUtc,
            cancellationToken)
            ? "Seat has an active, paused, or ending session."
            : null;
    }

    private async Task<bool> HasBlockingSessionAsync(
        Guid organizationId,
        Guid branchId,
        Guid seatId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        CancellationToken cancellationToken)
    {
        return await dbContext.Sessions.AnyAsync(
            session =>
                session.OrganizationId == organizationId &&
                session.BranchId == branchId &&
                session.SeatId == seatId &&
                BlockingSessionStates.Contains(session.State) &&
                (session.EndsAtUtc == null || session.EndsAtUtc > startsAtUtc) &&
                session.RequestedAtUtc < endsAtUtc,
            cancellationToken);
    }

    // Auto-confirm gate for self-service bookings: the player has spendable funds (positive wallet,
    // not in debt). Read-only on money — the hold/charge lifecycle is a separate deferred feature.
    private async Task<bool> ShouldAutoConfirmOnlineAsync(Guid playerAccountId, CancellationToken cancellationToken)
    {
        var summary = await LedgerBalanceProjector.GetWalletSummaryAsync(dbContext, playerAccountId, cancellationToken);
        if (summary is null)
        {
            return false;
        }

        return summary.WalletBalance.MinorUnits > 0 && summary.DebtBalance.MinorUnits <= 0;
    }

    private async Task<ReservationEntity?> LoadForWriteAsync(
        Guid organizationId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Reservations.SingleOrDefaultAsync(
            reservation =>
                reservation.OrganizationId == organizationId &&
                reservation.ReservationId == reservationId,
            cancellationToken);
    }

    private async Task<IReadOnlyList<ReservationDto>> ProjectAsync(
        IReadOnlyList<ReservationEntity> reservations,
        CancellationToken cancellationToken)
    {
        var seatIds = reservations
            .Where(reservation => reservation.SeatId is not null)
            .Select(reservation => reservation.SeatId!.Value)
            .Distinct()
            .ToList();
        var seats = seatIds.Count == 0
            ? new List<SeatEntity>()
            : await dbContext.Seats
                .AsNoTracking()
                .Where(seat => seatIds.Contains(seat.SeatId))
                .ToListAsync(cancellationToken);
        var zoneIds = seats.Select(seat => seat.ZoneId).Distinct().ToList();
        var zones = zoneIds.Count == 0
            ? new List<ZoneEntity>()
            : await dbContext.Zones
                .AsNoTracking()
                .Where(zone => zoneIds.Contains(zone.ZoneId))
                .ToListAsync(cancellationToken);
        var seatById = seats.ToDictionary(seat => seat.SeatId);
        var zoneById = zones.ToDictionary(zone => zone.ZoneId);

        return reservations.Select(reservation =>
        {
            SeatEntity? seat = null;
            if (reservation.SeatId is not null)
            {
                seatById.TryGetValue(reservation.SeatId.Value, out seat);
            }

            var zoneName = seat is not null && zoneById.TryGetValue(seat.ZoneId, out var zone)
                ? zone.Name
                : null;

            return new ReservationDto(
                reservation.ReservationId,
                reservation.OrganizationId,
                reservation.BranchId,
                reservation.PlayerAccountId,
                reservation.SeatId,
                seat?.Name,
                zoneName,
                reservation.CustomerName,
                reservation.PhoneNumber,
                reservation.StartsAtUtc,
                reservation.EndsAtUtc,
                DurationMinutes(reservation),
                reservation.State,
                reservation.Source,
                reservation.Note,
                reservation.CreatedAtUtc,
                reservation.UpdatedAtUtc,
                reservation.CancelledAtUtc,
                reservation.CancelReason,
                reservation.ReservationGroupId,
                reservation.Version,
                reservation.StartedSessionId);
        }).ToList();
    }

    private static (DateTimeOffset FromUtc, DateTimeOffset ToUtc) NormalizeRange(
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        DateTimeOffset nowUtc)
    {
        var to = toUtc ?? nowUtc.AddDays(1);
        var from = fromUtc ?? new DateTimeOffset(to.UtcDateTime.Date, TimeSpan.Zero);

        return from <= to
            ? (from, to)
            : (to, from);
    }

    private static int NormalizeLimit(int? limit)
    {
        return limit is null or <= 0 ? DefaultLimit : Math.Min(limit.Value, MaxLimit);
    }

    private static string NormalizeSource(string value)
    {
        return string.Equals(value, ReservationSourceNames.Online, StringComparison.OrdinalIgnoreCase)
            ? ReservationSourceNames.Online
            : ReservationSourceNames.Operator;
    }

    private static bool IsKnownSource(string value)
    {
        return string.Equals(value, ReservationSourceNames.Operator, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, ReservationSourceNames.Online, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanChange(ReservationEntity reservation)
    {
        return reservation.State is ReservationStateNames.Pending or ReservationStateNames.Confirmed;
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int DurationMinutes(ReservationEntity reservation)
    {
        return Math.Max(1, (int)Math.Round((reservation.EndsAtUtc - reservation.StartsAtUtc).TotalMinutes));
    }

    public async Task<ReservationServiceResult<ReservationDto>> CreateOnlineAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        CreatePlayerReservationRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartsAtUtc >= request.EndsAtUtc)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(
                "Reservation end time must be after start time.");
        }

        // Compute duration in minutes from the absolute times.
        var durationMinutes = (int)Math.Round((request.EndsAtUtc - request.StartsAtUtc).TotalMinutes);

        // Load the player's display name and phone for the reservation record.
        var account = await dbContext.PlayerAccounts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                player =>
                    player.OrganizationId == organizationId &&
                    player.HomeBranchId == branchId &&
                    player.PlayerAccountId == playerAccountId,
                cancellationToken);

        if (account is null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(
                "Player account was not found in this branch.");
        }

        var validation = await ValidateReservationShapeAsync(
            organizationId,
            branchId,
            playerAccountId,
            request.SeatId,
            account.DisplayName,
            request.StartsAtUtc,
            durationMinutes,
            ReservationSourceNames.Online,
            cancellationToken);

        if (validation is not null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(validation);
        }

        var endsAtUtc = request.StartsAtUtc.AddMinutes(durationMinutes);
        var conflict = await FindConflictAsync(
            organizationId,
            branchId,
            request.SeatId,
            request.StartsAtUtc,
            endsAtUtc,
            excludedReservationId: null,
            cancellationToken);

        if (conflict is not null)
        {
            return ReservationServiceResult<ReservationDto>.RequestConflict(conflict);
        }

        var now = timeProvider.GetUtcNow();
        // Auto-confirm self-service bookings when the player has funds (positive wallet, no debt):
        // «free slot + has balance → book without operator». Otherwise stay Pending for operator
        // review (the requests lane stays a fallback for funded-later / disputed cases).
        var autoConfirm = await ShouldAutoConfirmOnlineAsync(playerAccountId, cancellationToken);
        var reservation = new ReservationEntity
        {
            ReservationId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            PlayerAccountId = playerAccountId,
            SeatId = request.SeatId,
            CustomerName = account.DisplayName,
            PhoneNumber = account.PhoneNumber,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = endsAtUtc,
            State = autoConfirm
                ? ReservationStateNames.Confirmed
                : ReservationStateNames.Pending,
            Source = ReservationSourceNames.Online,
            Note = NormalizeText(request.Note),
            // Guid.Empty = self-service sentinel; no staff actor for online bookings.
            CreatedByStaffUserId = Guid.Empty,
            UpdatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CancelReason = string.Empty
        };

        dbContext.Reservations.Add(reservation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    public async Task<ReservationServiceResult<ReservationDto>> CancelOnlineAsync(
        Guid reservationId,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.ReservationId == reservationId &&
                    candidate.PlayerAccountId == playerAccountId,
                cancellationToken);

        // Return NotFound (not Forbidden) to avoid existence disclosure.
        if (reservation is null)
        {
            return ReservationServiceResult<ReservationDto>.Missing(
                "Reservation was not found.");
        }

        if (reservation.State is not ReservationStateNames.Pending and not ReservationStateNames.Confirmed and not ReservationStateNames.Cancelled)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(
                "Only pending or confirmed reservations can be cancelled.");
        }

        if (reservation.State != ReservationStateNames.Cancelled)
        {
            var now = timeProvider.GetUtcNow();
            reservation.State = ReservationStateNames.Cancelled;
            reservation.CancelReason = "player-initiated";
            reservation.CancelledAtUtc = now;
            reservation.UpdatedByStaffUserId = Guid.Empty;
            reservation.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }
}
