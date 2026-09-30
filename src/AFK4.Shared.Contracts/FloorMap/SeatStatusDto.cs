namespace AFK4.Shared.Contracts.FloorMap;

public sealed record SeatStatusDto(
    Guid SeatId,
    string SeatName,
    Guid ZoneId,
    string ZoneName,
    int SortOrder,
    // Одно из SeatStateNames.
    string State,
    Guid? DeviceId,
    string? DeviceName,
    bool? IsDeviceOnline,
    bool? IsDeviceLocked,
    DateTimeOffset? LastHeartbeatAtUtc,
    string? AgentVersion,
    string? ShellVersion,
    Guid? ActiveSessionId,
    int? RemainingSeconds,
    // Live accrued time cost for an open-tab session (count-up). Null for fixed
    // sessions (which expose RemainingSeconds instead) and unbilled guests.
    long? AccruedCostMinorUnits = null,
    string? CurrencyCode = null,
    // Optimistic-concurrency version of the active session; the operator echoes it back as
    // ExpectedVersion on a seat mutation so a stale view loses the race with a 409.
    int? SessionVersion = null,
    // Who is on the seat right now: the active session's player display name. Null for a
    // guest session with no account, or a free seat.
    string? PlayerDisplayName = null,
    // The tariff the active session bills against. Null for guest/package sessions that
    // carry no named tariff, or a free seat.
    string? TariffName = null,
    // When the active session started (UTC) — lets the operator show real elapsed time.
    DateTimeOffset? SessionStartedAtUtc = null,
    // Когда с этого места позвали оператора. Null — не зовут. Время, а не флаг: стойке важно,
    // кто ждёт дольше.
    DateTimeOffset? AssistanceRequestedAtUtc = null,
    // С какого момента ПК на обслуживании по решению клуба. Null — ПК в зале. Отдельно от State:
    // «обслуживание» на карте бывает и у неподтверждённого ПК, а вернуть в зал можно только того,
    // кого туда увели.
    DateTimeOffset? MaintenanceSinceUtc = null,
    // Место с консолью без агента: сессию ведёт администратор, команд ПК у места нет.
    bool IsConsole = false,
    // ПК сверх предела бесплатного тарифа: новые сессии на нём не запускаются, идущая доживает.
    bool IsOutsidePlan = false,
    // Какая команда администратора (DeviceCommandTypeNames) упала на ПК последней и с тех пор не
    // сменилась успешной. Null — сбоя нет. По ней карта показывает «Сбой команды» и кнопку
    // «Повторить …»: без неё было видно, что что-то не прошло, но не что именно повторять.
    string? LastFailedCommandType = null,
    // Как оплачена идущая сессия (BillingModeNames); пусто — гость без расчёта или места занято нет.
    // Панели нужно знать, что у гостя, заплатившего наличными, «+15 мин» — это новая оплата у стойки.
    string? SessionBillingMode = null,
    // Киоск на ПК снят: это не игровое место, посадить и забронировать его нельзя, пока ПК не
    // вернут в зал мастером. Отдельно от State — так же, как IsOutsidePlan.
    bool IsKioskAbsent = false,
    // За местом сидит игрок со счётом клуба — даже если имя в его карточке пусто. Без этого признака
    // безымянный игрок на карте и в окне расчёта выглядел гостем, а это другие деньги и другие права.
    bool HasPlayerAccount = false);
