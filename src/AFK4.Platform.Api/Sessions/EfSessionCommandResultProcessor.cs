using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

public sealed class EfSessionCommandResultProcessor(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : ISessionCommandResultProcessor
{
    private static readonly string[] AcceptedTerminalStatuses = ["Accepted", "Completed"];

    public async Task ProcessAsync(DeviceCommandResultDto result, CancellationToken cancellationToken)
    {
        if (!AcceptedTerminalStatuses.Contains(result.Status, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        var command = await dbContext.DeviceCommands
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.DeviceId == result.DeviceId &&
                    candidate.CommandId == result.CommandId,
                cancellationToken);

        if (command is null ||
            !string.Equals(command.Type, DeviceCommandTypeNames.Lock, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var sessionId = TryReadSessionId(command.PayloadJson);
        if (sessionId is null)
        {
            return;
        }

        var session = await dbContext.Sessions.SingleOrDefaultAsync(
            candidate =>
                candidate.SessionId == sessionId.Value &&
                candidate.OrganizationId == result.OrganizationId &&
                candidate.BranchId == result.BranchId &&
                candidate.DeviceId == result.DeviceId,
            cancellationToken);

        if (session is null || session.State != SessionStateNames.Ending)
        {
            return;
        }

        var endedAtUtc = result.ObservedAtUtc == default
            ? timeProvider.GetUtcNow()
            : result.ObservedAtUtc;
        session.State = SessionStateNames.Ended;
        session.EndedAtUtc = endedAtUtc;
        session.CurrentLeaseId = null;
        session.UpdatedAtUtc = endedAtUtc;
        dbContext.SessionEvents.Add(new SessionEventEntity
        {
            SessionEventId = Guid.NewGuid(),
            SessionId = session.SessionId,
            OrganizationId = session.OrganizationId,
            BranchId = session.BranchId,
            EventType = "session-ended",
            ActorStaffUserId = null,
            DeviceId = result.DeviceId,
            CreatedAtUtc = endedAtUtc,
            DetailsJson = JsonSerializer.Serialize(new
            {
                reason = "device-lock-result",
                result.CommandId,
                result.Status,
                result.Message,
                result.ObservedAtUtc
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });

        await dbContext.SaveChangesAsync(cancellationToken);
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
}
