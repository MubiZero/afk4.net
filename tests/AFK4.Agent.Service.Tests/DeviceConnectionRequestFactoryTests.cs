using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Agent.Service.Tests;

public sealed class DeviceConnectionRequestFactoryTests
{
    [Fact]
    public void Create_BuildsConnectionRequestFromAgentOptions()
    {
        var options = new AgentOptions
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
            DeviceCredentialSecret = "device-secret"
        };

        var request = DeviceConnectionRequestFactory.Create(
            options,
            DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        Assert.Equal(options.OrganizationId, request.OrganizationId);
        Assert.Equal(options.BranchId, request.BranchId);
        Assert.Equal(options.DeviceId, request.DeviceId);
        Assert.Equal("PC-001", request.MachineName);
        Assert.Equal("0.1.0", request.AgentVersion);
        Assert.Equal("0.1.0", request.ShellVersion);
        Assert.Equal("device-secret", request.CredentialSecret);
        Assert.Equal(DateTimeOffset.Parse("2026-05-12T00:00:00Z"), request.ConnectedAtUtc);
    }

    [Fact]
    public void Create_IncludesCurrentSessionLeaseSnapshot()
    {
        var options = new AgentOptions
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
            DeviceCredentialSecret = "device-secret"
        };
        var lease = CreateLease();
        var store = new InMemorySessionLeaseStore();
        store.Save(lease);

        var request = DeviceConnectionRequestFactory.Create(
            options,
            DateTimeOffset.Parse("2026-05-13T10:02:00Z"),
            store);

        Assert.Equal(lease.SessionId, request.ActiveSessionId);
        Assert.Equal(lease.ExpiresAtUtc, request.ActiveSessionLeaseExpiresAtUtc);
        Assert.Equal(lease.Sequence, request.ActiveSessionLeaseSequence);
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
            Sequence: 2,
            IssuedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T10:15:00Z"),
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
    }
}
