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
    bool? ActiveSessionLeaseOffline = null,
    // Достучался ли ПК до кеша игр клуба при последней проверке. null — кеш не задан или агент прежней
    // версии: Панель такой ПК не считает.
    bool? GameCacheReachable = null,
    // Встроенный кеш игр на этом ПК поднят и сколько в нём лежит. null — ПК не кеш или агент прежней версии.
    bool? GameCacheServing = null,
    long? GameCacheStoredBytes = null,
    // ПК был обесточен: до этого момента (часы платформы) агент последний раз был жив, а потом машина
    // включилась заново без нормального выключения. Когда включилась — сервер считает сам: его «сейчас»
    // минус UptimeSeconds. Шлётся, пока сердцебиение не дойдёт. null — обесточивания не было.
    DateTimeOffset? PowerLostFromUtc = null,
    long? UptimeSeconds = null,
    // ПК работает от батареи ИБП (Windows видит ИБП как батарею): в клубе пропал свет. null — агент
    // прежней версии или ИБП Windows не видит.
    bool? OnBatteryPower = null);
