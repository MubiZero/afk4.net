using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

/// <summary>
/// То, что оболочке нужно из последнего удачного сердцебиения: код посадки со сроком, оформление
/// клуба и интервал, по которому судят, жива ли связь.
///
/// Раньше это были поля работника, и собрать состояние оболочки мог только он — раз в
/// сердцебиение. Теперь их читает сборщик, и канал отдаёт состояние, когда оно изменилось.
/// </summary>
public sealed class ShellHeartbeatSnapshot
{
    private readonly Lock gate = new();
    private string? seatingCode;
    private DateTimeOffset? seatingCodeExpiresAtUtc;
    private ShellBrandingDto? branding;
    private int? intervalSeconds;
    private DeviceSeatDto? seat;
    private DeviceSessionOwnerDto? sessionOwner;
    private IReadOnlyList<string>? features;
    private DateTimeOffset? maintenanceSinceUtc;
    private string? maintenanceByName;
    private DeviceLiveSessionDto? liveSession;

    /// <summary>Идущая сессия по словам сервера: её начало и конец. null — сессии нет.</summary>
    public DeviceLiveSessionDto? LiveSession { get { lock (gate) { return liveSession; } } }

    public void RecordLiveSession(DeviceLiveSessionDto? liveSession)
    {
        lock (gate)
        {
            this.liveSession = liveSession;
        }
    }

    public string? SeatingCode { get { lock (gate) { return seatingCode; } } }

    public DateTimeOffset? SeatingCodeExpiresAtUtc { get { lock (gate) { return seatingCodeExpiresAtUtc; } } }

    public ShellBrandingDto? Branding { get { lock (gate) { return branding; } } }

    /// <summary>Интервал, который сервер назвал в последний раз; <c>null</c> — сервер ещё не отвечал.</summary>
    public int? IntervalSeconds { get { lock (gate) { return intervalSeconds; } } }

    /// <summary>Место этого ПК — «ПК 07 · Общий зал». null — ПК не привязан или сервер ещё не отвечал.</summary>
    public DeviceSeatDto? Seat { get { lock (gate) { return seat; } } }

    /// <summary>Чья сессия идёт на ПК — по словам сервера.</summary>
    public DeviceSessionOwnerDto? SessionOwner { get { lock (gate) { return sessionOwner; } } }

    /// <summary>
    /// Владелец сессии <paramref name="sessionId"/> — только если сервер назвал его для неё же. Сразу
    /// после старта аренда уже на ПК, а последнее сердцебиение говорит о прошлой сессии: её владелец
    /// новой не подходит — ни для роли на экране, ни для возраста у игр. До следующего сердцебиения
    /// владелец неизвестен (null). Без сессии — то, что сказал сервер.
    /// </summary>
    public DeviceSessionOwnerDto? SessionOwnerFor(Guid? sessionId)
    {
        lock (gate)
        {
            return sessionId is null || liveSession?.SessionId == sessionId ? sessionOwner : null;
        }
    }

    /// <summary>Права организации по тарифу: без них экран прячет разделы, которых у клуба нет.</summary>
    public IReadOnlyList<string>? Features { get { lock (gate) { return features; } } }

    /// <summary>С какого момента ПК на обслуживании — по словам сервера.</summary>
    public DateTimeOffset? MaintenanceSinceUtc { get { lock (gate) { return maintenanceSinceUtc; } } }

    /// <summary>Кто включил обслуживание.</summary>
    public string? MaintenanceByName { get { lock (gate) { return maintenanceByName; } } }

    public void RecordMaintenance(DateTimeOffset? sinceUtc, string? byName)
    {
        lock (gate)
        {
            maintenanceSinceUtc = sinceUtc;
            maintenanceByName = byName;
        }
    }

    /// <summary>
    /// Место, владелец сессии и права из того же сердцебиения. Отдельно от <see cref="Record"/>:
    /// у них нет своей логики «пустое значит не прислали» — сервер отвечает всем трём сразу.
    /// </summary>
    public void RecordPlace(DeviceSeatDto? seat, DeviceSessionOwnerDto? sessionOwner, IReadOnlyList<string>? features)
    {
        lock (gate)
        {
            this.seat = seat;
            this.sessionOwner = sessionOwner;
            this.features = features;
        }
    }

    public void Record(
        string? seatingCode,
        DateTimeOffset? seatingCodeExpiresAtUtc,
        ShellBrandingDto? branding,
        int intervalSeconds)
    {
        lock (gate)
        {
            // Код пустой у занятой машины — это тоже ответ сервера, его надо запомнить.
            this.seatingCode = seatingCode;
            this.seatingCodeExpiresAtUtc = seatingCodeExpiresAtUtc;
            // Оформление переживает ответ без него: логотип не должен мигать оттого, что сервер
            // однажды его не прислал.
            if (branding is not null)
            {
                this.branding = branding;
            }

            this.intervalSeconds = intervalSeconds;
        }
    }
}
