using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// ПК, запертый намеренно, — пауза, «Блокировать» из меню ПК, автозащита по времени или лимиту —
/// сердцебиение и сверка не открывают. Раньше планировщик видел «сессия идёт, а локальной нет» и
/// отпирал ПК через несколько секунд: пауза и блокировка оператора жили до следующего сердцебиения,
/// а игрок играл, пока время на сервере стояло.
///
/// Правда здесь одна и живёт в журнале команд: последняя команда lock/unlock ПК решает. Живой lock
/// (в пути, принят или выполнен) держится, пока не придёт unlock — от Панели, продления или
/// возобновления. Любой unlock снимает держание, каким бы ни вышел его статус: платящего игрока
/// лучше отпереть лишний раз, чем оставить запертым из-за чужого сбоя.
/// </summary>
public static class SessionLockHold
{
    // Провалившийся lock ничего не запер. Последние команды смотрим окном, а не всю историю ПК:
    // упавших locks подряд больше окна не бывает, а журнал растёт вечно.
    private const int RecentCommandsWindow = 10;

    private static readonly string[] LiveLockStatuses =
    [
        DeviceCommandStatusNames.Pending,
        DeviceCommandStatusNames.Accepted,
        DeviceCommandStatusNames.Completed
    ];

    public static async Task<bool> IsHeldAsync(
        PlatformDbContext dbContext,
        Guid deviceId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var recent = await dbContext.DeviceCommands
            .AsNoTracking()
            .Where(command =>
                command.DeviceId == deviceId &&
                (command.Type == DeviceCommandTypeNames.Lock || command.Type == DeviceCommandTypeNames.Unlock))
            .OrderByDescending(command => command.CreatedAtUtc)
            .Take(RecentCommandsWindow)
            .Select(command => new { command.Type, command.Status, command.PayloadJson })
            .ToListAsync(cancellationToken);

        foreach (var command in recent)
        {
            if (command.Type == DeviceCommandTypeNames.Unlock)
            {
                return false;
            }

            if (!LiveLockStatuses.Contains(command.Status))
            {
                continue;
            }

            // У команды оператора sessionId нет — ПК один, сессия на нём одна. Если он есть, то lock
            // чужой сессии (её завершили или перенесли) эту не держит.
            return TryReadSessionId(command.PayloadJson) is not { } lockedSessionId || lockedSessionId == sessionId;
        }

        return false;
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
