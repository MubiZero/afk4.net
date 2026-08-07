using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Platform.Analytics;

/// <summary>Одни свёрнутые сутки клуба. <c>AgentAlive == null</c> — «неизвестно», не «мёртв».</summary>
public sealed record BranchDynamicsDayDto(
    DateOnly Date,
    int SessionCount,
    MoneyDto Revenue,
    int ShiftOpenedCount,
    bool? AgentAlive);

public sealed record BranchDynamicsDto(
    Guid OrganizationId,
    Guid BranchId,
    DateOnly FromDate,
    DateOnly ToDate,
    MoneyDto TotalRevenue,
    int TotalSessionCount,
    int DaysWithoutAgent,
    int DaysWithUnknownAgent,
    /// <summary>Сутки окна, за которые снимка нет вовсе. Нулями они НЕ дорисовываются.</summary>
    int MissingDayCount,
    IReadOnlyList<BranchDynamicsDayDto> Days);
