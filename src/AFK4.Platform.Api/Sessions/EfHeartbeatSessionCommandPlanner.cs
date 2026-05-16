using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Sessions;

public sealed class EfHeartbeatSessionCommandPlanner(
    PlatformDbContext dbContext,
    ISessionLeaseSigner leaseSigner,
    IOptions<SessionLeaseOptions> leaseOptions,
    TimeProvider timeProvider) : IHeartbeatSessionCommandPlanner
{
    private const string HeartbeatLeaseEventType = "session-lease-refreshed-by-heartbeat";
    private static readonly TimeSpan RefreshThreshold = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] PlanningSessionStates =
    [
        SessionStateNames.Active,
        SessionStateNames.Paused,
        SessionStateNames.Ending
    ];
    private static readonly string[] SessionCommandTypes =
    [
        DeviceCommandTypeNames.Lock,
        DeviceCommandTypeNames.Unlock,
        DeviceCommandTypeNames.RefreshSessionLease
    ];

    public async Task<IReadOnlyList<HeartbeatSessionCommandPlan>> PlanAsync(
        Guid deviceId,
        DeviceHeartbeatRequest heartbeat,
        CancellationToken cancellationToken)
    {
        var session = await dbContext.Sessions
            .Where(candidate =>
                candidate.OrganizationId == heartbeat.OrganizationId &&
                candidate.BranchId == heartbeat.BranchId &&
                candidate.DeviceId == deviceId &&
                PlanningSessionStates.Contains(candidate.State))
            .OrderByDescending(candidate => candidate.UpdatedAtUtc)
            .ThenByDescending(candidate => candidate.RequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return heartbeat.ActiveSessionId is null
                ? []
                : await PlanLockAsync(
                    deviceId,
                    heartbeat.ActiveSessionId.Value,
                    "heartbeat-session-missing",
                    cancellationToken);
        }

        if (session.State == SessionStateNames.Ending)
        {
            return await PlanLockAsync(
                deviceId,
                session.SessionId,
                "heartbeat-session-ending",
                cancellationToken);
        }

        if (await HasPendingSessionCommandAsync(deviceId, session.SessionId, cancellationToken))
        {
            return [];
        }

        var now = timeProvider.GetUtcNow();
        var hasMatchingLocalSession = heartbeat.ActiveSessionId == session.SessionId;
        if (!hasMatchingLocalSession)
        {
            var lease = await IssueNextLeaseAsync(
                session,
                now,
                "heartbeat-session-continue",
                cancellationToken);

            return
            [
                new HeartbeatSessionCommandPlan(
                    deviceId,
                    new CreateDeviceCommandRequest(
                        DeviceCommandTypeNames.Unlock,
                        LeasePayload(session.SessionId, lease, "heartbeat-session-continue")))
            ];
        }

        var latestLeaseSequence = await LoadLatestLeaseSequenceAsync(session.SessionId, cancellationToken);
        var leaseIsNearExpiry = heartbeat.ActiveSessionLeaseExpiresAtUtc is null ||
            heartbeat.ActiveSessionLeaseExpiresAtUtc.Value <= now.Add(RefreshThreshold);
        var leaseIsMissingOrStale = latestLeaseSequence == 0 ||
            heartbeat.ActiveSessionLeaseSequence is null ||
            heartbeat.ActiveSessionLeaseSequence.Value < latestLeaseSequence;

        if (!leaseIsNearExpiry && !leaseIsMissingOrStale)
        {
            return [];
        }

        var refreshedLease = await IssueNextLeaseAsync(
            session,
            now,
            "heartbeat-lease-refresh",
            cancellationToken);

        return
        [
            new HeartbeatSessionCommandPlan(
                deviceId,
                new CreateDeviceCommandRequest(
                    DeviceCommandTypeNames.RefreshSessionLease,
                    LeasePayload(session.SessionId, refreshedLease, "heartbeat-lease-refresh")))
        ];
    }

    private async Task<IReadOnlyList<HeartbeatSessionCommandPlan>> PlanLockAsync(
        Guid deviceId,
        Guid sessionId,
        string reason,
        CancellationToken cancellationToken)
    {
        if (await HasPendingSessionCommandAsync(deviceId, sessionId, cancellationToken))
        {
            return [];
        }

        return
        [
            new HeartbeatSessionCommandPlan(
                deviceId,
                new CreateDeviceCommandRequest(
                    DeviceCommandTypeNames.Lock,
                    new Dictionary<string, string>
                    {
                        ["sessionId"] = sessionId.ToString("D"),
                        ["reason"] = reason
                    }))
        ];
    }

    private async Task<bool> HasPendingSessionCommandAsync(
        Guid deviceId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var commands = await dbContext.DeviceCommands
            .AsNoTracking()
            .Where(command =>
                command.DeviceId == deviceId &&
                command.Status == "Pending" &&
                SessionCommandTypes.Contains(command.Type))
            .Select(command => command.PayloadJson)
            .ToListAsync(cancellationToken);

        return commands.Any(payloadJson => TryReadSessionId(payloadJson) == sessionId);
    }

    private static Guid? TryReadSessionId(string payloadJson)
    {
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            return document.RootElement.TryGetProperty("sessionId", out var sessionIdElement) &&
                sessionIdElement.ValueKind == JsonValueKind.String &&
                Guid.TryParse(sessionIdElement.GetString(), out var sessionId)
                    ? sessionId
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<int> LoadLatestLeaseSequenceAsync(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.SessionLeases
            .Where(lease => lease.SessionId == sessionId)
            .Select(lease => (int?)lease.Sequence)
            .MaxAsync(cancellationToken) ?? 0;
    }

    private async Task<SessionLeaseDto> IssueNextLeaseAsync(
        SessionEntity session,
        DateTimeOffset now,
        string reason,
        CancellationToken cancellationToken)
    {
        var previousSequence = await LoadLatestLeaseSequenceAsync(session.SessionId, cancellationToken);
        var lease = leaseSigner.Sign(
            session.SessionId,
            session.OrganizationId,
            session.BranchId,
            session.SeatId,
            session.DeviceId,
            session.State,
            previousSequence + 1,
            now,
            now.AddMinutes(leaseOptions.Value.LeaseMinutes));
        var leaseEntity = CreateLeaseEntity(lease);

        session.CurrentLeaseId = leaseEntity.SessionLeaseId;
        session.UpdatedAtUtc = now;
        dbContext.SessionLeases.Add(leaseEntity);
        dbContext.SessionEvents.Add(new SessionEventEntity
        {
            SessionEventId = Guid.NewGuid(),
            SessionId = session.SessionId,
            OrganizationId = session.OrganizationId,
            BranchId = session.BranchId,
            EventType = HeartbeatLeaseEventType,
            ActorStaffUserId = null,
            DeviceId = session.DeviceId,
            DetailsJson = JsonSerializer.Serialize(new
            {
                reason,
                lease.Sequence,
                lease.ExpiresAtUtc
            }, JsonOptions),
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

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
            PayloadJson = JsonSerializer.Serialize(lease, JsonOptions),
            RevokedAtUtc = null
        };
    }

    private static Dictionary<string, string> LeasePayload(
        Guid sessionId,
        SessionLeaseDto lease,
        string reason)
    {
        return new Dictionary<string, string>
        {
            ["sessionId"] = sessionId.ToString("D"),
            ["sessionLease"] = JsonSerializer.Serialize(lease, JsonOptions),
            ["reason"] = reason
        };
    }
}
