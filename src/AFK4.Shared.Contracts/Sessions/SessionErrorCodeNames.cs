namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Машинные имена отказов по управлению сессией. См. <see cref="Reservations.ReservationErrorCodeNames"/>
/// — та же причина: карта пола обновляется по SignalR, но между тем, что видит стойка, и тем, что
/// уже случилось на сервере, всегда есть зазор в доли секунды, и отказ в него нужно назвать.
/// </summary>
public static class SessionErrorCodeNames
{
    /// <summary>Продлить можно только идущую или на паузе сессию — эту уже закрыли или отменили.</summary>
    public const string NotExtendable = "session_not_extendable";

    /// <summary>Перенести можно только идущую сессию.</summary>
    public const string NotTransferable = "session_not_transferable";

    /// <summary>На паузу можно поставить только идущую сессию.</summary>
    public const string NotPausable = "session_not_pausable";

    /// <summary>Снять с паузы можно только сессию, которая на паузе.</summary>
    public const string NotResumable = "session_not_resumable";

    /// <summary>Закончить можно только идущую или на паузе сессию.</summary>
    public const string NotEndable = "session_not_endable";

    /// <summary>Рассчитать и закрыть счёт можно только по идущей сессии — эту уже закрыли.</summary>
    public const string NotCheckoutable = "session_not_checkoutable";

    /// <summary>Сумма разбивки по способам оплаты не сходится со счётом — счёт успел измениться.</summary>
    public const string CheckoutSplitMismatch = "checkout_split_mismatch";

    /// <summary>Тариф успел измениться: сумма, которую оператор назвал гостю, уже не та.</summary>
    public const string PriceChanged = "price_changed";

    /// <summary>Наличными платит гость без аккаунта; клиенту клуба этот режим не подходит.</summary>
    public const string CashBillingGuestOnly = "cash_billing_guest_only";
}
