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
public interface IShellHeartbeatSnapshot
{
    string? SeatingCode { get; }

    DateTimeOffset? SeatingCodeExpiresAtUtc { get; }

    ShellBrandingDto? Branding { get; }

    /// <summary>Интервал, который сервер назвал в последний раз; <c>null</c> — сервер ещё не отвечал.</summary>
    int? IntervalSeconds { get; }

    void Record(string? seatingCode, DateTimeOffset? seatingCodeExpiresAtUtc, ShellBrandingDto? branding, int intervalSeconds);

    /// <summary>Место этого ПК — «ПК 07 · Общий зал». null — ПК не привязан или сервер ещё не отвечал.</summary>
    DeviceSeatDto? Seat { get; }

    /// <summary>Чья сессия идёт на ПК — по словам сервера.</summary>
    DeviceSessionOwnerDto? SessionOwner { get; }

    /// <summary>Права организации по тарифу: без них экран прячет разделы, которых у клуба нет.</summary>
    IReadOnlyList<string>? Features { get; }

    /// <summary>
    /// Место, владелец сессии и права из того же сердцебиения. Отдельно от <see cref="Record"/>:
    /// у них нет своей логики «пустое значит не прислали» — сервер отвечает всем трём сразу.
    /// </summary>
    void RecordPlace(DeviceSeatDto? seat, DeviceSessionOwnerDto? sessionOwner, IReadOnlyList<string>? features);
}

public sealed class ShellHeartbeatSnapshot : IShellHeartbeatSnapshot
{
    private readonly Lock gate = new();
    private string? seatingCode;
    private DateTimeOffset? seatingCodeExpiresAtUtc;
    private ShellBrandingDto? branding;
    private int? intervalSeconds;
    private DeviceSeatDto? seat;
    private DeviceSessionOwnerDto? sessionOwner;
    private IReadOnlyList<string>? features;

    public string? SeatingCode { get { lock (gate) { return seatingCode; } } }

    public DateTimeOffset? SeatingCodeExpiresAtUtc { get { lock (gate) { return seatingCodeExpiresAtUtc; } } }

    public ShellBrandingDto? Branding { get { lock (gate) { return branding; } } }

    public int? IntervalSeconds { get { lock (gate) { return intervalSeconds; } } }

    public DeviceSeatDto? Seat { get { lock (gate) { return seat; } } }

    public DeviceSessionOwnerDto? SessionOwner { get { lock (gate) { return sessionOwner; } } }

    public IReadOnlyList<string>? Features { get { lock (gate) { return features; } } }

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
