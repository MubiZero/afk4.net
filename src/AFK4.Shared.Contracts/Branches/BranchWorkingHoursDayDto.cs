namespace AFK4.Shared.Contracts.Branches;

/// <summary>Один день расписания клуба. DayOfWeek по ISO-8601: 1=Пн … 7=Вс.
/// Время — строка "HH:mm" (24ч); при IsClosed времена игнорируются.</summary>
public sealed record BranchWorkingHoursDayDto(int DayOfWeek, bool IsClosed, string? OpenTime, string? CloseTime);
