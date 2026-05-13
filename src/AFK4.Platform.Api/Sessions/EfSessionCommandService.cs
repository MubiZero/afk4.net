using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

public sealed class EfSessionCommandService(
    PlatformDbContext dbContext,
    IDeviceCommandDispatchService deviceCommandDispatchService,
    ISessionLeaseSigner leaseSigner,
    TimeProvider timeProvider) : ISessionCommandService
{
    private const int LeaseMinutes = 15;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] BlockingStates =
    [
        SessionStateNames.Active,
        SessionStateNames.Paused,
        SessionStateNames.Ending
    ];

    public async Task<SessionCommandServiceResult> StartGuestSessionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        StartGuestSessionRequest request,
        CancellationToken cancellationToken)
    {
        var idempotency = await GetExistingIdempotencyAsync(
            request.OrganizationId,
            branchId,
            "start",
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.DurationMinutes <= 0)
        {
            return SessionCommandServiceResult.Invalid("Session duration must be positive.");
        }

        var assignment = await LoadActiveAssignmentAsync(
            request.OrganizationId,
            branchId,
            request.SeatId,
            cancellationToken);

        if (assignment is null)
        {
            return SessionCommandServiceResult.Invalid("Seat has no active device assignment.");
        }

        if (await HasBlockingSessionAsync(
            request.OrganizationId,
            branchId,
            request.SeatId,
            assignment.DeviceId,
            excludedSessionId: null,
            cancellationToken))
        {
            return SessionCommandServiceResult.Invalid("Seat or device already has an active session.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var sessionId = Guid.NewGuid();
            var endsAtUtc = now.AddMinutes(request.DurationMinutes);
            var lease = leaseSigner.Sign(
                sessionId,
                request.OrganizationId,
                branchId,
                request.SeatId,
                assignment.DeviceId,
                SessionStateNames.Active,
                Sequence: 1,
                IssuedAtUtc: now,
                ExpiresAtUtc: now.AddMinutes(LeaseMinutes));
            var leaseEntity = CreateLeaseEntity(lease);
            var session = new SessionEntity
            {
                SessionId = sessionId,
                OrganizationId = request.OrganizationId,
                BranchId = branchId,
                SeatId = request.SeatId,
                DeviceId = assignment.DeviceId,
                CreatedByStaffUserId = actorStaffUserId,
                PlayerKind = "guest",
                TariffRuleVersionId = request.TariffRuleVersionId,
                State = SessionStateNames.Active,
                RequestedAtUtc = now,
                StartedAtUtc = now,
                EndsAtUtc = endsAtUtc,
                CurrentLeaseId = leaseEntity.SessionLeaseId,
                UpdatedAtUtc = now
            };

            dbContext.Sessions.Add(session);
            dbContext.SessionLeases.Add(leaseEntity);
            AddEvent(session, "session-started", actorStaffUserId, deviceId: assignment.DeviceId, now);
            await dbContext.SaveChangesAsync(cancellationToken);

            var command = await deviceCommandDispatchService.DispatchAsync(
                assignment.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: "unlock",
                    Payload: new Dictionary<string, string>
                    {
                        ["sessionId"] = sessionId.ToString("D"),
                        ["sessionLease"] = JsonSerializer.Serialize(lease, JsonOptions),
                        ["reason"] = "session-start"
                    }),
                cancellationToken);
            var response = CreateResponse(request.IdempotencyKey, session, lease, [command], now);

            AddIdempotencyRecord(
                request.OrganizationId,
                branchId,
                "start",
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return SessionCommandServiceResult.Ok(response);
        }, cancellationToken);
    }

    public async Task<SessionCommandServiceResult> ExtendSessionAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        ExtendSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions.SingleOrDefaultAsync(
            candidate => candidate.SessionId == sessionId,
            cancellationToken);

        if (session is null)
        {
            return SessionCommandServiceResult.Missing("Session was not found.");
        }

        var idempotency = await GetExistingIdempotencyAsync(
            session.OrganizationId,
            session.BranchId,
            "extend",
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (request.AdditionalMinutes <= 0)
        {
            return SessionCommandServiceResult.Invalid("Additional minutes must be positive.");
        }

        if (session.State is not SessionStateNames.Active and not SessionStateNames.Paused)
        {
            return SessionCommandServiceResult.Invalid("Only active or paused sessions can be extended.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            session.TariffRuleVersionId = request.TariffRuleVersionId;
            session.EndsAtUtc = (session.EndsAtUtc ?? now).AddMinutes(request.AdditionalMinutes);
            session.UpdatedAtUtc = now;

            var lease = await IssueNextLeaseAsync(session, now, cancellationToken);
            AddEvent(session, "session-extended", actorStaffUserId, session.DeviceId, now);
            await dbContext.SaveChangesAsync(cancellationToken);

            var command = await deviceCommandDispatchService.DispatchAsync(
                session.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: "refresh-session-lease",
                    Payload: LeasePayload(session.SessionId, lease, "session-extend")),
                cancellationToken);
            var response = CreateResponse(request.IdempotencyKey, session, lease, [command], now);

            AddIdempotencyRecord(
                session.OrganizationId,
                session.BranchId,
                "extend",
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return SessionCommandServiceResult.Ok(response);
        }, cancellationToken);
    }

    public async Task<SessionCommandServiceResult> TransferSessionAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        TransferSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions.SingleOrDefaultAsync(
            candidate => candidate.SessionId == sessionId,
            cancellationToken);

        if (session is null)
        {
            return SessionCommandServiceResult.Missing("Session was not found.");
        }

        var idempotency = await GetExistingIdempotencyAsync(
            session.OrganizationId,
            session.BranchId,
            "transfer",
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (session.State != SessionStateNames.Active)
        {
            return SessionCommandServiceResult.Invalid("Only active sessions can be transferred.");
        }

        var assignment = await LoadActiveAssignmentAsync(
            session.OrganizationId,
            session.BranchId,
            request.TargetSeatId,
            cancellationToken);

        if (assignment is null)
        {
            return SessionCommandServiceResult.Invalid("Target seat has no active device assignment.");
        }

        if (await HasBlockingSessionAsync(
            session.OrganizationId,
            session.BranchId,
            request.TargetSeatId,
            assignment.DeviceId,
            excludedSessionId: session.SessionId,
            cancellationToken))
        {
            return SessionCommandServiceResult.Invalid("Target seat or device already has an active session.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            var oldDeviceId = session.DeviceId;
            session.SeatId = assignment.SeatId;
            session.DeviceId = assignment.DeviceId;
            session.UpdatedAtUtc = now;

            var lease = await IssueNextLeaseAsync(session, now, cancellationToken);
            AddEvent(session, "session-transferred", actorStaffUserId, assignment.DeviceId, now);
            await dbContext.SaveChangesAsync(cancellationToken);

            var lockCommand = await deviceCommandDispatchService.DispatchAsync(
                oldDeviceId,
                new CreateDeviceCommandRequest(
                    Type: "lock",
                    Payload: new Dictionary<string, string>
                    {
                        ["sessionId"] = session.SessionId.ToString("D"),
                        ["reason"] = "session-transfer"
                    }),
                cancellationToken);
            var unlockCommand = await deviceCommandDispatchService.DispatchAsync(
                assignment.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: "unlock",
                    Payload: LeasePayload(session.SessionId, lease, "session-transfer")),
                cancellationToken);
            var response = CreateResponse(request.IdempotencyKey, session, lease, [lockCommand, unlockCommand], now);

            AddIdempotencyRecord(
                session.OrganizationId,
                session.BranchId,
                "transfer",
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return SessionCommandServiceResult.Ok(response);
        }, cancellationToken);
    }

    public async Task<SessionCommandServiceResult> EndSessionAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        EndSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions.SingleOrDefaultAsync(
            candidate => candidate.SessionId == sessionId,
            cancellationToken);

        if (session is null)
        {
            return SessionCommandServiceResult.Missing("Session was not found.");
        }

        var idempotency = await GetExistingIdempotencyAsync(
            session.OrganizationId,
            session.BranchId,
            "end",
            request.IdempotencyKey,
            request,
            cancellationToken);

        if (idempotency is not null)
        {
            return idempotency;
        }

        if (session.State is not SessionStateNames.Active and not SessionStateNames.Paused)
        {
            return SessionCommandServiceResult.Invalid("Only active or paused sessions can be ended.");
        }

        return await ExecuteInTransactionAsync(async () =>
        {
            var now = timeProvider.GetUtcNow();
            session.State = SessionStateNames.Ending;
            session.UpdatedAtUtc = now;
            AddEvent(session, "session-ending", actorStaffUserId, session.DeviceId, now);
            await dbContext.SaveChangesAsync(cancellationToken);

            var command = await deviceCommandDispatchService.DispatchAsync(
                session.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: "lock",
                    Payload: new Dictionary<string, string>
                    {
                        ["sessionId"] = session.SessionId.ToString("D"),
                        ["reason"] = string.IsNullOrWhiteSpace(request.Reason) ? "session-end" : request.Reason.Trim()
                    }),
                cancellationToken);
            var response = CreateResponse(request.IdempotencyKey, session, CurrentLease: null, [command], now);

            AddIdempotencyRecord(
                session.OrganizationId,
                session.BranchId,
                "end",
                request.IdempotencyKey,
                request,
                response,
                now);
            await dbContext.SaveChangesAsync(cancellationToken);

            return SessionCommandServiceResult.Ok(response);
        }, cancellationToken);
    }

    private async Task<SessionCommandServiceResult?> GetExistingIdempotencyAsync<TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return SessionCommandServiceResult.Invalid("Idempotency key is required.");
        }

        var requestHash = HashRequest(request);
        var idempotencyKeyHash = SessionCommandIdempotencyKeyHasher.Hash(idempotencyKey);
        var existing = await dbContext.SessionCommandIdempotency
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.OrganizationId == organizationId &&
                    candidate.BranchId == branchId &&
                    candidate.Operation == operation &&
                    candidate.IdempotencyKeyHash == idempotencyKeyHash,
                cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return SessionCommandServiceResult.RequestConflict("Idempotency key was already used for a different request.");
        }

        var response = JsonSerializer.Deserialize<SessionCommandResponse>(existing.ResponseJson, JsonOptions);

        return response is null
            ? SessionCommandServiceResult.Invalid("Stored idempotent response could not be read.")
            : SessionCommandServiceResult.Ok(response);
    }

    private async Task<DeviceSeatAssignmentEntity?> LoadActiveAssignmentAsync(
        Guid organizationId,
        Guid branchId,
        Guid seatId,
        CancellationToken cancellationToken)
    {
        return await dbContext.DeviceSeatAssignments
            .AsNoTracking()
            .Where(assignment =>
                assignment.OrganizationId == organizationId &&
                assignment.BranchId == branchId &&
                assignment.SeatId == seatId &&
                assignment.DetachedAtUtc == null)
            .OrderByDescending(assignment => assignment.AttachedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> HasBlockingSessionAsync(
        Guid organizationId,
        Guid branchId,
        Guid seatId,
        Guid deviceId,
        Guid? excludedSessionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Sessions.AnyAsync(
            session =>
                session.OrganizationId == organizationId &&
                session.BranchId == branchId &&
                session.SessionId != excludedSessionId &&
                BlockingStates.Contains(session.State) &&
                (session.SeatId == seatId || session.DeviceId == deviceId),
            cancellationToken);
    }

    private async Task<SessionLeaseDto> IssueNextLeaseAsync(
        SessionEntity session,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var previousSequence = await dbContext.SessionLeases
            .Where(lease => lease.SessionId == session.SessionId)
            .Select(lease => (int?)lease.Sequence)
            .MaxAsync(cancellationToken) ?? 0;
        var lease = leaseSigner.Sign(
            session.SessionId,
            session.OrganizationId,
            session.BranchId,
            session.SeatId,
            session.DeviceId,
            session.State,
            previousSequence + 1,
            now,
            now.AddMinutes(LeaseMinutes));
        var leaseEntity = CreateLeaseEntity(lease);
        session.CurrentLeaseId = leaseEntity.SessionLeaseId;
        dbContext.SessionLeases.Add(leaseEntity);

        return lease;
    }

    private static SessionLeaseEntity CreateLeaseEntity(SessionLeaseDto lease)
    {
        return new SessionLeaseEntity
        {
            SessionLeaseId = Guid.NewGuid(),
            SessionId = lease.SessionId,
            OrganizationId = lease.OrganizationId,
            BranchId = lease.BranchId,
            SeatId = lease.SeatId,
            DeviceId = lease.DeviceId,
            State = lease.State,
            Sequence = lease.Sequence,
            IssuedAtUtc = lease.IssuedAtUtc,
            ExpiresAtUtc = lease.ExpiresAtUtc,
            SignatureAlgorithm = lease.SignatureAlgorithm,
            Signature = lease.Signature,
            PayloadJson = JsonSerializer.Serialize(lease, JsonOptions)
        };
    }

    private void AddEvent(
        SessionEntity session,
        string eventType,
        Guid? actorStaffUserId,
        Guid? deviceId,
        DateTimeOffset now)
    {
        dbContext.SessionEvents.Add(new SessionEventEntity
        {
            SessionEventId = Guid.NewGuid(),
            SessionId = session.SessionId,
            OrganizationId = session.OrganizationId,
            BranchId = session.BranchId,
            EventType = eventType,
            ActorStaffUserId = actorStaffUserId,
            DeviceId = deviceId,
            CreatedAtUtc = now,
            DetailsJson = "{}"
        });
    }

    private void AddIdempotencyRecord<TRequest>(
        Guid organizationId,
        Guid branchId,
        string operation,
        string idempotencyKey,
        TRequest request,
        SessionCommandResponse response,
        DateTimeOffset now)
    {
        dbContext.SessionCommandIdempotency.Add(new SessionCommandIdempotencyEntity
        {
            SessionCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            IdempotencyKeyHash = SessionCommandIdempotencyKeyHasher.Hash(idempotencyKey),
            Operation = operation,
            RequestHash = HashRequest(request),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });
    }

    private SessionCommandResponse CreateResponse(
        string idempotencyKey,
        SessionEntity session,
        SessionLeaseDto? CurrentLease,
        IReadOnlyList<DeviceCommandDto> commands,
        DateTimeOffset now)
    {
        return new SessionCommandResponse(
            IdempotencyKey: idempotencyKey,
            Session: new SessionDto(
                SessionId: session.SessionId,
                OrganizationId: session.OrganizationId,
                BranchId: session.BranchId,
                SeatId: session.SeatId,
                DeviceId: session.DeviceId,
                State: session.State,
                TariffRuleVersionId: session.TariffRuleVersionId,
                StartedAtUtc: session.StartedAtUtc,
                EndsAtUtc: session.EndsAtUtc,
                EndedAtUtc: session.EndedAtUtc,
                RemainingSeconds: session.EndsAtUtc is null
                    ? null
                    : Math.Max(0, (int)(session.EndsAtUtc.Value - now).TotalSeconds),
                CurrentLease: CurrentLease),
            DeviceCommands: commands);
    }

    private static Dictionary<string, string> LeasePayload(Guid sessionId, SessionLeaseDto lease, string reason)
    {
        return new Dictionary<string, string>
        {
            ["sessionId"] = sessionId.ToString("D"),
            ["sessionLease"] = JsonSerializer.Serialize(lease, JsonOptions),
            ["reason"] = reason
        };
    }

    private static string HashRequest<TRequest>(TRequest request)
    {
        return SessionCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions));
    }

    private async Task<SessionCommandServiceResult> ExecuteInTransactionAsync(
        Func<Task<SessionCommandServiceResult>> action,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await action();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);

        return result;
    }
}
