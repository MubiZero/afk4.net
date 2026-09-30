namespace AFK4.Shared.Contracts.Operator;

/// <summary>
/// Итоги справочника клиентов по всем подходящим под запрос, а не по открытой странице: шапка
/// «На балансах / Долги» и счётчики отборов. Деньги — сумма положительных остатков по клиентам:
/// минус у одного не гасит плюс у другого.
/// </summary>
public sealed record PlayersSummaryDto(
    int TotalCount,
    int DebtorCount,
    int InactiveCount,
    long WalletTotalMinorUnits,
    long DebtTotalMinorUnits);
