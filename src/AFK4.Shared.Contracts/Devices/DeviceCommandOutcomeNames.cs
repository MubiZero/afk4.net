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
}
