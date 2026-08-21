namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// Что случилось с бронью — стойке, прямо сейчас.
///
/// Полоса заявок до сих пор обновлялась опросом: администратор видел чужое решение через
/// несколько секунд, а два администратора на разных машинах какое-то время видели разное. Хуже
/// того, решения принимают и таймеры — срок ответа истекает сам, — и о них узнать было неоткуда,
/// кроме следующего опроса.
///
/// Форма повторяет <c>SessionLifecycleChangedDto</c> намеренно: у операторского экрана уже есть
/// приёмник таких событий с отбором по филиалу, и второй способ доставлять то же самое разошёлся
/// бы с первым на первом же исправлении.
/// </summary>
/// <param name="Kind">Что именно произошло — <see cref="ReservationChangeKinds"/>.</param>
/// <param name="State">Состояние брони после изменения.</param>
public sealed record ReservationChangedDto(
    Guid OrganizationId,
    Guid BranchId,
    Guid ReservationId,
    Guid? SeatId,
    string Kind,
    string State,
    int Version,
    DateTimeOffset StartsAtUtc,
    DateTimeOffset ObservedAtUtc);

/// <summary>
/// Почему бронь изменилась. Отдельно от состояния: «отменена» бывает решением игрока и молчанием
/// клуба, и полоса на экране показывает их по-разному.
/// </summary>
public static class ReservationChangeKinds
{
    public const string Created = "created";

    public const string Confirmed = "confirmed";

    public const string Rejected = "rejected";

    public const string Cancelled = "cancelled";

    public const string Seated = "seated";

    public const string NoShow = "no_show";

    /// <summary>Клуб не ответил в обещанный срок — решение принял таймер, а не человек.</summary>
    public const string Expired = "expired";
}
