using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceHeartbeatServicePersistenceTests
{
    [Fact]
    public async Task RecordHeartbeatAsync_PersistsLatestDeviceState()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

        await using (var db = new PlatformDbContext(options))
        {
            db.Devices.Add(new DeviceEntity
            {
                DeviceId = deviceId,
                OrganizationId = organizationId,
                BranchId = branchId,
                MachineName = "PC-001",
                AgentVersion = "0.1.0",
                ShellVersion = "0.1.0",
                EnrolledAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
            });

            await db.SaveChangesAsync();
        }

        await using (var db = new PlatformDbContext(options))
        {
            var service = new DeviceHeartbeatService(new CapturingHubContext(), db);
            await service.RecordHeartbeatAsync(
                deviceId,
                new DeviceHeartbeatRequest(
                    OrganizationId: organizationId,
                    BranchId: branchId,
                    DeviceId: deviceId,
                    MachineName: "PC-001",
                    AgentVersion: "0.1.1",
                    ShellVersion: "0.1.2",
                    ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:02:00Z"),
                    IsLocked: true,
                    ActiveSessionId: null,
                    ActiveSessionLeaseExpiresAtUtc: null,
                    ActiveSessionLeaseSequence: null),
                CancellationToken.None);
        }

        await using (var db = new PlatformDbContext(options))
        {
            var device = await db.Devices.SingleAsync(candidate => candidate.DeviceId == deviceId);

            Assert.True(device.IsOnline);
            Assert.True(device.IsLocked);
            Assert.Equal("0.1.1", device.AgentVersion);
            Assert.Equal("0.1.2", device.ShellVersion);
            Assert.Equal(DateTimeOffset.Parse("2026-05-12T00:02:00Z"), device.LastHeartbeatAtUtc);
        }
    }

    private sealed class CapturingHubContext : IHubContext<DeviceHub>
    {
        public IHubClients Clients { get; } = new CapturingHubClients();

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class CapturingHubClients : IHubClients
    {
        private readonly IClientProxy proxy = new NoOpClientProxy();

        public IClientProxy All => proxy;

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Client(string connectionId) => throw new NotSupportedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IClientProxy Group(string groupName) => throw new NotSupportedException();

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IClientProxy User(string userId) => throw new NotSupportedException();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class NoOpClientProxy : IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
