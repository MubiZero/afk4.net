using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

public sealed class EfDeviceCommandStore(PlatformDbContext dbContext) : IDeviceCommandStore
{
    public async Task AddPendingAsync(Guid deviceId, DeviceCommandDto command, CancellationToken cancellationToken)
    {
        dbContext.DeviceCommands.Add(new DeviceCommandEntity
        {
            DeviceId = deviceId,
            CommandId = command.CommandId,
            Type = command.Type,
            PayloadJson = JsonSerializer.Serialize(command.Payload),
            Status = "Pending",
            Message = null,
            CreatedAtUtc = command.CreatedAtUtc,
            UpdatedAtUtc = command.CreatedAtUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyResultAsync(DeviceCommandResultDto result, CancellationToken cancellationToken)
    {
        var command = await dbContext.DeviceCommands.SingleOrDefaultAsync(
            candidate => candidate.DeviceId == result.DeviceId && candidate.CommandId == result.CommandId,
            cancellationToken);

        if (command is null)
        {
            dbContext.DeviceCommands.Add(new DeviceCommandEntity
            {
                DeviceId = result.DeviceId,
                CommandId = result.CommandId,
                Type = "unknown",
                PayloadJson = "{}",
                Status = result.Status,
                Message = result.Message,
                CreatedAtUtc = result.ObservedAtUtc,
                UpdatedAtUtc = result.ObservedAtUtc
            });
        }
        else
        {
            command.Status = result.Status;
            command.Message = result.Message;
            command.UpdatedAtUtc = result.ObservedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<DeviceCommandStatusDto?> GetAsync(Guid deviceId, Guid commandId, CancellationToken cancellationToken)
    {
        var command = await dbContext.DeviceCommands
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.DeviceId == deviceId && candidate.CommandId == commandId,
                cancellationToken);

        if (command is null)
        {
            return null;
        }

        return new DeviceCommandStatusDto(
            DeviceId: command.DeviceId,
            CommandId: command.CommandId,
            Type: command.Type,
            Status: command.Status,
            Message: command.Message,
            CreatedAtUtc: command.CreatedAtUtc,
            UpdatedAtUtc: command.UpdatedAtUtc);
    }
}
