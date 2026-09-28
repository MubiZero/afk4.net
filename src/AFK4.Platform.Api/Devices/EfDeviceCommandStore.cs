using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

public sealed class EfDeviceCommandStore(
    PlatformDbContext dbContext,
    AFK4.Platform.Api.Sessions.EfSessionCommandResultProcessor? sessionResults = null) : IDeviceCommandStore
{
    public async Task<DeviceCommandDto> AddPendingAsync(
        Guid deviceId,
        DeviceCommandDto command,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        // У консоли нет агента: отпереть, запереть, продлить аренду на ней нечему. Команда
        // записывается сразу выполненной — иначе она висела бы в очереди вечно, а экран, ждущий
        // ответа устройства, так его и не дождался бы.
        var device = await dbContext.Devices.AsNoTracking()
            .Where(candidate => candidate.DeviceId == deviceId)
            .Select(candidate => new { candidate.Role, candidate.OrganizationId, candidate.BranchId })
            .FirstOrDefaultAsync(cancellationToken);
        var noAgent = device is not null && !DeviceRoleNames.HasAgent(device.Role);
        var entity = new DeviceCommandEntity
        {
            DeviceId = deviceId,
            CommandId = command.CommandId,
            Type = command.Type,
            PayloadJson = JsonSerializer.Serialize(command.Payload),
            Status = noAgent ? DeviceCommandStatusNames.Completed : DeviceCommandStatusNames.Pending,
            Message = noAgent ? NoAgentMessage : null,
            IdempotencyKey = idempotencyKey,
            CreatedAtUtc = command.CreatedAtUtc,
            UpdatedAtUtc = command.CreatedAtUtc
        };
        dbContext.DeviceCommands.Add(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (idempotencyKey is not null
                && RelationalFailureClassifier.IsUniqueViolation(exception, IdempotencyKeyIndex))
        {
            // Два одинаковых запроса проскочили проверку разом: второй отдаёт команду первого.
            dbContext.Entry(entity).State = EntityState.Detached;
            return await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken)
                ?? throw new InvalidOperationException("The device command that took the idempotency key is gone.");
        }

        // Ответ за консоль — тот же, что прислал бы агент: «заперто» завершает сессию, которая ждала
        // этого в состоянии «завершается». Без него сессия на консоли не закрылась бы никогда.
        if (noAgent && sessionResults is not null)
        {
            await sessionResults.ProcessAsync(new DeviceCommandResultDto(
                device!.OrganizationId, device.BranchId, deviceId, command.CommandId,
                DeviceCommandStatusNames.Completed, NoAgentMessage, command.CreatedAtUtc), cancellationToken);
        }

        return command;
    }

    public const string NoAgentMessage = "no_agent";

    private const string IdempotencyKeyIndex = "IX_device_commands_IdempotencyKey";

    public async Task<DeviceCommandDto?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        var stored = await dbContext.DeviceCommands.AsNoTracking()
            .Where(candidate => candidate.IdempotencyKey == idempotencyKey)
            .Select(candidate => new { candidate.CommandId, candidate.Type, candidate.CreatedAtUtc, candidate.PayloadJson })
            .FirstOrDefaultAsync(cancellationToken);

        return stored is null
            ? null
            : new DeviceCommandDto(
                CommandId: stored.CommandId,
                Type: stored.Type,
                CreatedAtUtc: stored.CreatedAtUtc,
                Payload: JsonSerializer.Deserialize<Dictionary<string, string>>(stored.PayloadJson) ?? []);
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
                Outcome = result.Outcome,
                CreatedAtUtc = result.ObservedAtUtc,
                UpdatedAtUtc = result.ObservedAtUtc
            });
        }
        else
        {
            command.Status = result.Status;
            command.Message = result.Message;
            command.Outcome = result.Outcome;
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
            UpdatedAtUtc: command.UpdatedAtUtc,
            Outcome: command.Outcome);
    }
}
