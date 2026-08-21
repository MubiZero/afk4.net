using System.Data;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Branches;
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
        var waitsForAnswer = source == ReservationSourceNames.Online;
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
            State = waitsForAnswer
                ? ReservationStateNames.Pending
                : ReservationStateNames.Confirmed,
            RespondByUtc = waitsForAnswer
                ? await ResolveRespondByAsync(
                    request.OrganizationId, branchId, request.StartsAtUtc, now, cancellationToken)
                : null,
            ConfirmedAtUtc = waitsForAnswer ? null : now,
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
        var waitsForAnswer = source == ReservationSourceNames.Online;
        var state = waitsForAnswer
            ? ReservationStateNames.Pending
            : ReservationStateNames.Confirmed;
        // Обещание одно на всю компанию: срок считается один раз и ставится каждому месту группы.
        DateTimeOffset? respondByUtc = waitsForAnswer
            ? await ResolveRespondByAsync(
                request.OrganizationId, branchId, request.StartsAtUtc, now, cancellationToken)
            : null;
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
            RespondByUtc = respondByUtc,
            ConfirmedAtUtc = waitsForAnswer ? null : now,
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

        var versionConflict = GuardVersion(reservation, request.ExpectedVersion);
        if (versionConflict is not null)
        {
            return versionConflict;
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
        var nextPlayerAccountId = request.PlayerAccountId ?? reservation.PlayerAccountId;
        var nextSource = string.IsNullOrWhiteSpace(request.Source)
            ? reservation.Source
            : NormalizeSource(request.Source);
        var nextPhoneNumber = request.PhoneNumber is null
            ? reservation.PhoneNumber
            : NormalizeNullable(request.PhoneNumber);
        var nextNote = request.Note is null
            ? reservation.Note
            : NormalizeText(request.Note);
        var nextEndsAtUtc = nextStartsAtUtc.AddMinutes(nextDurationMinutes);

        if (nextPlayerAccountId == reservation.PlayerAccountId &&
            nextSeatId == reservation.SeatId &&
            string.Equals(nextCustomerName, reservation.CustomerName, StringComparison.Ordinal) &&
            string.Equals(nextPhoneNumber, reservation.PhoneNumber, StringComparison.Ordinal) &&
            nextStartsAtUtc == reservation.StartsAtUtc &&
            nextEndsAtUtc == reservation.EndsAtUtc &&
            string.Equals(nextSource, reservation.Source, StringComparison.Ordinal) &&
            string.Equals(nextNote, reservation.Note, StringComparison.Ordinal))
        {
            return ReservationServiceResult<ReservationDto>.Ok(
                (await ProjectAsync([reservation], cancellationToken))[0]);
        }

        var validation = await ValidateReservationShapeAsync(
            request.OrganizationId,
            reservation.BranchId,
            nextPlayerAccountId,
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

        reservation.PlayerAccountId = nextPlayerAccountId;
        reservation.SeatId = nextSeatId;
        reservation.CustomerName = nextCustomerName;
        reservation.PhoneNumber = nextPhoneNumber;
        reservation.StartsAtUtc = nextStartsAtUtc;
        reservation.EndsAtUtc = nextEndsAtUtc;
        // Бронь передвинули на более раннее время — обещание ответа едет вместе с ней: отвечать
        // после начала уже некому.
        if (reservation.RespondByUtc is { } respondByUtc && respondByUtc > nextStartsAtUtc)
        {
            reservation.RespondByUtc = nextStartsAtUtc;
        }

        reservation.Source = nextSource;
        reservation.Note = nextNote;
        reservation.UpdatedByStaffUserId = actorStaffUserId;
        reservation.UpdatedAtUtc = timeProvider.GetUtcNow();
        reservation.Version++;

        var saveConflict = await SaveMutationAsync(reservation, cancellationToken);
        if (saveConflict is not null)
        {
            return saveConflict;
        }

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

        var versionConflict = GuardVersion(reservation, request.ExpectedVersion);
        if (versionConflict is not null)
        {
            return versionConflict;
        }

        if (reservation.State == ReservationStateNames.Confirmed)
        {
            return ReservationServiceResult<ReservationDto>.Ok(
                (await ProjectAsync([reservation], cancellationToken))[0]);
        }

        if (reservation.State != ReservationStateNames.Pending)
        {
            return ReservationServiceResult<ReservationDto>.Invalid("Only pending reservations can be confirmed.");
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

        var answeredAt = timeProvider.GetUtcNow();
        reservation.State = ReservationStateNames.Confirmed;
        // Когда клуб ответил. Срок в RespondByUtc остаётся как след обещания: он больше ничего
        // не решает, но по нему видно, уложилась стойка в своё слово или нет.
        reservation.ConfirmedAtUtc = answeredAt;
        reservation.UpdatedByStaffUserId = actorStaffUserId;
        reservation.UpdatedAtUtc = answeredAt;
        reservation.Version++;
        var saveConflict = await SaveMutationAsync(reservation, cancellationToken);
        if (saveConflict is not null)
        {
            return saveConflict;
        }

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

        var versionConflict = GuardVersion(reservation, request.ExpectedVersion);
        if (versionConflict is not null)
        {
            return versionConflict;
        }

        if (reservation.State == ReservationStateNames.Seated)
        {
            return ReservationServiceResult<ReservationDto>.Ok(
                (await ProjectAsync([reservation], cancellationToken))[0]);
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
        // Посадить — это и есть ответ клуба: заявку, которую никто не подтверждал, стойка приняла
        // тем, что усадила человека за машину. Без этой отметки посаженная заявка выглядела бы
        // «так и не подтверждённой».
        reservation.ConfirmedAtUtc ??= now;
        reservation.UpdatedByStaffUserId = actorStaffUserId;
        reservation.UpdatedAtUtc = now;
        reservation.Version++;
        // Игрок сел — холд снимается, дальше считает обычный биллинг сессии. Оставить холд
        // значит взять деньги дважды: сначала заморозкой, потом настоящим списанием.
        await ReservationHold.ReleaseAsync(
            dbContext, reservation.ReservationId, ReservationHoldCauses.Seated, now, cancellationToken);
        var saveConflict = await SaveMutationAsync(reservation, cancellationToken);
        if (saveConflict is not null)
        {
            return saveConflict;
        }

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

        var versionConflict = GuardVersion(reservation, request.ExpectedVersion);
        if (versionConflict is not null)
        {
            return versionConflict;
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
            reservation.Version++;
            // Отмена со стойки возвращает деньги так же, как отмена из приложения: игроку всё
            // равно, кто нажал кнопку, а замороженные после отмены деньги он считает списанием.
            await ReservationHold.ReleaseAsync(
                dbContext, reservation.ReservationId, ReservationHoldCauses.Cancelled, now, cancellationToken);
            var saveConflict = await SaveMutationAsync(reservation, cancellationToken);
            if (saveConflict is not null)
            {
                return saveConflict;
            }
        }

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    private static ReservationServiceResult<ReservationDto>? GuardVersion(
        ReservationEntity reservation,
        int expectedVersion) =>
        reservation.Version == expectedVersion
            ? null
            : VersionConflict(reservation.Version);

    private async Task<ReservationServiceResult<ReservationDto>?> SaveMutationAsync(
        ReservationEntity reservation,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            var currentVersion = await dbContext.Reservations
                .AsNoTracking()
                .Where(candidate => candidate.ReservationId == reservation.ReservationId)
                .Select(candidate => (int?)candidate.Version)
                .SingleOrDefaultAsync(cancellationToken);
            return currentVersion is int version
                ? VersionConflict(version)
                : ReservationServiceResult<ReservationDto>.Missing("Reservation was not found.");
        }
    }

    private static ReservationServiceResult<ReservationDto> VersionConflict(int currentVersion) =>
        ReservationServiceResult<ReservationDto>.RequestConflict(
            "Reservation changed since it was loaded.",
            "version_conflict",
            currentVersion);

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

    // Есть ли игроку чем платить: кошелёк положительный и долга нет. Само по себе это больше не
    // решает судьбу брони — решает режим приёма у филиала, — но в режиме «подтверждаем сами» это
    // и есть то условие, по которому клуб раньше подтверждал брони молча.
    private async Task<bool> HasSpendableFundsAsync(Guid playerAccountId, CancellationToken cancellationToken)
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

    private Task<IReadOnlyList<ReservationDto>> ProjectAsync(
        IReadOnlyList<ReservationEntity> reservations,
        CancellationToken cancellationToken) =>
        ReservationProjection.ProjectAsync(dbContext, reservations, cancellationToken);

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

    /// <summary>
    /// Докуда клуб обещал ответить на заявку. Обещать ответ позже начала брони нельзя: к этому
    /// времени отвечать уже не на что, и игрок ждал бы того, чего не будет.
    /// </summary>
    private static DateTimeOffset RespondBy(
        DateTimeOffset now,
        DateTimeOffset startsAtUtc,
        int respondWithinMinutes)
    {
        var promised = now.AddMinutes(respondWithinMinutes);
        return promised < startsAtUtc ? promised : startsAtUtc;
    }

    /// <summary>
    /// Срок для заявки, заведённой на стойке: настройки филиала здесь ещё не читались, в отличие
    /// от брони из приложения, где они уже посчитаны для этого игрока.
    /// </summary>
    private async Task<DateTimeOffset> ResolveRespondByAsync(
        Guid organizationId,
        Guid branchId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var settings = await BranchBookingSettingsDefaults.ResolveAsync(
            dbContext, organizationId, branchId, cancellationToken);
        return RespondBy(now, startsAtUtc, settings.RespondWithinMinutes);
    }

    private static int DurationMinutes(ReservationEntity reservation)
    {
        return Math.Max(1, (int)Math.Round((reservation.EndsAtUtc - reservation.StartsAtUtc).TotalMinutes));
    }

    public Task<ReservationServiceResult<ReservationDto>> CreateOnlineAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        CreatePlayerReservationRequest request,
        CancellationToken cancellationToken) =>
        ExecuteBookingAsync(
            () => CreateOnlineCoreAsync(playerAccountId, organizationId, branchId, request, cancellationToken),
            () => ReservationServiceResult<ReservationDto>.RequestConflict(BranchCapacity.NoSeatsAvailableCode),
            cancellationToken);

    private async Task<ReservationServiceResult<ReservationDto>> CreateOnlineCoreAsync(
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

        // Первым делом — решение клуба о приёме гостей: филиал, закрывший брони из приложения,
        // не должен объяснять игроку ни цену, ни занятость зала.
        var rules = await PlayerBookingRules.EvaluateAsync(
            dbContext, organizationId, branchId, playerAccountId, timeProvider.GetUtcNow(), cancellationToken);
        if (!rules.AcceptsBookings)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(PlayerBookingRules.BookingDisabledCode);
        }

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

        // Свободные машины считаются раньше денег: сказать «не хватает средств» про вечер, на
        // который в зале всё равно нет мест, — это отправить игрока пополнять кошелёк впустую.
        if (!await BranchCapacity.HasRoomForAsync(
            dbContext, organizationId, branchId, request.StartsAtUtc, endsAtUtc, 1,
            timeProvider.GetUtcNow(), cancellationToken))
        {
            return ReservationServiceResult<ReservationDto>.RequestConflict(
                BranchCapacity.NoSeatsAvailableCode);
        }

        // Потолок броней у новичка считается после зала: сказать «броней хватит» про вечер, на
        // который всё равно нет мест, — это отказ не по той причине.
        if (!rules.HasRoomForOneMoreBooking)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(
                PlayerBookingRules.ActiveReservationLimitCode);
        }

        var now = timeProvider.GetUtcNow();

        var pricing = await PriceOnlineBookingAsync(
            organizationId, branchId, request.TariffVersionId, durationMinutes, request.StartsAtUtc, now,
            cancellationToken);
        if (pricing.Error is not null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(pricing.Error);
        }

        var tariffVersion = pricing.TariffVersion;
        var estimate = pricing.Estimate;

        // Клуб просит незнакомого гостя заплатить вперёд. Без выбранного тарифа замораживать
        // нечего, поэтому такая бронь отклоняется, а не висит «в ожидании».
        if (rules.PrepaymentRequired && estimate is null)
        {
            return ReservationServiceResult<ReservationDto>.Invalid(
                PlayerBookingRules.PrepaymentRequiredCode);
        }

        // Бронь с выбранным тарифом сразу замораживает деньги: слот занят, значит и сумма должна
        // быть занята — иначе к моменту прихода игрока её потратят в баре или на другой брони.
        // Не хватает денег — бронь отклоняется, а не висит «в ожидании»: «бронь только с деньгами»
        // (решение владельца 2026-06-18). Бронь без тарифа считают на стойке, там всё как раньше.
        LedgerEntryEntity? hold = null;
        if (estimate is not null)
        {
            var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
                dbContext, playerAccountId, cancellationToken);
            var available = wallet?.WalletBalance.MinorUnits ?? 0;
            if (available < estimate.AmountMinorUnits)
            {
                return ReservationServiceResult<ReservationDto>.Invalid("insufficient_funds");
            }
        }

        // Подтверждает бронь клуб, а не код. В режиме «смотрит администратор» заявка ждёт стойку
        // даже с замороженными деньгами: клуб так решил, и деньги этого решения не отменяют.
        // В режиме «подтверждаем сами» действует прежнее правило — «свободный слот и есть чем
        // платить», где замороженные деньги ответ более сильный, чем положительный баланс.
        var autoConfirm = rules.ConfirmsWithoutOperator
            && (estimate is not null || await HasSpendableFundsAsync(playerAccountId, cancellationToken));
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
            // Заявка, которую смотрит стойка, несёт срок ответа: по нему идёт обратный отсчёт у
            // игрока и таймер у администратора, и по нему же заявка снимается, если клуб промолчал.
            RespondByUtc = autoConfirm
                ? null
                : RespondBy(now, request.StartsAtUtc, rules.Settings.RespondWithinMinutes),
            ConfirmedAtUtc = autoConfirm ? now : null,
            Source = ReservationSourceNames.Online,
            Note = NormalizeText(request.Note),
            // Guid.Empty = self-service sentinel; no staff actor for online bookings.
            CreatedByStaffUserId = Guid.Empty,
            UpdatedByStaffUserId = Guid.Empty,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CancelReason = string.Empty,
            TariffVersionId = tariffVersion?.TariffVersionId,
            EstimatedCostMinorUnits = estimate?.AmountMinorUnits,
            CurrencyCode = estimate?.CurrencyCode
        };

        dbContext.Reservations.Add(reservation);
        if (estimate is not null)
        {
            hold = ReservationHold.Create(reservation, estimate.AmountMinorUnits, estimate.CurrencyCode, now);
            dbContext.LedgerEntries.Add(hold);
        }

        // Бронь и холд сохраняются одним разом: бронь без замороженных денег — это занятый слот
        // без обеспечения, а холд без брони — деньги, которые некому вернуть.
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
            // Отменил — деньги свободны в тот же момент. Держать их «до выяснения» не за чем:
            // слот освободился, а замороженный без причины баланс игрок считает списанием.
            await ReservationHold.ReleaseAsync(
                dbContext, reservation.ReservationId, ReservationHoldCauses.Cancelled, now, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ReservationServiceResult<ReservationDto>.Ok(
            (await ProjectAsync([reservation], cancellationToken))[0]);
    }

    private const int MaxBookingAttempts = 3;

    /// <summary>
    /// Проверка вместимости и вставка брони — одно неделимое действие.
    ///
    /// Порознь они образуют классическую гонку: две брони одновременно читают «места есть», обе
    /// проходят проверку и обе вставляются, и зал на одну машину раздаёт два обещания и замораживает
    /// деньги дважды. Читающая проверка на READ COMMITTED от этого не спасает: соперник вставляет
    /// строку уже после нашего чтения.
    ///
    /// Тот же приём, что у старта сессии (EfSessionCommandService) и у старта по брони
    /// (EfReservationSessionCoordinator): Serializable плюс повтор при конфликте сериализации,
    /// который транзиентен. Исчерпали попытки — отвечаем «мест нет»: раз соперник продолжает
    /// выигрывать гонку, места достались ему.
    /// </summary>
    private async Task<T> ExecuteBookingAsync<T>(
        Func<Task<T>> action,
        Func<T> whenSeatsRanOut,
        CancellationToken cancellationToken)
    {
        // In-memory провайдер транзакций не знает; тесты на нём проверяют логику, а не изоляцию.
        if (!dbContext.Database.IsRelational())
        {
            return await action();
        }

        for (var attempt = 1; attempt <= MaxBookingAttempts; attempt++)
        {
            try
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable, cancellationToken);
                var result = await action();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (RelationalFailureClassifier.IsSerializationFailure(exception))
            {
                dbContext.ChangeTracker.Clear();
                if (attempt == MaxBookingAttempts)
                {
                    return whenSeatsRanOut();
                }
            }
        }

        return whenSeatsRanOut();
    }

    private sealed record OnlineBookingPricing(
        string? Error,
        TariffVersionEntity? TariffVersion,
        TariffComputation? Estimate);

    /// <summary>
    /// Цена выбора игрока — за одно место. Считается на сервере, а не в приложении: минимальная
    /// оплачиваемая длительность и шаг округления живут в биллинге, и вторая их реализация в
    /// клиенте разошлась бы с настоящим списанием при первой же правке любой из сторон.
    ///
    /// Общая и для одиночной брони, и для брони на компанию: две копии этого расчёта — верный
    /// способ однажды показать компании одну цену, а заморозить другую.
    /// </summary>
    private async Task<OnlineBookingPricing> PriceOnlineBookingAsync(
        Guid organizationId,
        Guid branchId,
        Guid? tariffVersionId,
        int durationMinutes,
        DateTimeOffset startsAtUtc,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Тарифа нет — бронь считают на стойке, как было до самообслуживания.
        if (tariffVersionId is not { } versionId)
        {
            return new OnlineBookingPricing(null, null, null);
        }

        var tariffVersion = await dbContext.TariffVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                version =>
                    version.TariffVersionId == versionId &&
                    version.OrganizationId == organizationId &&
                    version.BranchId == branchId &&
                    version.EffectiveFromUtc <= now &&
                    (version.RetiredAtUtc == null || version.RetiredAtUtc > now),
                cancellationToken);

        // Снятый с публикации или чужой тариф отклоняется, а не подменяется тихо тарифом по
        // умолчанию: игрок согласился на конкретную цену, и забронировать по другой — это ложь
        // о деньгах.
        if (tariffVersion is null)
        {
            return new OnlineBookingPricing(
                "Selected tariff is not available in this branch.", null, null);
        }

        // Расписание проверяется по времени брони, а не по «сейчас»: игрок в восемь вечера
        // бронирует завтрашнее утро, и утренний тариф для этой брони действует, хотя в момент
        // нажатия кнопки — нет.
        //
        // Проверяется весь промежуток, а не одно его начало. Бронь считается одной ставкой на всю
        // длительность, поэтому «тариф действовал на старте» пропустило бы бронь с 08:00 до 23:00
        // целиком по утренней цене.
        if (!await TariffAvailability.AppliesThroughoutAsync(
            dbContext, organizationId, branchId, versionId, startsAtUtc,
            startsAtUtc.AddMinutes(durationMinutes), cancellationToken))
        {
            return new OnlineBookingPricing(TariffSchedule.OutsideHoursCode, null, null);
        }

        var estimate = TariffBilling.ComputeForMinutes(
            durationMinutes,
            new TariffPricing(
                tariffVersion.PricePerMinuteMinorUnits,
                tariffVersion.MinimumBillableMinutes,
                tariffVersion.RoundingIncrementMinutes,
                tariffVersion.CurrencyCode));

        return estimate is null
            ? new OnlineBookingPricing("Selected tariff cannot price this booking.", null, null)
            : new OnlineBookingPricing(null, tariffVersion, estimate);
    }

    public Task<ReservationServiceResult<IReadOnlyList<ReservationDto>>> CreateOnlineGroupAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        CreatePlayerReservationGroupRequest request,
        CancellationToken cancellationToken) =>
        ExecuteBookingAsync(
            () => CreateOnlineGroupCoreAsync(playerAccountId, organizationId, branchId, request, cancellationToken),
            () => ReservationServiceResult<IReadOnlyList<ReservationDto>>.RequestConflict(
                BranchCapacity.NoSeatsAvailableCode),
            cancellationToken);

    private async Task<ReservationServiceResult<IReadOnlyList<ReservationDto>>> CreateOnlineGroupCoreAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        CreatePlayerReservationGroupRequest request,
        CancellationToken cancellationToken)
    {
        if (!PlayerReservationGroupLimits.IsAllowedSeatCount(request.SeatCount))
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid(
                PlayerReservationGroupLimits.InvalidSeatCountCode);
        }

        if (request.StartsAtUtc >= request.EndsAtUtc)
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid(
                "Reservation end time must be after start time.");
        }

        var durationMinutes = (int)Math.Round((request.EndsAtUtc - request.StartsAtUtc).TotalMinutes);

        var rules = await PlayerBookingRules.EvaluateAsync(
            dbContext, organizationId, branchId, playerAccountId, timeProvider.GetUtcNow(), cancellationToken);
        if (!rules.AcceptsBookings)
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid(
                PlayerBookingRules.BookingDisabledCode);
        }

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
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid(
                "Player account was not found in this branch.");
        }

        // Форма проверяется один раз: у всех мест группы она общая, а конкретного места у брони из
        // приложения нет — его назначает клуб.
        var validation = await ValidateReservationShapeAsync(
            organizationId,
            branchId,
            playerAccountId,
            seatId: null,
            account.DisplayName,
            request.StartsAtUtc,
            durationMinutes,
            ReservationSourceNames.Online,
            cancellationToken);

        if (validation is not null)
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid(validation);
        }

        var now = timeProvider.GetUtcNow();
        var endsAtUtc = request.StartsAtUtc.AddMinutes(durationMinutes);

        // Компания влезает целиком или не влезает вовсе. Посадить четверых из пятерых и промолчать
        // про пятого — это тот же подвод компании в клубе, что и «денег хватило на троих».
        if (!await BranchCapacity.HasRoomForAsync(
            dbContext, organizationId, branchId, request.StartsAtUtc, endsAtUtc, request.SeatCount,
            now, cancellationToken))
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.RequestConflict(
                BranchCapacity.NoSeatsAvailableCode);
        }

        // Компания — это одна бронь, а не четыре: потолок новичка она занимает целиком один раз.
        if (!rules.HasRoomForOneMoreBooking)
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid(
                PlayerBookingRules.ActiveReservationLimitCode);
        }

        var pricing = await PriceOnlineBookingAsync(
            organizationId, branchId, request.TariffVersionId, durationMinutes, request.StartsAtUtc, now,
            cancellationToken);
        if (pricing.Error is not null)
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid(pricing.Error);
        }

        if (rules.PrepaymentRequired && pricing.Estimate is null)
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid(
                PlayerBookingRules.PrepaymentRequiredCode);
        }

        // Денег должно хватить на ВСЮ компанию сразу. Забронировать три места из четырёх и
        // промолчать про четвёртое — это подвести компанию в клубе, а не «частичный успех».
        if (pricing.Estimate is { } perSeat)
        {
            var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(
                dbContext, playerAccountId, cancellationToken);
            var available = wallet?.WalletBalance.MinorUnits ?? 0;
            if (available < perSeat.AmountMinorUnits * request.SeatCount)
            {
                return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Invalid("insufficient_funds");
            }
        }

        var autoConfirm = rules.ConfirmsWithoutOperator
            && (pricing.Estimate is not null || await HasSpendableFundsAsync(playerAccountId, cancellationToken));
        var groupId = Guid.NewGuid();
        var note = NormalizeText(request.Note);
        var reservations = new List<ReservationEntity>(request.SeatCount);

        for (var index = 0; index < request.SeatCount; index++)
        {
            var reservation = new ReservationEntity
            {
                ReservationId = Guid.NewGuid(),
                OrganizationId = organizationId,
                BranchId = branchId,
                ReservationGroupId = groupId,
                PlayerAccountId = playerAccountId,
                SeatId = null,
                CustomerName = account.DisplayName,
                PhoneNumber = account.PhoneNumber,
                StartsAtUtc = request.StartsAtUtc,
                EndsAtUtc = endsAtUtc,
                State = autoConfirm
                    ? ReservationStateNames.Confirmed
                    : ReservationStateNames.Pending,
                RespondByUtc = autoConfirm
                    ? null
                    : RespondBy(now, request.StartsAtUtc, rules.Settings.RespondWithinMinutes),
                ConfirmedAtUtc = autoConfirm ? now : null,
                Source = ReservationSourceNames.Online,
                Note = note,
                CreatedByStaffUserId = Guid.Empty,
                UpdatedByStaffUserId = Guid.Empty,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CancelReason = string.Empty,
                TariffVersionId = pricing.TariffVersion?.TariffVersionId,
                EstimatedCostMinorUnits = pricing.Estimate?.AmountMinorUnits,
                CurrencyCode = pricing.Estimate?.CurrencyCode
            };

            reservations.Add(reservation);
            dbContext.Reservations.Add(reservation);

            // Холд на КАЖДОЕ место отдельной записью, а не один общий на группу. Так отмена
            // одного места, посадка одного человека и неявка одного из компании работают ровно
            // тем же кодом, что и у одиночной брони, — ничего не пришлось учить про группы.
            if (pricing.Estimate is { } estimate)
            {
                dbContext.LedgerEntries.Add(
                    ReservationHold.Create(reservation, estimate.AmountMinorUnits, estimate.CurrencyCode, now));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Ok(
            await ProjectAsync(reservations, cancellationToken));
    }

    public async Task<ReservationServiceResult<IReadOnlyList<ReservationDto>>> CancelOnlineGroupAsync(
        Guid reservationGroupId,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var reservations = await dbContext.Reservations
            .Where(candidate =>
                candidate.ReservationGroupId == reservationGroupId &&
                candidate.PlayerAccountId == playerAccountId)
            .ToListAsync(cancellationToken);

        // Чужая группа неотличима от несуществующей — иначе перебором идентификаторов можно
        // узнать, что она есть.
        if (reservations.Count == 0)
        {
            return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Missing(
                "Reservation group was not found.");
        }

        var now = timeProvider.GetUtcNow();
        foreach (var reservation in reservations)
        {
            // Уже отменённые и отыгранные пропускаются молча: отмена компании — это одно действие
            // с одним исходом, и падать на месте, которое уже отменили, значит запретить отменить
            // остальные.
            if (reservation.State is not ReservationStateNames.Pending and not ReservationStateNames.Confirmed)
            {
                continue;
            }

            reservation.State = ReservationStateNames.Cancelled;
            reservation.CancelReason = "player-initiated";
            reservation.CancelledAtUtc = now;
            reservation.UpdatedByStaffUserId = Guid.Empty;
            reservation.UpdatedAtUtc = now;
            await ReservationHold.ReleaseAsync(
                dbContext, reservation.ReservationId, ReservationHoldCauses.Cancelled, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return ReservationServiceResult<IReadOnlyList<ReservationDto>>.Ok(
            await ProjectAsync(reservations, cancellationToken));
    }
}
