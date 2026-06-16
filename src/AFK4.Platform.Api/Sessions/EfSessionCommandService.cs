using System.Data;
using System.Text.Json;
using AFK4.Platform.Api.AntiFraud;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

public sealed class EfSessionCommandService(
    PlatformDbContext dbContext,
    IDeviceCommandDispatchService deviceCommandDispatchService,
    ISessionLeaseSigner leaseSigner,
    TimeProvider timeProvider,
    ISessionBillingService sessionBillingService,
    ISessionLifecycleNotifier lifecycleNotifier) : ISessionCommandService
{
    private const int LeaseMinutes = 15;
    private const int CompReasonMinLength = 8;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] BlockingStates =
    [
        SessionStateNames.Active,
        SessionStateNames.Paused,
        SessionStateNames.Ending
    ];

    private static string? NormalizeDurationMode(string? durationMode)
    {
        var normalized = (durationMode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return SessionDurationModes.Open;
        }

        return SessionDurationModes.IsValid(normalized) ? normalized : null;
    }

    public async Task<SessionCommandServiceResult> StartGuestSessionAsync(
        Guid branchId,
        Guid actorStaffUserId,
        StartGuestSessionRequest request,
        CancellationToken cancellationToken,
        bool actorCanApproveComp = false)
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

        var durationMode = NormalizeDurationMode(request.DurationMode);
        if (durationMode is null)
        {
            return SessionCommandServiceResult.Invalid("Unsupported session duration mode.");
        }

        var isFixed = durationMode == SessionDurationModes.Fixed;
        var billingMode = (request.BillingMode ?? string.Empty).Trim();

        // §5.4: an explicit comp is a free session — it must carry no billing mode and a real reason.
        // The control fires only on the IsComp flag; the existing manual/guest path is untouched.
        long? compValue = null;
        if (request.IsComp)
        {
            if (!string.IsNullOrEmpty(billingMode))
            {
                return SessionCommandServiceResult.Invalid("A comp (free) session cannot specify a billing mode.");
            }

            if ((request.CompReason?.Trim().Length ?? 0) < CompReasonMinLength)
            {
                return SessionCommandServiceResult.Invalid(
                    $"A comp session requires a reason of at least {CompReasonMinLength} characters.");
            }

            // A comp grants a fixed amount of free time at a real tariff, so its value
            // (duration × tariff) is known up front and the gate is always preventive.
            if (!isFixed || request.DurationMinutes is not > 0)
            {
                return SessionCommandServiceResult.Invalid(
                    "A comp session must have a fixed duration so its value can be assessed.");
            }

            if (request.TariffVersionId is null)
            {
                return SessionCommandServiceResult.Invalid(
                    "A comp session requires a tariff version to value the free time.");
            }

            var valuation = await sessionBillingService.ComputeCompValueAsync(
                request.OrganizationId,
                branchId,
                request.TariffVersionId.Value,
                request.DurationMinutes.Value,
                cancellationToken);
            if (!valuation.Succeeded)
            {
                return SessionCommandServiceResult.Invalid(valuation.Error ?? "Comp value could not be computed.");
            }

            compValue = valuation.AmountMinorUnits;

            var branch = await dbContext.Branches
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    candidate => candidate.OrganizationId == request.OrganizationId && candidate.BranchId == branchId,
                    cancellationToken);
            var compThreshold = MoneyControlPolicy.ResolveCompThreshold(
                branch?.CompApprovalThresholdMinorUnits,
                MoneyControlPolicy.ResolveApprovalThreshold(
                    branch?.HighRiskApprovalThresholdMinorUnits,
                    MoneyControlPolicy.DefaultApprovalThresholdMinorUnits));

            // Over the comp threshold, only an actor who can approve money actions may proceed
            // (a manager comping directly). Otherwise the free session is blocked.
            if (compValue > compThreshold && !actorCanApproveComp)
            {
                return SessionCommandServiceResult.Invalid(
                    $"Comp value {compValue} exceeds the {compThreshold} approval threshold; manager approval is required.");
            }
        }

        if (isFixed)
        {
            if (request.DurationMinutes is not > 0)
            {
                return SessionCommandServiceResult.Invalid("Fixed-duration sessions require a positive duration.");
            }
        }
        else if (billingMode is not ("" or BillingModeNames.PostpaidDebt))
        {
            // Open tab has no known amount up front, so prepaid/package cannot use it.
            return SessionCommandServiceResult.Invalid(
                "Open-tab sessions support guest or postpaid billing only; choose a fixed duration for prepaid or package billing.");
        }

        // For validation we need a positive duration to resolve tariff/player/shift.
        // Open tabs defer the real charge to checkout, so a nominal minute is enough here.
        var validationMinutes = isFixed ? request.DurationMinutes!.Value : 1;

        var assignment = await LoadActiveAssignmentAsync(
            request.OrganizationId,
            branchId,
            request.SeatId,
            cancellationToken);

        if (assignment is null)
        {
            return SessionCommandServiceResult.Invalid("Seat has no active approved device assignment.");
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

        Guid? deviceIdToNotify = null;
        DeviceCommandDto? commandToNotify = null;
        SessionCommandServiceResult result;
        try
        {
            result = await ExecuteInTransactionAsync(async () =>
        {
            var billingValidation = await sessionBillingService.ValidateStartAsync(
                request.OrganizationId,
                branchId,
                request.PlayerAccountId,
                billingMode,
                request.TariffVersionId,
                request.PlayerPackageId,
                validationMinutes,
                cancellationToken);

            if (!billingValidation.Succeeded)
            {
                return SessionCommandServiceResult.Invalid(billingValidation.Error ?? "Session billing validation failed.");
            }

            var now = timeProvider.GetUtcNow();
            var sessionId = Guid.NewGuid();
            DateTimeOffset? endsAtUtc = isFixed ? now.AddMinutes(request.DurationMinutes!.Value) : null;
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
                PlayerAccountId = request.PlayerAccountId,
                TariffRuleVersionId = string.IsNullOrWhiteSpace(billingValidation.TariffRuleVersionId)
                    ? request.TariffRuleVersionId
                    : billingValidation.TariffRuleVersionId,
                BillingMode = billingMode,
                State = SessionStateNames.Active,
                RequestedAtUtc = now,
                StartedAtUtc = now,
                EndsAtUtc = endsAtUtc,
                CurrentLeaseId = leaseEntity.SessionLeaseId,
                IsComp = request.IsComp,
                CompValueMinorUnits = compValue,
                UpdatedAtUtc = now,
                Version = 1
            };

            dbContext.Sessions.Add(session);
            dbContext.SessionLeases.Add(leaseEntity);
            AddEvent(session, "session-started", actorStaffUserId, deviceId: assignment.DeviceId, now);
            await dbContext.SaveChangesAsync(cancellationToken);

            // Open-tab postpaid defers its charge to checkout; only fixed-duration
            // (and prepaid/package) sessions write the charge at start.
            if (isFixed && request.PlayerAccountId is not null)
            {
                await sessionBillingService.AppendStartLedgerEntriesAsync(
                    sessionId,
                    actorStaffUserId,
                    billingValidation,
                    request.PlayerAccountId.Value,
                    request.PlayerPackageId,
                    billingMode,
                    now,
                    cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var command = await deviceCommandDispatchService.EnqueueAsync(
                assignment.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: DeviceCommandTypeNames.Unlock,
                    Payload: new Dictionary<string, string>
                    {
                        ["sessionId"] = sessionId.ToString("D"),
                        ["sessionLease"] = JsonSerializer.Serialize(lease, JsonOptions),
                        ["reason"] = "session-start"
                    }),
                cancellationToken);
            deviceIdToNotify = assignment.DeviceId;
            commandToNotify = command;
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
        }, IsolationLevel.Serializable, cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique partial index on an active seat fired: another start won the race between
            // our occupancy pre-check and our insert. The database, not timing, decides the winner.
            return SessionCommandServiceResult.RequestConflict(
                "Seat already has an active session.", "seat_occupied");
        }

        if (result.Succeeded && deviceIdToNotify is not null && commandToNotify is not null)
        {
            await deviceCommandDispatchService.NotifyAsync(deviceIdToNotify.Value, commandToNotify, cancellationToken);
        }

        if (result.Succeeded && result.Response is not null)
        {
            await NotifyLifecycleAsync(result.Response.Session, SessionLifecycleKinds.Started, cancellationToken);
        }

        return result;
    }

    // A mutating command may carry the version the caller last saw. If it no longer matches the
    // authoritative row, the caller's view is stale and loses the race with a 409 instead of
    // silently double-acting. No expected version (legacy/idempotent caller) skips the check and
    // relies on the DB concurrency token to catch a genuine concurrent write.
    private static SessionCommandServiceResult? CheckExpectedVersion(SessionEntity session, int? expectedVersion)
    {
        return expectedVersion is int expected && expected != session.Version
            ? SessionCommandServiceResult.StaleVersion(session.Version)
            : null;
    }

    // Broadcasts a lifecycle change from the committed response so clients can patch the floor map
    // and apply a dashboard delta. The post-commit Version lets clients order/dedupe events.
    private Task NotifyLifecycleAsync(SessionDto session, string kind, CancellationToken cancellationToken)
    {
        return lifecycleNotifier.NotifyAsync(
            new SessionLifecycleChangedDto(
                OrganizationId: session.OrganizationId,
                BranchId: session.BranchId,
                SeatId: session.SeatId,
                SessionId: session.SessionId,
                Kind: kind,
                State: session.State,
                Version: session.Version,
                StartedAtUtc: session.StartedAtUtc,
                EndsAtUtc: session.EndsAtUtc,
                ObservedAtUtc: timeProvider.GetUtcNow()),
            cancellationToken);
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

        if (CheckExpectedVersion(session, request.ExpectedVersion) is { } extendStale)
        {
            return extendStale;
        }

        if (session.PlayerAccountId is not null &&
            request.PlayerAccountId is not null &&
            session.PlayerAccountId.Value != request.PlayerAccountId.Value)
        {
            return SessionCommandServiceResult.Invalid("Extend request player account must match the session player account.");
        }

        var playerAccountId = session.PlayerAccountId ?? request.PlayerAccountId;

        // Money-path guard: an extend that omits the billing mode inherits what the session was
        // started with — never silently fall back to a free guest top-up for a paid session. An
        // explicit mode in the request still wins (e.g. an operator comping the extension).
        var effectiveBillingMode = string.IsNullOrWhiteSpace(request.BillingMode)
            ? session.BillingMode
            : request.BillingMode;

        Guid? deviceIdToNotify = null;
        DeviceCommandDto? commandToNotify = null;
        var result = await ExecuteVersionedMutationAsync(sessionId, async () =>
        {
            var billingValidation = await sessionBillingService.ValidateExtendAsync(
                session.OrganizationId,
                session.BranchId,
                playerAccountId,
                effectiveBillingMode,
                request.TariffVersionId,
                request.PlayerPackageId,
                request.AdditionalMinutes,
                cancellationToken);

            if (!billingValidation.Succeeded)
            {
                return SessionCommandServiceResult.Invalid(billingValidation.Error ?? "Session billing validation failed.");
            }

            var now = timeProvider.GetUtcNow();
            session.PlayerAccountId = playerAccountId;
            session.TariffRuleVersionId = string.IsNullOrWhiteSpace(billingValidation.TariffRuleVersionId)
                ? request.TariffRuleVersionId
                : billingValidation.TariffRuleVersionId;
            session.EndsAtUtc = (session.EndsAtUtc ?? now).AddMinutes(request.AdditionalMinutes);
            session.UpdatedAtUtc = now;
            session.Version += 1;

            var lease = await IssueNextLeaseAsync(session, now, cancellationToken);
            AddEvent(session, "session-extended", actorStaffUserId, session.DeviceId, now);

            if (playerAccountId is not null)
            {
                await sessionBillingService.AppendExtendLedgerEntriesAsync(
                    session.SessionId,
                    actorStaffUserId,
                    billingValidation,
                    playerAccountId.Value,
                    request.PlayerPackageId,
                    effectiveBillingMode,
                    now,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            var command = await deviceCommandDispatchService.EnqueueAsync(
                session.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: DeviceCommandTypeNames.RefreshSessionLease,
                    Payload: LeasePayload(session.SessionId, lease, "session-extend")),
                cancellationToken);
            deviceIdToNotify = session.DeviceId;
            commandToNotify = command;
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
        }, IsolationLevel.Serializable, cancellationToken);

        if (result.Succeeded && deviceIdToNotify is not null && commandToNotify is not null)
        {
            await deviceCommandDispatchService.NotifyAsync(deviceIdToNotify.Value, commandToNotify, cancellationToken);
        }

        if (result.Succeeded && result.Response is not null)
        {
            await NotifyLifecycleAsync(result.Response.Session, SessionLifecycleKinds.Extended, cancellationToken);
        }

        return result;
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

        if (CheckExpectedVersion(session, request.ExpectedVersion) is { } transferStale)
        {
            return transferStale;
        }

        var assignment = await LoadActiveAssignmentAsync(
            session.OrganizationId,
            session.BranchId,
            request.TargetSeatId,
            cancellationToken);

        if (assignment is null)
        {
            return SessionCommandServiceResult.Invalid("Target seat has no active approved device assignment.");
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

        var commandsToNotify = new List<(Guid DeviceId, DeviceCommandDto Command)>();
        var result = await ExecuteVersionedMutationAsync(sessionId, async () =>
        {
            var now = timeProvider.GetUtcNow();
            var oldDeviceId = session.DeviceId;
            session.SeatId = assignment.SeatId;
            session.DeviceId = assignment.DeviceId;
            session.UpdatedAtUtc = now;
            session.Version += 1;

            var lease = await IssueNextLeaseAsync(session, now, cancellationToken);
            AddEvent(session, "session-transferred", actorStaffUserId, assignment.DeviceId, now);
            await dbContext.SaveChangesAsync(cancellationToken);

            var lockCommand = await deviceCommandDispatchService.EnqueueAsync(
                oldDeviceId,
                new CreateDeviceCommandRequest(
                    Type: DeviceCommandTypeNames.Lock,
                    Payload: new Dictionary<string, string>
                    {
                        ["sessionId"] = session.SessionId.ToString("D"),
                        ["reason"] = "session-transfer"
                    }),
                cancellationToken);
            commandsToNotify.Add((oldDeviceId, lockCommand));
            var unlockCommand = await deviceCommandDispatchService.EnqueueAsync(
                assignment.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: DeviceCommandTypeNames.Unlock,
                    Payload: LeasePayload(session.SessionId, lease, "session-transfer")),
                cancellationToken);
            commandsToNotify.Add((assignment.DeviceId, unlockCommand));
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
        }, isolationLevel: null, cancellationToken);

        if (result.Succeeded)
        {
            foreach (var (deviceId, command) in commandsToNotify)
            {
                await deviceCommandDispatchService.NotifyAsync(deviceId, command, cancellationToken);
            }
        }

        if (result.Succeeded && result.Response is not null)
        {
            await NotifyLifecycleAsync(result.Response.Session, SessionLifecycleKinds.Transferred, cancellationToken);
        }

        return result;
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

        if (session.State == SessionStateNames.Ending)
        {
            var now = timeProvider.GetUtcNow();
            var commands = await GetPendingEndCommandsAsync(session, cancellationToken);
            var response = CreateResponse(request.IdempotencyKey, session, CurrentLease: null, commands, now);
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
        }

        if (session.State is not SessionStateNames.Active and not SessionStateNames.Paused)
        {
            return SessionCommandServiceResult.Invalid("Only active or paused sessions can be ended.");
        }

        if (CheckExpectedVersion(session, request.ExpectedVersion) is { } endStale)
        {
            return endStale;
        }

        Guid? deviceIdToNotify = null;
        DeviceCommandDto? commandToNotify = null;
        var result = await ExecuteVersionedMutationAsync(sessionId, async () =>
        {
            var now = timeProvider.GetUtcNow();
            session.State = SessionStateNames.Ending;
            session.UpdatedAtUtc = now;
            session.Version += 1;
            AddEvent(session, "session-ending", actorStaffUserId, session.DeviceId, now);
            await dbContext.SaveChangesAsync(cancellationToken);

            var command = await deviceCommandDispatchService.EnqueueAsync(
                session.DeviceId,
                new CreateDeviceCommandRequest(
                    Type: DeviceCommandTypeNames.Lock,
                    Payload: new Dictionary<string, string>
                    {
                        ["sessionId"] = session.SessionId.ToString("D"),
                        ["reason"] = string.IsNullOrWhiteSpace(request.Reason) ? "session-end" : request.Reason.Trim()
                    }),
                cancellationToken);
            deviceIdToNotify = session.DeviceId;
            commandToNotify = command;
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
        }, isolationLevel: null, cancellationToken);

        if (result.Succeeded && deviceIdToNotify is not null && commandToNotify is not null)
        {
            await deviceCommandDispatchService.NotifyAsync(deviceIdToNotify.Value, commandToNotify, cancellationToken);
        }

        if (result.Succeeded && result.Response is not null)
        {
            await NotifyLifecycleAsync(result.Response.Session, SessionLifecycleKinds.Ended, cancellationToken);
        }

        return result;
    }

    private async Task<IReadOnlyList<DeviceCommandDto>> GetPendingEndCommandsAsync(
        SessionEntity session,
        CancellationToken cancellationToken)
    {
        var commands = await dbContext.DeviceCommands
            .AsNoTracking()
            .Where(command => command.DeviceId == session.DeviceId && command.Type == DeviceCommandTypeNames.Lock)
            .OrderByDescending(command => command.CreatedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken);

        return commands
            .Where(command => string.Equals(command.Status, "pending", StringComparison.OrdinalIgnoreCase))
            .Select(ToDeviceCommandDto)
            .Where(command => command.Payload.TryGetValue("sessionId", out var sessionId) &&
                string.Equals(sessionId, session.SessionId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            .ToList();
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
        return await (
            from assignment in dbContext.DeviceSeatAssignments.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking()
                on assignment.DeviceId equals device.DeviceId
            where assignment.OrganizationId == organizationId &&
                assignment.BranchId == branchId &&
                assignment.SeatId == seatId &&
                assignment.DetachedAtUtc == null &&
                device.OrganizationId == organizationId &&
                device.BranchId == branchId &&
                device.EnrollmentState == DeviceEnrollmentStateNames.Approved
            orderby assignment.AttachedAtUtc descending
            select assignment)
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
                CurrentLease: CurrentLease,
                Version: session.Version),
            DeviceCommands: commands,
            CompValueMinorUnits: session.CompValueMinorUnits);
    }

    private static DeviceCommandDto ToDeviceCommandDto(DeviceCommandEntity command)
    {
        return new DeviceCommandDto(
            CommandId: command.CommandId,
            Type: command.Type,
            CreatedAtUtc: command.CreatedAtUtc,
            Payload: ParseCommandPayload(command.PayloadJson));
    }

    private static IReadOnlyDictionary<string, string> ParseCommandPayload(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(payloadJson, JsonOptions)
                ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
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

    // Runs a session mutation and converts a lost optimistic-concurrency race (the DB concurrency
    // token mismatched on commit) into a typed 409 carrying the now-current version, so a caller
    // that omitted ExpectedVersion still cannot silently double-act under a genuine simultaneous write.
    private async Task<SessionCommandServiceResult> ExecuteVersionedMutationAsync(
        Guid sessionId,
        Func<Task<SessionCommandServiceResult>> action,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteInTransactionAsync(action, isolationLevel, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            var currentVersion = await dbContext.Sessions
                .AsNoTracking()
                .Where(session => session.SessionId == sessionId)
                .Select(session => (int?)session.Version)
                .SingleOrDefaultAsync(cancellationToken) ?? 0;
            return SessionCommandServiceResult.StaleVersion(currentVersion);
        }
    }

    private async Task<SessionCommandServiceResult> ExecuteInTransactionAsync(
        Func<Task<SessionCommandServiceResult>> action,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await action();
        }

        await using var transaction = isolationLevel is null
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : await dbContext.Database.BeginTransactionAsync(isolationLevel.Value, cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);

        return result;
    }

    private Task<SessionCommandServiceResult> ExecuteInTransactionAsync(
        Func<Task<SessionCommandServiceResult>> action,
        CancellationToken cancellationToken)
    {
        return ExecuteInTransactionAsync(action, isolationLevel: null, cancellationToken);
    }
}
