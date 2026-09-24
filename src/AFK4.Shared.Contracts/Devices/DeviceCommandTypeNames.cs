namespace AFK4.Shared.Contracts.Devices;

public static class DeviceCommandTypeNames
{
    public const string Lock = "lock";

    public const string Unlock = "unlock";

    public const string RefreshSessionLease = "refresh-session-lease";

    // A non-blocking warning overlay pushed to the shell (e.g. fixed time almost
    // up, or an open tab approaching its credit limit).
    public const string Warn = "warn";

    /// <summary>Перезагрузить ПК. Только без живой сессии; отдаётся агенту один раз.</summary>
    public const string Reboot = "reboot";

    /// <summary>Выключить ПК. Только без живой сессии; отдаётся агенту один раз.</summary>
    public const string Shutdown = "shutdown";

    /// <summary>
    /// «Разбудить этот ПК» — так просит администратор, называя спящую машину. Выключенный ПК
    /// команду не получит, поэтому сервер передаёт её соседу по подсети как <see cref="WakeNeighbor"/>.
    /// </summary>
    public const string Wake = "wake";

    /// <summary>
    /// Агенту: отправь волшебный пакет (6×FF + 16×MAC, UDP 9) в свою подсеть. В теле — mac,
    /// broadcast и targetDeviceId. Администратор эту команду не шлёт — её собирает сервер из
    /// <see cref="Wake"/>.
    /// </summary>
    public const string WakeNeighbor = "wake-neighbor";

    /// <summary>Хост выходит из аккаунта игрока; сессия, если идёт, продолжается.</summary>
    public const string SignOut = "sign-out";

    /// <summary>Сообщение игроку: окно поверх игры или полоса на экране блокировки. В теле — text.</summary>
    public const string Message = "message";

    /// <summary>Режим обслуживания: игрокам вход закрыт. Право organization.devices.maintenance.</summary>
    public const string MaintenanceOn = "maintenance-on";

    /// <summary>Вернуть ПК в зал из обслуживания.</summary>
    public const string MaintenanceOff = "maintenance-off";

    /// <summary>Перечитать профиль защиты.</summary>
    public const string PolicyRefresh = "policy-refresh";
}
