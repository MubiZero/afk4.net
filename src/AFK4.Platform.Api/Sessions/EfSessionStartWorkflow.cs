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

public sealed class EfSessionStartWorkflow(
    PlatformDbContext dbContext,
    IDeviceCommandDispatchService deviceCommandDispatchService,
    ISessionLeaseSigner leaseSigner,
    TimeProvider timeProvider,
    ISessionBillingService sessionBillingService,
    ISessionLifecycleNotifier lifecycleNotifier) : ISessionStartWorkflow
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

    public async Task<SessionStartStage> StageAsync(
        Guid branchId,
        Guid actorStaffUserId,
        StartGuestSessionRequest request,
        bool actorCanApproveComp,
        CancellationToken cancellationToken)
    {
        var durationMode = NormalizeDurationMode(request.DurationMode);
        if (durationMode is null)
        {
            return Invalid("Unsupported session duration mode.");
        }

        var isFixed = durationMode == SessionDurationModes.Fixed;
        var billingMode = (request.BillingMode ?? string.Empty).Trim();
        long? compValue = null;
        if (request.IsComp)
        {
            if (!string.IsNullOrEmpty(billingMode))
            {
                return Invalid("A comp (free) session cannot specify a billing mode.");
            }

            if ((request.CompReason?.Trim().Length ?? 0) < CompReasonMinLength)
            {
                return Invalid($"A comp session requires a reason of at least {CompReasonMinLength} characters.");
            }

            if (!isFixed || request.DurationMinutes is not > 0)
            {
                return Invalid("A comp session must have a fixed duration so its value can be assessed.");
            }

            if (request.TariffVersionId is null)
            {
                return Invalid("A comp session requires a tariff version to value the free time.");
            }

            var valuation = await sessionBillingService.ComputeCompValueAsync(
                request.OrganizationId,
                branchId,
                request.TariffVersionId.Value,
                request.DurationMinutes.Value,
                cancellationToken);
            if (!valuation.Succeeded)
            {
                return Invalid(valuation.Error ?? "Comp value could not be computed.");
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

            if (compValue > compThreshold && !actorCanApproveComp)
            {
                return Invalid($"Comp value {compValue} exceeds the {compThreshold} approval threshold; manager approval is required.");
            }
        }

        if (isFixed)
        {
            if (request.DurationMinutes is not > 0)
            {
                return Invalid("Fixed-duration sessions require a positive duration.");
            }
        }
        else if (billingMode is not ("" or BillingModeNames.PostpaidDebt))
        {
            return Invalid("Open-tab sessions support guest or postpaid billing only; choose a fixed duration for prepaid or package billing.");
        }

        var validationMinutes = isFixed ? request.DurationMinutes!.Value : 1;
        var assignment = await LoadActiveAssignmentAsync(
            request.OrganizationId,
            branchId,
            request.SeatId,
            cancellationToken);
        if (assignment is null)
        {
            return Invalid("Seat has no active approved device assignment.");
        }

        if (await HasBlockingSessionAsync(
            request.OrganizationId,
            branchId,
            request.SeatId,
            assignment.DeviceId,
            cancellationToken))
        {
            return Invalid("Seat or device already has an active session.");
        }

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
            return Invalid(billingValidation.Error ?? "Session billing validation failed.");
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
        AddEvent(session, actorStaffUserId, assignment.DeviceId, now);

        if (isFixed && request.PlayerAccountId is not null && !request.IsComp)
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
        var response = CreateResponse(request.IdempotencyKey, session, lease, [command], now);
        AddIdempotencyRecord(request, branchId, response, now);

        return new SessionStartStage(SessionCommandServiceResult.Ok(response), assignment.DeviceId, command);
    }

    public async Task NotifyCommittedAsync(SessionStartStage stage, CancellationToken cancellationToken)
    {
        if (!stage.Result.Succeeded || stage.Result.Response is null)
        {
            return;
        }

        if (stage.DeviceId is not null && stage.Command is not null)
        {
            await deviceCommandDispatchService.NotifyAsync(stage.DeviceId.Value, stage.Command, cancellationToken);
        }

        var session = stage.Result.Response.Session;
        await lifecycleNotifier.NotifyAsync(
            new SessionLifecycleChangedDto(
                OrganizationId: session.OrganizationId,
                BranchId: session.BranchId,
                SeatId: session.SeatId,
                SessionId: session.SessionId,
                Kind: SessionLifecycleKinds.Started,
                State: session.State,
                Version: session.Version,
                StartedAtUtc: session.StartedAtUtc,
                EndsAtUtc: session.EndsAtUtc,
                ObservedAtUtc: timeProvider.GetUtcNow()),
            cancellationToken);
    }

    private static SessionStartStage Invalid(string error) =>
        new(SessionCommandServiceResult.Invalid(error), null, null);

    private static string? NormalizeDurationMode(string? durationMode)
    {
        var normalized = (durationMode ?? string.Empty).Trim().ToLowerInvariant();
        return normalized.Length == 0
            ? SessionDurationModes.Open
            : SessionDurationModes.IsValid(normalized) ? normalized : null;
    }

    private async Task<DeviceSeatAssignmentEntity?> LoadActiveAssignmentAsync(
        Guid organizationId,
        Guid branchId,
        Guid seatId,
        CancellationToken cancellationToken) =>
        await (
            from assignment in dbContext.DeviceSeatAssignments.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking() on assignment.DeviceId equals device.DeviceId
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

    private Task<bool> HasBlockingSessionAsync(
        Guid organizationId,
        Guid branchId,
        Guid seatId,
        Guid deviceId,
        CancellationToken cancellationToken) =>
        dbContext.Sessions.AnyAsync(
            session => session.OrganizationId == organizationId &&
                session.BranchId == branchId &&
                BlockingStates.Contains(session.State) &&
                (session.SeatId == seatId || session.DeviceId == deviceId),
            cancellationToken);

    private static SessionLeaseEntity CreateLeaseEntity(SessionLeaseDto lease) => new()
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

    private void AddEvent(SessionEntity session, Guid actorStaffUserId, Guid deviceId, DateTimeOffset now) =>
        dbContext.SessionEvents.Add(new SessionEventEntity
        {
            SessionEventId = Guid.NewGuid(),
            SessionId = session.SessionId,
            OrganizationId = session.OrganizationId,
            BranchId = session.BranchId,
            EventType = "session-started",
            ActorStaffUserId = actorStaffUserId,
            DeviceId = deviceId,
            CreatedAtUtc = now,
            DetailsJson = "{}"
        });

    private void AddIdempotencyRecord(
        StartGuestSessionRequest request,
        Guid branchId,
        SessionCommandResponse response,
        DateTimeOffset now) =>
        dbContext.SessionCommandIdempotency.Add(new SessionCommandIdempotencyEntity
        {
            SessionCommandIdempotencyId = Guid.NewGuid(),
            OrganizationId = request.OrganizationId,
            BranchId = branchId,
            IdempotencyKeyHash = SessionCommandIdempotencyKeyHasher.Hash(request.IdempotencyKey),
            Operation = "start",
            RequestHash = SessionCommandIdempotencyKeyHasher.Hash(JsonSerializer.Serialize(request, JsonOptions)),
            ResponseJson = JsonSerializer.Serialize(response, JsonOptions),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(1)
        });

    private static SessionCommandResponse CreateResponse(
        string idempotencyKey,
        SessionEntity session,
        SessionLeaseDto lease,
        IReadOnlyList<DeviceCommandDto> commands,
        DateTimeOffset now) =>
        new(
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
                CurrentLease: lease,
                Version: session.Version),
            DeviceCommands: commands,
            CompValueMinorUnits: session.CompValueMinorUnits);
}
