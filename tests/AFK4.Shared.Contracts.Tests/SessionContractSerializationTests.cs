using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Tests;

public sealed class SessionContractSerializationTests
{
    [Fact]
    public void SessionCommandResponse_RoundTripsSessionLeaseAndDeviceCommands()
    {
        var lease = CreateLease();
        var session = new SessionDto(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            TariffRuleVersionId: "manual-v1",
            StartedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            EndsAtUtc: DateTimeOffset.Parse("2026-05-13T11:00:00Z"),
            EndedAtUtc: null,
            RemainingSeconds: 3600,
            CurrentLease: lease);
        var command = new DeviceCommandDto(
            CommandId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            Type: "unlock",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            Payload: new Dictionary<string, string>
            {
                ["sessionId"] = session.SessionId.ToString("D"),
                ["sessionLease"] = JsonSerializer.Serialize(lease)
            });
        var response = new SessionCommandResponse(
            IdempotencyKey: "start-seat-1-20260513-1000",
            Session: session,
            DeviceCommands: [command]);

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<SessionCommandResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(SessionStateNames.Active, copy.Session.State);
        Assert.Equal("manual-v1", copy.Session.TariffRuleVersionId);
        Assert.Equal(lease.Signature, copy.Session.CurrentLease?.Signature);
        Assert.Single(copy.DeviceCommands);
        Assert.Equal("unlock", copy.DeviceCommands[0].Type);
    }

    [Fact]
    public void DeviceHeartbeatRequest_CanCarryActiveLeaseSnapshot()
    {
        var heartbeat = new DeviceHeartbeatRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-13T10:02:00Z"),
            IsLocked: false,
            ActiveSessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            ActiveSessionLeaseExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T10:15:00Z"),
            ActiveSessionLeaseSequence: 1);

        var json = JsonSerializer.Serialize(heartbeat);
        var copy = JsonSerializer.Deserialize<DeviceHeartbeatRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(heartbeat.ActiveSessionId, copy.ActiveSessionId);
        Assert.Equal(1, copy.ActiveSessionLeaseSequence);
    }

    private static SessionLeaseDto CreateLease()
    {
        return new SessionLeaseDto(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T10:15:00Z"),
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
    }
}
