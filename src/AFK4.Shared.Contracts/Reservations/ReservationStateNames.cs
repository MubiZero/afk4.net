namespace AFK4.Shared.Contracts.Reservations;

public static class ReservationStateNames
{
    public const string Pending = "pending";

    public const string Confirmed = "confirmed";

    public const string Seated = "seated";

    public const string Cancelled = "cancelled";

    /// <summary>
    /// Игрок не приехал. Отдельно от отмены намеренно: отменённая бронь — это решение человека
    /// или клуба, а неявка — его отсутствие, и стоить она может денег. Пока оба исхода изображала
    /// одна «отмена» с пометкой в свободном тексте, отличить их можно было только сравнением строк.
    /// </summary>
    public const string NoShow = "no_show";

    /// <summary>
    /// Клуб отказал в заявке — с причиной. Не отмена: игрок ничего не отменял, и в его репутации
    /// чужой отказ появляться не должен.
    /// </summary>
    public const string Rejected = "rejected";
}
