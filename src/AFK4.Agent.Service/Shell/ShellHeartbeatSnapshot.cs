using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

/// <summary>
/// То, что оболочке нужно из последнего удачного сердцебиения: код посадки со сроком, оформление
/// клуба и интервал, по которому судят, жива ли связь.
///
/// Раньше это были поля работника, и собрать состояние оболочки мог только он — раз в
/// сердцебиение. Теперь их читает сборщик, и канал отдаёт состояние, когда оно изменилось.
///
/// Место, оформление клуба и права тарифа меняются редко и нужны экрану и без связи: после
/// загрузки без интернета (утро после отключения света) оболочка иначе теряла «PC-012 · Основной
/// зал» и логотип клуба до первого сердцебиения. Они лежат на диске (<see cref="FileName"/>) и
/// переписываются, только когда меняются. Код посадки, обслуживание и идущая сессия — только от
/// сервера: устаревшими они врали бы.
/// </summary>
public sealed class ShellHeartbeatSnapshot
{
    public const string FileName = "shell-place.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Lock gate = new();
    private readonly string? path;
    private string? remembered;
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

    public ShellHeartbeatSnapshot(string? stateDirectory = null)
    {
        path = stateDirectory is null ? null : Path.Combine(stateDirectory, FileName);
        if (path is null || !File.Exists(path))
        {
            return;
        }

        try
        {
            remembered = File.ReadAllText(path);
            var place = JsonSerializer.Deserialize<RememberedPlace>(remembered, JsonOptions);
            seat = place?.Seat;
            branding = place?.Branding;
            features = place?.Features;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Нет файла — экран как до первого сердцебиения; вреда больше, чем прежде, нет.
            remembered = null;
        }
    }

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
            RememberPlace();
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
            RememberPlace();
        }
    }

    // Под gate. Пишет, только если что-то поменялось: сердцебиение раз в 10–30 с, а место и
    // оформление клуба меняются раз в недели.
    private void RememberPlace()
    {
        if (path is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new RememberedPlace(seat, branding, features), JsonOptions);
        if (json == remembered)
        {
            return;
        }

        try
        {
            AtomicFile.WriteAllText(path, json);
            remembered = json;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Не записалось — попробуем со следующим сердцебиением.
        }
    }

    private sealed record RememberedPlace(DeviceSeatDto? Seat, ShellBrandingDto? Branding, IReadOnlyList<string>? Features);
}
