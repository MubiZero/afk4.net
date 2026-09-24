namespace AFK4.Shared.Contracts.Devices;

/// <summary>Почему сервер не принял команду администратора.</summary>
public static class DeviceCommandErrorCodeNames
{
    /// <summary>Такой команды нет — раньше сервер принимал любую строку, и агент отвечал «не умею».</summary>
    public const string UnknownType = "unknown_command_type";

    /// <summary>На ПК идёт сессия: перезагружать, выключать и уводить в обслуживание нельзя.</summary>
    public const string ActiveSession = "device_has_active_session";

    public const string InvalidPayload = "invalid_command_payload";

    /// <summary>Разбудить нельзя: ПК ещё ни разу не сообщил свой сетевой адрес.</summary>
    public const string WakeTargetUnknown = "wake_target_unknown";

    /// <summary>Разбудить некому: в подсети этого ПК нет ни одного включённого соседа.</summary>
    public const string NoWakeHelper = "no_wake_helper";
}
