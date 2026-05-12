using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceCommandStoreTests
{
    [Fact]
    public async Task ApplyResultAsync_UpdatesPersistedCommandStatus()
    {
        var store = new InMemoryDeviceCommandStore();
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var command = new DeviceCommandDto(
            CommandId: Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94"),
            Type: "lock",
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            Payload: new Dictionary<string, string>
            {
                ["reason"] = "operator-request"
            });
        var result = new DeviceCommandResultDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: deviceId,
            CommandId: command.CommandId,
            Status: "Accepted",
            Message: "Command accepted by Agent skeleton.",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:05Z"));

        await store.AddPendingAsync(deviceId, command, CancellationToken.None);
        await store.ApplyResultAsync(result, CancellationToken.None);

        var status = await store.GetAsync(deviceId, command.CommandId, CancellationToken.None);

        Assert.NotNull(status);
        Assert.Equal("Accepted", status.Status);
        Assert.Equal("Command accepted by Agent skeleton.", status.Message);
        Assert.Equal(DateTimeOffset.Parse("2026-05-12T00:00:05Z"), status.UpdatedAtUtc);
    }
}
