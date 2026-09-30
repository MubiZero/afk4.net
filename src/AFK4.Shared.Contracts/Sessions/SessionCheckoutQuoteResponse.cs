using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Read-only preview of a session checkout: the bill breakdown the operator needs
/// to enter split payments (time charge + attached POS = grand total), the billable
/// seconds for the "Наиграно" display, and — when the session has a player — the
/// wallet balance so a wallet part can be auto-suggested. No state is changed.
/// </summary>
public sealed record SessionCheckoutQuoteResponse(
    Guid SessionId,
    MoneyDto TimeCharge,
    MoneyDto PosTotal,
    MoneyDto GrandTotal,
    int BillableSeconds,
    Guid? PlayerAccountId,
    MoneyDto? WalletBalance,
    // Сколько сыграно (от старта за вычетом пауз) — одно число для «Сыграно» в окне расчёта, в
    // отличие от BillableSeconds, которое у предоплаты ноль: время уже оплачено.
    int PlayedSeconds = 0,
    // Сколько за время этой сессии уплачено вперёд: списано с кошелька при старте и продлениях
    // или отдано наличными гостем. Null — вперёд ничего не платили.
    MoneyDto? PrepaidCharged = null,
    // Что вернётся игроку, если закончить сейчас — на кошелёк, по правилам раннего ухода. Null —
    // возвращать нечего; у гостя, заплатившего наличными, автоматического возврата нет.
    MoneyDto? PrepaidRefund = null);
