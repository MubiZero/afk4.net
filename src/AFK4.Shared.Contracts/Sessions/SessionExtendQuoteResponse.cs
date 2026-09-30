using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Сколько стоит продлить идущую сессию на N минут — по её же тарифу, до нажатия. Гость платит за
/// продление наличными на месте, и оператору нужна точная сумма, а не прикидка в браузере. Ничего
/// не меняет.
/// </summary>
public sealed record SessionExtendQuoteResponse(
    Guid SessionId,
    int AdditionalMinutes,
    MoneyDto Charge);
