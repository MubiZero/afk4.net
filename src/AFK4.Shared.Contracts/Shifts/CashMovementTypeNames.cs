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
}
