namespace AFK4.Shared.Contracts.Billing;

/// <summary>
/// Деньги игрока в одном клубе: сколько можно потратить, сколько придержано под брони, сколько он
/// должен.
///
/// <paramref name="WalletBalance"/> — доступный остаток, и он таким и остаётся: заморозка под бронь
/// из него уже вычтена, потому что холд и есть отрицательная запись журнала.
/// <paramref name="HeldBalance"/> ничего не переносит и не пересчитывает — оно объясняет, куда
/// делась часть остатка.
///
/// Это ответ на денежную операцию, и он отдельно от <c>PlayerDashboardDto</c> намеренно: идущей
/// сессии здесь нет, и делать вид, что она просто «пустая», значит однажды показать «сессии нет»
/// там, где она есть.
/// </summary>
public sealed record WalletSummaryDto(
    Guid PlayerAccountId,
    MoneyDto WalletBalance,
    MoneyDto HeldBalance,
    MoneyDto DebtBalance,
    IReadOnlyList<LedgerEntryDto> RecentEntries);
