using AFK4.Shared.Contracts.Install;

namespace AFK4.Platform.Api.Data;

public sealed class DeviceEntity
{
    public Guid DeviceId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string MachineName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string DevicePublicKey { get; set; } = string.Empty;

    public string Role { get; set; } = DeviceRoleNames.GamingPc;

    public string EnrollmentState { get; set; } = DeviceEnrollmentStateNames.Approved;

    public string AgentVersion { get; set; } = string.Empty;

    public string ShellVersion { get; set; } = string.Empty;

    public DateTimeOffset EnrolledAtUtc { get; set; }

    /// <summary>
    /// Владелец отметил этот ПК работающим на бесплатном тарифе, когда ПК больше предела (спека
    /// тарифов клуба, §5a). Без отметок работают подключённые раньше других.
    /// </summary>
    public bool KeptOnFreePlan { get; set; }

    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }

    public bool IsOnline { get; set; }

    public bool IsLocked { get; set; }

    /// <summary>
    /// Клуб попросил перевыпустить ключ этой машины. Агент увидит просьбу в ближайшем
    /// сердцебиении, перевыпустит ключ сам и запишет новый — без визита к ПК.
    /// null — просьбы нет.
    /// </summary>
    public DateTimeOffset? CredentialRotationRequestedAtUtc { get; set; }

    /// <summary>
    /// С какого момента ПК на обслуживании: сессию на нём не начать, код посадки он не показывает,
    /// карта пишет «обслуживание». null — ПК в зале. Правда живёт здесь, а не только на агенте:
    /// иначе стойка видела бы запертую машину свободной и начинала бы на ней сессию.
    /// </summary>
    public DateTimeOffset? MaintenanceSinceUtc { get; set; }

    /// <summary>
    /// Кто включил обслуживание — сотрудник и его имя на тот момент. Имя хранится снимком: полоса
    /// на ПК и журнал должны говорить, кто нажал кнопку, даже если сотрудника потом переименуют
    /// или уволят. Поддержка платформы сотрудником клуба не является — у неё только имя.
    /// </summary>
    public Guid? MaintenanceByStaffUserId { get; set; }

    public string? MaintenanceByName { get; set; }

    /// <summary>
    /// Последний отчёт ПК о защите (спека оболочки, §6.3): версия профиля и что вышло по каждому
    /// пункту, JSON-ом. Живёт на устройстве, а не отдельной таблицей: отчёт один и заменяется целиком.
    /// </summary>
    public string? ProtectionReportJson { get; set; }

    /// <summary>
    /// Когда с этой машины позвали оператора. Живёт на устройстве, а не на сессии: кнопка есть и
    /// на запертом экране, где сессии нет вовсе. Повторное нажатие время не сбрасывает — стойка
    /// должна видеть, сколько человек уже ждёт.
    /// </summary>
    public DateTimeOffset? AssistanceRequestedAtUtc { get; set; }

    /// <summary>
    /// Неудачные входы игроков с этого ПК в текущем окне. Предел ПИН-кода живёт на человеке и
    /// не мешает перебирать чужие номера по пять попыток на каждый — этот считает саму машину.
    /// </summary>
    public int PlayerSignInFailedCount { get; set; }

    /// <summary>Когда началось окно счёта неудачных входов. null — неудач ещё не было.</summary>
    public DateTimeOffset? PlayerSignInWindowStartedAtUtc { get; set; }

    /// <summary>
    /// MAC проводного адаптера со шлюзом — последний, что агент сообщил. По нему выключенный ПК
    /// будит сосед: самой машине, пока она спит, команду не отдать.
    /// </summary>
    public string? NetworkMacAddress { get; set; }

    /// <summary>Подсеть этого адаптера: будить можно только из той же подсети.</summary>
    public string? NetworkSubnet { get; set; }

    /// <summary>Широковещательный адрес подсети — куда сосед шлёт волшебный пакет.</summary>
    public string? NetworkBroadcastAddress { get; set; }
}
