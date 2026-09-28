using System.Collections.Concurrent;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public sealed class InMemoryDeviceCommandStore : IDeviceCommandStore
{
    private readonly ConcurrentDictionary<(Guid DeviceId, Guid CommandId), DeviceCommandStatusDto> statuses = new();
    private readonly ConcurrentDictionary<string, DeviceCommandDto> byIdempotencyKey = new(StringComparer.Ordinal);

    public Task<DeviceCommandDto> AddPendingAsync(
        Guid deviceId,
        DeviceCommandDto command,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (idempotencyKey is not null)
        {
            var winner = byIdempotencyKey.GetOrAdd(idempotencyKey, command);
            if (winner.CommandId != command.CommandId)
            {
                return Task.FromResult(winner);
            }
        }

        var status = new DeviceCommandStatusDto(
            DeviceId: deviceId,
            CommandId: command.CommandId,
            Type: command.Type,
            Status: "Pending",
            Message: null,
            CreatedAtUtc: command.CreatedAtUtc,
            UpdatedAtUtc: command.CreatedAtUtc);

        statuses[(deviceId, command.CommandId)] = status;

        return Task.FromResult(command);
    }

    public Task<DeviceCommandDto?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken) =>
        Task.FromResult(byIdempotencyKey.TryGetValue(idempotencyKey, out var command) ? command : null);

    public Task ApplyResultAsync(DeviceCommandResultDto result, CancellationToken cancellationToken)
    {
        var key = (result.DeviceId, result.CommandId);
        statuses.AddOrUpdate(
            key,
            _ => new DeviceCommandStatusDto(
                DeviceId: result.DeviceId,
                CommandId: result.CommandId,
                Type: "unknown",
                Status: result.Status,
                Message: result.Message,
                CreatedAtUtc: result.ObservedAtUtc,
                UpdatedAtUtc: result.ObservedAtUtc,
                Outcome: result.Outcome),
            (_, existing) => existing with
            {
                Status = result.Status,
                Message = result.Message,
                Outcome = result.Outcome,
                UpdatedAtUtc = result.ObservedAtUtc
            });

        return Task.CompletedTask;
    }

    public Task<DeviceCommandStatusDto?> GetAsync(Guid deviceId, Guid commandId, CancellationToken cancellationToken)
    {
        statuses.TryGetValue((deviceId, commandId), out var status);
        return Task.FromResult(status);
    }
}
