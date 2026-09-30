using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Devices;

/// <summary>
/// Какие команды администратор может отдать ПК, чьим правом и при каком условии (спека оболочки,
/// §5.8). Раньше сервер принимал любую строку: опечатка в типе доезжала до агента и возвращалась
/// «не умею», а перезагрузка посреди чужой игры не упиралась ни во что.
/// </summary>
public static class DeviceCommandPolicy
{
    /// <summary>
    /// Сколько живёт неповторяемая команда, пока её не забрали. Перезагрузка, пришедшая через три
    /// дня после просьбы — когда ПК наконец включили, — хуже потерянной.
    /// </summary>
    public static readonly TimeSpan OneShotLifetime = TimeSpan.FromMinutes(10);

    private static readonly HashSet<string> StaffCommands = new(StringComparer.Ordinal)
    {
        DeviceCommandTypeNames.Lock,
        DeviceCommandTypeNames.Unlock,
        DeviceCommandTypeNames.RefreshSessionLease,
        DeviceCommandTypeNames.Warn,
        DeviceCommandTypeNames.Reboot,
        DeviceCommandTypeNames.Shutdown,
        DeviceCommandTypeNames.Wake,
        DeviceCommandTypeNames.SignOut,
        DeviceCommandTypeNames.Message,
        DeviceCommandTypeNames.MaintenanceOn,
        DeviceCommandTypeNames.MaintenanceOff,
        DeviceCommandTypeNames.PolicyRefresh
    };

    private static readonly HashSet<string> OneShotCommands = new(StringComparer.Ordinal)
    {
        DeviceCommandTypeNames.Reboot,
        DeviceCommandTypeNames.Shutdown,
        DeviceCommandTypeNames.WakeNeighbor
    };

    private static readonly HashSet<string> NeedsFreeDevice = new(StringComparer.Ordinal)
    {
        DeviceCommandTypeNames.Reboot,
        DeviceCommandTypeNames.Shutdown,
        DeviceCommandTypeNames.MaintenanceOn
    };

    // Команды, которые адресованы игроку или оболочке: без киоска ни того, ни другого нет. Питание,
    // пробуждение, профиль защиты и возврат из обслуживания остаются — ими управляют ПК как машиной.
    private static readonly HashSet<string> NeedsKiosk = new(StringComparer.Ordinal)
    {
        DeviceCommandTypeNames.Lock,
        DeviceCommandTypeNames.Unlock,
        DeviceCommandTypeNames.Warn,
        DeviceCommandTypeNames.SignOut,
        DeviceCommandTypeNames.Message,
        DeviceCommandTypeNames.MaintenanceOn
    };

    /// <summary>Сообщение игроку — не письмо: длиннее этого на экране поверх игры его не прочтут.</summary>
    public const int MaxMessageLength = 500;

    /// <summary>
    /// Команда, которую может прислать администратор. <c>wake-neighbor</c> сюда не входит: его
    /// собирает сервер из <c>wake</c>, выбрав соседа.
    /// </summary>
    public static bool IsStaffCommand(string? type) => type is not null && StaffCommands.Contains(type);

    /// <summary>
    /// Команда, которую агенту отдают ровно один раз и только по сердцебиению. Для <c>lock</c>
    /// повтор безвреден; повтор перезагрузки после перезапуска агента был бы петлёй.
    /// </summary>
    public static bool IsOneShot(string? type) => type is not null && OneShotCommands.Contains(type);

    /// <summary>Нельзя, пока на ПК идёт сессия: чужую игру не выключают и не уводят в обслуживание.</summary>
    public static bool RequiresFreeDevice(string type) => NeedsFreeDevice.Contains(type);

    /// <summary>Нельзя, пока на ПК нет киоска: команда адресована игроку или оболочке, а их нет.</summary>
    public static bool RequiresKiosk(string type) => NeedsKiosk.Contains(type);

    public static string RequiredPermission(string? type) =>
        type is DeviceCommandTypeNames.MaintenanceOn or DeviceCommandTypeNames.MaintenanceOff
            ? OrganizationPermissionNames.MaintainDevice
            : OrganizationPermissionNames.DispatchDeviceCommand;

    /// <summary>Тело команды проверяется до отправки; null — годится.</summary>
    public static string? ValidatePayload(string type, IReadOnlyDictionary<string, string> payload)
    {
        // Предупреждение с причиной, которой агент не знает, он отклоняет, и игрок ничего не видит.
        if (type == DeviceCommandTypeNames.Warn)
        {
            return payload.TryGetValue("reason", out var reason) && DeviceWarnReasonNames.IsKnown(reason)
                ? null
                : "Warn command requires a known reason (time-almost-up, credit-limit or low-balance).";
        }

        if (type != DeviceCommandTypeNames.Message)
        {
            return null;
        }

        return payload.TryGetValue("text", out var text)
            && !string.IsNullOrWhiteSpace(text)
            && text.Length <= MaxMessageLength
                ? null
                : $"Message command requires a text of 1–{MaxMessageLength} characters.";
    }
}
