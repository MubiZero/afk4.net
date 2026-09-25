namespace AFK4.Shared.Contracts.Consoles;

/// <summary>
/// Консоль на месте — без агента (план `2026-09-25-console-seats.md`): администратор сам начинает и
/// заканчивает сессию, тарифы, касса и отчёты — как у ПК. Снимается консоль тем же «Снять
/// устройство», что и ПК.
/// </summary>
public sealed record CreateConsoleSeatRequest(Guid OrganizationId, Guid SeatId, string DisplayName);

public static class ConsoleSeatErrorCodeNames
{
    /// <summary>На месте уже стоит ПК или консоль.</summary>
    public const string SeatTaken = "console_seat_taken";

    public const string SeatNotFound = "console_seat_not_found";
}

public static class ConsoleSeatLimits
{
    public const int DisplayNameMax = 80;
}
