namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Чем закончилась команда на устройстве — машинным именем, а не фразой.
///
/// Журнал команд читает администратор клуба на своём языке. Агент до этого присылал только
/// человеческую строку и присылал её по-английски («Workstation locked (nothing)»), и она
/// доезжала до экрана как есть. Имя исхода переводится на стороне клиента — тот же порядок, что
/// у кодов ошибок API: сервер отдаёт код, клиент решает, какими словами о нём сказать.
///
/// Строку-сообщение это не отменяет: она остаётся деталью для инженера.
/// </summary>
public static class DeviceCommandOutcomeNames
{
    /// <summary>Команда принята, отдельного исхода у неё нет.</summary>
    public const string Accepted = "accepted";

    /// <summary>Аренда принята, место открыто гостю.</summary>
    public const string LeaseAccepted = "lease-accepted";

    /// <summary>Аренда продлена: сессия продолжается.</summary>
    public const string LeaseRefreshed = "lease-refreshed";

    /// <summary>Машина заперта.</summary>
    public const string WorkstationLocked = "workstation-locked";

    /// <summary>
    /// Агент не смог применить машинные политики: на самой машине ничего не изменилось. Раньше
    /// такой исход сообщался как обычный успех — оператор видел «заблокировано» там, где
    /// диспетчер задач остался доступен.
    /// </summary>
    public const string MachinePoliciesUnavailable = "machine-policies-unavailable";

    /// <summary>Предупреждение показано на экране игрока.</summary>
    public const string WarningShown = "warning-shown";

    /// <summary>Причина предупреждения агенту неизвестна — игрок ничего не увидел.</summary>
    public const string WarningReasonUnknown = "warning-reason-unknown";

    /// <summary>Такой тип команды этот агент не исполняет.</summary>
    public const string CommandNotImplemented = "command-not-implemented";

    /// <summary>
    /// Исполнение сорвалось: диск, реестр, права. Агент обязан ответить и в этом случае — молчание
    /// оператор читает как «команда где-то в пути», и ждать он будет бесконечно.
    /// </summary>
    public const string CommandExecutionFailed = "command-execution-failed";

    /// <summary>В команде не было аренды сессии.</summary>
    public const string LeaseMissing = "lease-missing";

    /// <summary>Аренду в команде не удалось прочитать.</summary>
    public const string LeaseUnreadable = "lease-unreadable";

    /// <summary>Аренда не прошла проверку подписи или срока.</summary>
    public const string LeaseInvalid = "lease-invalid";

    /// <summary>Windows перезагрузит ПК через десять секунд: ответ ушёл раньше.</summary>
    public const string RebootScheduled = "reboot-scheduled";

    /// <summary>Windows выключит ПК через десять секунд.</summary>
    public const string ShutdownScheduled = "shutdown-scheduled";

    /// <summary>На ПК идёт сессия: чужую игру агент не выключает и в обслуживание не уводит.</summary>
    public const string SessionInProgress = "session-in-progress";

    /// <summary>Сосед отправил волшебный пакет. Проснулся ли ПК, скажет его сердцебиение.</summary>
    public const string WakePacketSent = "wake-packet-sent";

    /// <summary>MAC или широковещательный адрес не годятся — или сосед уже в другой подсети.</summary>
    public const string WakeTargetInvalid = "wake-target-invalid";

    public const string MaintenanceStarted = "maintenance-started";

    public const string MaintenanceEnded = "maintenance-ended";

    /// <summary>Выход игрока или сообщение переданы на экран ПК.</summary>
    public const string DeliveredToShell = "delivered-to-shell";

    /// <summary>Экран игрока не запущен или не отвечает — передать некому.</summary>
    public const string ShellNotConnected = "shell-not-connected";

    /// <summary>Профиля защиты у ПК пока нет — обновлять нечего.</summary>
    public const string NothingToRefresh = "nothing-to-refresh";

    /// <summary>Профиль защиты перечитан и применён; что вышло по пунктам — в отчёте ПК.</summary>
    public const string ProtectionApplied = "protection-applied";

    /// <summary>Профиль не удалось получить с сервера — ПК остаётся на прежнем.</summary>
    public const string ProtectionUnavailable = "protection-unavailable";
}
