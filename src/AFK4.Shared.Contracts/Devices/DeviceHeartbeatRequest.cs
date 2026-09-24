namespace AFK4.Shared.Contracts.Devices;

public sealed record DeviceHeartbeatRequest(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string MachineName,
    string AgentVersion,
    string ShellVersion,
    DateTimeOffset ObservedAtUtc,
    bool IsLocked,
    Guid? ActiveSessionId,
    DateTimeOffset? ActiveSessionLeaseExpiresAtUtc,
    int? ActiveSessionLeaseSequence,
    // MAC проводного адаптера со шлюзом («AA-BB-CC-DD-EE-FF»): по нему этот ПК будит сосед, когда
    // он выключен. Пусто — агент ещё не умеет его сообщать.
    string? NetworkMacAddress = null,
    // Подсеть этого адаптера («192.168.1.0/24»): будить можно только из той же подсети.
    string? NetworkSubnet = null,
    // Широковещательный адрес подсети — куда сосед шлёт волшебный пакет.
    string? NetworkBroadcastAddress = null);
