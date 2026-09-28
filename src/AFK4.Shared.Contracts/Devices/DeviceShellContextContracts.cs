namespace AFK4.Shared.Contracts.Devices;

/// <summary>
/// Место, к которому привязан ПК, — то, что оболочка пишет в шапке: «ПК 07 · Общий зал». Имя
/// места клуб набирает сам, номера отдельно от имени нет.
/// </summary>
public sealed record DeviceSeatDto(
    string Label,
    // Пусто — место без зоны или зона удалена.
    string? ZoneName);

/// <summary>
/// Чья сессия идёт на ПК. Оболочке это нужно, чтобы не открыть вошедшему чужую сессию: посаженный
/// у стойки гость и игрок со своим счётом выглядят по-разному, а вошедший не владелец видит «эта
/// сессия не ваша».
/// </summary>
public sealed record DeviceSessionOwnerDto(
    // Одно из DeviceSessionOwnerKindNames.
    string Kind,
    // Счёт игрока; только у Kind = player.
    Guid? PlayerAccountId = null,
    // Полных лет игроку, если он ввёл день рождения: агент запирает игры старше его возраста.
    // null — возраст неизвестен, и ничего не запирается (дата по желанию, владелец 2026-09-26).
    int? PlayerAge = null);

/// <summary>
/// Идущая на ПК сессия: когда началась и когда кончится. Отсчёт «Осталось» считается от конца
/// сессии, а не от срока аренды — аренда подписана на 15 минут и продлевается, пока сессия идёт.
/// </summary>
public sealed record DeviceLiveSessionDto(
    Guid SessionId,
    DateTimeOffset? StartedAtUtc,
    // null — открытый счёт: конца нет, экран показывает, сколько уже идёт.
    DateTimeOffset? EndsAtUtc);

public static class DeviceSessionOwnerKindNames
{
    /// <summary>Живой сессии на ПК нет.</summary>
    public const string None = "none";

    /// <summary>Сессия без счёта игрока — посадили у стойки.</summary>
    public const string Guest = "guest";

    /// <summary>Сессия на счёте игрока.</summary>
    public const string Player = "player";
}
