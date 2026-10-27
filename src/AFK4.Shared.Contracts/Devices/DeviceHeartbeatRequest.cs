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
    string? NetworkBroadcastAddress = null,
    // Есть ли на ПК учётка игрока (киоск). false — киоск снят или не ставился: сервер уводит ПК из
    // зала. null — агент этого не сообщает (прежняя версия): признак не трогаем.
    bool? KioskInstalled = null,
    // IPv4 этого адаптера в сети клуба («192.168.1.23»): по нему Панель без интернета передаёт ПК
    // офлайн-аренду (docs/roadmap/offline-front-desk.md). Пусто — агент прежней версии.
    string? NetworkAddress = null,
    // Аренда, которую держит ПК, подписана ключом ПК администратора, а не платформой: её выдала
    // Панель без интернета, и сервер этой сессии ещё не знает. Такой ПК сервер не запирает, пока
    // аренда не кончилась, — иначе гость, севший без связи, вылетел бы, едва связь вернулась.
    bool? ActiveSessionLeaseOffline = null);
