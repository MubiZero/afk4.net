namespace AFK4.Shared.Contracts.Shell;

public sealed record PlayerShellStateDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid DeviceId,
    string State,
    Guid? SessionId,
    DateTimeOffset? LeaseExpiresAtUtc,
    int? RemainingSeconds,
    bool IsOnline,
    bool IsGraceMode,
    int WarningThresholdSeconds,
    string Message,
    IReadOnlyList<LauncherAppDto> LauncherApps,
    string Locale = "ru",
    string WarningKind = PlayerShellWarningKinds.None,
    ShellBrandingDto? Branding = null,
    // Код с этого монитора: человек набирает его в приложении и садится именно за эту машину.
    // Пусто, когда за ПК уже играют или связи с сервером нет — показать старый код значит
    // позвать человека к машине, которую сервер ему не отдаст.
    string? SeatingCode = null,
    // Когда код сменится: оболочка показывает, сколько ему осталось, а просроченный не рисует.
    DateTimeOffset? SeatingCodeExpiresAtUtc = null,
    // Время платформы в момент, когда агент собрал это состояние. Срок аренды — тоже время
    // платформы, а часы ПК могут от неё отставать: поправку хост считает по этому полю.
    DateTimeOffset? ObservedAtUtc = null,
    // Когда агент в последний раз достучался до платформы. Пусто — ни разу с запуска службы.
    DateTimeOffset? LastContactUtc = null,
    // Адрес платформы из настроек агента: хосту больше не нужно угадывать, куда ходить.
    string? ApiBaseUrl = null,
    // Место этого ПК — «ПК 07»: первое, что читается на экране, и видно от стойки.
    string? SeatLabel = null,
    // Зона места — «Общий зал».
    string? ZoneName = null,
    // Чья сессия идёт: none, guest или player. Вошедшему не владельцу экран говорит «эта сессия
    // не ваша» и ничего не открывает.
    string? SessionOwnerKind = null,
    // Счёт владельца сессии — только у player.
    Guid? SessionOwnerPlayerAccountId = null,
    // Права организации по тарифу: без player_shop нет вкладки «Бар», без loyalty — кэшбека.
    IReadOnlyList<string>? Features = null,
    // Обслуживание: с какого момента и кто его включил — для полосы «Включено из Панели AFK4.net
    // в 14:05 · Шерзод». Пусто вне обслуживания; имя пусто, если его включила поддержка без имени.
    DateTimeOffset? MaintenanceSinceUtc = null,
    string? MaintenanceByName = null);
