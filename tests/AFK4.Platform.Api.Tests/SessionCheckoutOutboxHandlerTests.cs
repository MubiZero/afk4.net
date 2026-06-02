using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Outbox;
using AFK4.Platform.Api.Sessions;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Tests;

public sealed class SessionCheckoutOutboxHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task HandleAsync_NotifiesDeviceWithCommandFromPayload()
    {
        var dispatch = new RecordingDispatch();
        var handler = new SessionCheckoutOutboxHandler(dispatch);
        var deviceId = Guid.NewGuid();
        var command = new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: DeviceCommandTypeNames.Lock,
            CreatedAtUtc: DateTimeOffset.UnixEpoch,
            Payload: new Dictionary<string, string> { ["reason"] = "session-checkout" });
        var row = NewRow(JsonSerializer.Serialize(new SessionCheckoutNotifyPayload(deviceId, command), JsonOptions));

        var result = await handler.HandleAsync(row, CancellationToken.None);

        Assert.True(result.Success);
        var (notifiedDeviceId, notifiedCommand) = Assert.Single(dispatch.Notified);
        Assert.Equal(deviceId, notifiedDeviceId);
        Assert.Equal(command.CommandId, notifiedCommand.CommandId);
        Assert.Equal(DeviceCommandTypeNames.Lock, notifiedCommand.Type);
    }

    [Fact]
    public async Task HandleAsync_UnreadablePayload_FailsPermanently()
    {
        var dispatch = new RecordingDispatch();
        var handler = new SessionCheckoutOutboxHandler(dispatch);
        var row = NewRow("not json");

        var result = await handler.HandleAsync(row, CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.Empty(dispatch.Notified);
    }

    private static OutboxMessageEntity NewRow(string payloadJson) =>
        new()
        {
            OutboxMessageId = Guid.NewGuid(),
            Type = OutboxMessageTypes.SessionCheckout,
            PayloadJson = payloadJson,
            IdempotencyKey = Guid.NewGuid().ToString("N")
        };

    private sealed class RecordingDispatch : IDeviceCommandDispatchService
    {
        public List<(Guid DeviceId, DeviceCommandDto Command)> Notified { get; } = [];

        public Task<DeviceCommandDto> EnqueueAsync(Guid deviceId, CreateDeviceCommandRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new DeviceCommandDto(Guid.NewGuid(), request.Type, DateTimeOffset.UnixEpoch, request.Payload));

        public Task NotifyAsync(Guid deviceId, DeviceCommandDto command, CancellationToken cancellationToken)
        {
            Notified.Add((deviceId, command));
            return Task.CompletedTask;
        }

        public Task<DeviceCommandDto> DispatchAsync(Guid deviceId, CreateDeviceCommandRequest request, CancellationToken cancellationToken) =>
            EnqueueAsync(deviceId, request, cancellationToken);
    }
}
