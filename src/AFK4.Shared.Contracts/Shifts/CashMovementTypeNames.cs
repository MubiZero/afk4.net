namespace AFK4.Shared.Contracts.Shifts;

public static class CashMovementTypeNames
{
    public const string CashIn = "cash_in";

    public const string CashOut = "cash_out";
}

/// <summary>
/// Причины движений кассы, которые пишет сам сервер, — кодом, а не русской фразой: Панель
/// подписывает их на языке экрана. После кода через двоеточие — имя получателя.
/// </summary>
public static class CashMovementReasonNames
{
    /// <summary>«tip_payout:Шерзод» — выданы чаевые администратору.</summary>
    public const string TipPayout = "tip_payout";

    /// <summary>
    /// Гость оплатил игру наличными у стойки. Эта фраза уже записана в платежах, поэтому она и есть
    /// код: Панель узнаёт её и пишет по-русски, а не переименовывает чужие строки в базе.
    /// </summary>
    public const string GuestGameplayCash = "guest gameplay paid in cash";

    /// <summary>Наличные, принятые при расчёте сессии (запись платежа со времён единого расчёта).</summary>
    public const string SessionCheckout = "session checkout";
}
