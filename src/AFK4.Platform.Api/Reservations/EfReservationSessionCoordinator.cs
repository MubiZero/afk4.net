using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AFK4.Platform.Api.Reservations;

public sealed class EfReservationSessionCoordinator(
    PlatformDbContext dbContext,
    ISessionStartWorkflow sessionStartWorkflow,
    TimeProvider timeProvider) : IReservationSessionCoordinator
{
    private const int MaxAttempts = 3;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReservationSessionStartResult> StartAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        bool actorCanApproveComp,
        StartReservationSessionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return ReservationSessionStartResult.Invalid(
                "idempotency_key_required",
                "Idempotency key is required.");
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            SessionStartStage? committedStage = null;
            try
            {
                var result = await ExecuteAttemptAsync(
                    reservationId,
                    actorStaffUserId,
                    actorCanApproveComp,
                    request,
                    stage => committedStage = stage,
                    cancellationToken);

                if (result.Succeeded && committedStage is not null)
                {
                    await sessionStartWorkflow.NotifyCommittedAsync(committedStage, cancellationToken);
                }

                return result;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (Exception exception) when (IsRetryable(exception) && attempt < MaxAttempts)
            {
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                dbContext.ChangeTracker.Clear();
                return await VersionConflictAsync(reservationId, request.OrganizationId, cancellationToken);
            }
            catch (Exception exception) when (IsRetryable(exception))
            {
                dbContext.ChangeTracker.Clear();
                return await ReevaluateConflictAsync(reservationId, request, cancellationToken);
            }
        }

        throw new InvalidOperationException("Reservation session start retry loop terminated unexpectedly.");
    }

    private async Task<ReservationSessionStartResult> ExecuteAttemptAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        bool actorCanApproveComp,
        StartReservationSessionRequest request,
        Action<SessionStartStage> onCommittedStage,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await StageAndSaveAsync(
                reservationId,
                actorStaffUserId,
                actorCanApproveComp,
                request,
                onCommittedStage,
                cancellationToken);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var result = await StageAndSaveAsync(
            reservationId,
            actorStaffUserId,
            actorCanApproveComp,
            request,
            onCommittedStage,
            cancellationToken);

        if (result.Succeeded)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        return result;
    }

    private async Task<ReservationSessionStartResult> StageAndSaveAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        bool actorCanApproveComp,
        StartReservationSessionRequest request,
        Action<SessionStartStage> onCommittedStage,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations.SingleOrDefaultAsync(
            candidate => candidate.ReservationId == reservationId &&
                candidate.OrganizationId == request.OrganizationId,
            cancellationToken);
        if (reservation is null)
        {
            return ReservationSessionStartResult.Missing(
                "reservation_not_found",
                "Reservation was not found.");
        }

        var sessionRequest = CreateSessionRequest(reservation, request);
        var replay = await TryReplayAsync(reservation, request, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (reservation.StartedSessionId is not null || reservation.State == ReservationStateNames.Seated)
        {
            return ReservationSessionStartResult.RequestConflict(
                "reservation_already_started",
                "Reservation already started a session.",
                reservation.Version);
        }

        if (reservation.Version != request.ExpectedVersion)
        {
            return ReservationSessionStartResult.RequestConflict(
                "version_conflict",
                "Reservation changed since it was loaded.",
                reservation.Version);
        }

        if (reservation.State != ReservationStateNames.Confirmed)
        {
            return ReservationSessionStartResult.RequestConflict(
                "reservation_confirmation_required",
                "Reservation must be confirmed before a session can start.",
                reservation.Version);
        }

        var now = timeProvider.GetUtcNow();
        if (now >= reservation.EndsAtUtc)
        {
            return ReservationSessionStartResult.RequestConflict(
                "reservation_expired",
                "Reservation has expired.",
                reservation.Version);
        }

        if (reservation.SeatId is null)
        {
            return ReservationSessionStartResult.RequestConflict(
                "seat_unavailable",
                "Reservation has no assigned seat.",
                reservation.Version);
        }

        if (reservation.PlayerAccountId is null && RequiresPlayer(request))
        {
            return ReservationSessionStartResult.Invalid(
                "player_required_for_wallet",
                "The selected billing mode requires a linked player.");
        }

        if (InvalidLinkedPlayerBillingChoice(reservation, request) is { } billingChoiceError)
        {
            return ReservationSessionStartResult.Invalid(
                "session_start_invalid",
                billingChoiceError);
        }

        var stage = await sessionStartWorkflow.StageAsync(
            reservation.BranchId,
            actorStaffUserId,
            sessionRequest,
            actorCanApproveComp,
            cancellationToken);
        if (!stage.Result.Succeeded || stage.Result.Response is null)
        {
            return MapWorkflowFailure(stage.Result, reservation.Version);
        }

        StampCoordinatorRequestHash(reservation, sessionRequest, request);

        reservation.State = ReservationStateNames.Seated;
        reservation.SeatedAtUtc = now;
        reservation.StartedSessionId = stage.Result.Response.Session.SessionId;
        reservation.UpdatedByStaffUserId = actorStaffUserId;
        reservation.UpdatedAtUtc = now;
        reservation.Version += 1;

        await dbContext.SaveChangesAsync(cancellationToken);
        var response = new StartReservationSessionResponse(
            await ProjectAsync(reservation, cancellationToken),
            stage.Result.Response);
        onCommittedStage(stage);
        return ReservationSessionStartResult.Ok(response);
    }

    private async Task<ReservationSessionStartResult?> TryReplayAsync(
        ReservationEntity reservation,
        StartReservationSessionRequest request,
        CancellationToken cancellationToken)
    {
        var sessionRequest = CreateSessionRequest(reservation, request);
        var keyHash = SessionCommandIdempotencyKeyHasher.Hash(sessionRequest.IdempotencyKey);
        var existing = await dbContext.SessionCommandIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == reservation.OrganizationId &&
                    candidate.BranchId == reservation.BranchId &&
                    candidate.Operation == "start" &&
                    candidate.IdempotencyKeyHash == keyHash,
                cancellationToken);
        if (existing is null)
        {
            return null;
        }

        var requestHash = HashCoordinatorRequest(reservation.ReservationId, request);
        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return ReservationSessionStartResult.RequestConflict(
                "idempotency_conflict",
                "Idempotency key was already used for a different request.",
                reservation.Version);
        }

        var sessionResponse = JsonSerializer.Deserialize<SessionCommandResponse>(
            existing.ResponseJson,
            JsonOptions);
        if (sessionResponse is null ||
            reservation.StartedSessionId != sessionResponse.Session.SessionId)
        {
            return ReservationSessionStartResult.RequestConflict(
                "idempotency_conflict",
                "Stored idempotent response does not match this reservation.",
                reservation.Version);
        }

        return ReservationSessionStartResult.Ok(new StartReservationSessionResponse(
            await ProjectAsync(reservation, cancellationToken),
            sessionResponse));
    }

    private void StampCoordinatorRequestHash(
        ReservationEntity reservation,
        StartGuestSessionRequest sessionRequest,
        StartReservationSessionRequest request)
    {
        var keyHash = SessionCommandIdempotencyKeyHasher.Hash(sessionRequest.IdempotencyKey);
        var record = dbContext.SessionCommandIdempotency.Local.SingleOrDefault(
            candidate => candidate.OrganizationId == reservation.OrganizationId &&
                candidate.BranchId == reservation.BranchId &&
                candidate.Operation == "start" &&
                candidate.IdempotencyKeyHash == keyHash);
        if (record is null)
        {
            throw new InvalidOperationException(
                "Session start workflow did not stage its idempotency record.");
        }

        record.RequestHash = HashCoordinatorRequest(reservation.ReservationId, request);
    }

    private static string HashCoordinatorRequest(
        Guid reservationId,
        StartReservationSessionRequest request) =>
        SessionCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(
            new ReservationSessionCommandIdentity(reservationId, request),
            JsonOptions));

    private static StartGuestSessionRequest CreateSessionRequest(
        ReservationEntity reservation,
        StartReservationSessionRequest request) =>
        new(
            reservation.OrganizationId,
            reservation.SeatId ?? Guid.Empty,
            request.TariffRuleVersionId,
            $"reservation:{reservation.ReservationId:D}:{request.IdempotencyKey}",
            request.DurationMode,
            request.DurationMinutes,
            reservation.PlayerAccountId,
            request.BillingMode,
            request.TariffVersionId,
            request.PlayerPackageId,
            request.IsComp,
            request.CompReason);

    private static bool RequiresPlayer(StartReservationSessionRequest request) =>
        !string.IsNullOrWhiteSpace(request.BillingMode) || request.PlayerPackageId is not null;

    private static string? InvalidLinkedPlayerBillingChoice(
        ReservationEntity reservation,
        StartReservationSessionRequest request)
    {
        if (reservation.PlayerAccountId is null)
        {
            return null;
        }

        var billingMode = request.BillingMode.Trim();
        if (!request.IsComp && billingMode.Length == 0)
        {
            return "A linked player requires a billing mode.";
        }

        if (billingMode == BillingModeNames.Package && request.PlayerPackageId is null)
        {
            return "Package billing requires a player package.";
        }

        if (billingMode != BillingModeNames.Package && request.PlayerPackageId is not null)
        {
            return "A player package can only be used with package billing.";
        }

        return null;
    }

    private static ReservationSessionStartResult MapWorkflowFailure(
        SessionCommandServiceResult failure,
        int currentVersion)
    {
        var error = failure.Error ?? "Session could not be started.";
        if (error.Contains("seat", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("device", StringComparison.OrdinalIgnoreCase))
        {
            return ReservationSessionStartResult.RequestConflict(
                "seat_unavailable",
                error,
                currentVersion);
        }

        if (error.Contains("open shift", StringComparison.OrdinalIgnoreCase))
        {
            return ReservationSessionStartResult.Invalid("open_shift_required", error);
        }

        if (error.Contains("insufficient", StringComparison.OrdinalIgnoreCase))
        {
            return ReservationSessionStartResult.Invalid("insufficient_funds", error);
        }

        if (error.Contains("player account", StringComparison.OrdinalIgnoreCase))
        {
            return ReservationSessionStartResult.Invalid("player_required_for_wallet", error);
        }

        return failure.Conflict
            ? ReservationSessionStartResult.RequestConflict(
                failure.Code ?? "session_start_conflict",
                error,
                failure.CurrentVersion ?? currentVersion)
            : ReservationSessionStartResult.Invalid(
                failure.Code ?? "session_start_invalid",
                error);
    }

    private async Task<ReservationDto> ProjectAsync(
        ReservationEntity reservation,
        CancellationToken cancellationToken)
    {
        var seat = reservation.SeatId is null
            ? null
            : await dbContext.Seats.AsNoTracking().SingleOrDefaultAsync(
                candidate => candidate.SeatId == reservation.SeatId.Value,
                cancellationToken);
        var zoneName = seat is null
            ? null
            : await dbContext.Zones.AsNoTracking()
                .Where(zone => zone.ZoneId == seat.ZoneId)
                .Select(zone => zone.Name)
                .SingleOrDefaultAsync(cancellationToken);

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
            Math.Max(1, (int)Math.Round((reservation.EndsAtUtc - reservation.StartsAtUtc).TotalMinutes)),
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
    }

    private async Task<ReservationSessionStartResult> VersionConflictAsync(
        Guid reservationId,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.Reservations.AsNoTracking()
            .Where(candidate => candidate.ReservationId == reservationId &&
                candidate.OrganizationId == organizationId)
            .Select(candidate => (int?)candidate.Version)
            .SingleOrDefaultAsync(cancellationToken);
        return version is null
            ? ReservationSessionStartResult.Missing("reservation_not_found", "Reservation was not found.")
            : ReservationSessionStartResult.RequestConflict(
                "version_conflict",
                "Reservation changed since it was loaded.",
                version);
    }

    private async Task<ReservationSessionStartResult> ReevaluateConflictAsync(
        Guid reservationId,
        StartReservationSessionRequest request,
        CancellationToken cancellationToken)
    {
        var reservation = await dbContext.Reservations.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.ReservationId == reservationId &&
                candidate.OrganizationId == request.OrganizationId,
            cancellationToken);
        if (reservation is null)
        {
            return ReservationSessionStartResult.Missing(
                "reservation_not_found",
                "Reservation was not found.");
        }

        var replay = await TryReplayAsync(reservation, request, cancellationToken);
        return replay ?? (reservation.StartedSessionId is not null
            ? ReservationSessionStartResult.RequestConflict(
                "reservation_already_started",
                "Reservation already started a session.",
                reservation.Version)
            : ReservationSessionStartResult.RequestConflict(
                "seat_unavailable",
                "Seat already has an active session.",
                reservation.Version));
    }

    private static bool IsRetryable(Exception exception)
    {
        var postgres = FindPostgresException(exception);
        return postgres?.SqlState is PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected or
            PostgresErrorCodes.UniqueViolation;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
            {
                return postgres;
            }
        }

        return null;
    }

    private sealed record ReservationSessionCommandIdentity(
        Guid ReservationId,
        StartReservationSessionRequest Request);
}
