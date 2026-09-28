using AFK4.Shared.Contracts.Operator;

namespace AFK4.Platform.Api.Search;

/// <summary>
/// Какие виды находок оператору вообще можно показывать. Считается из его прав в филиале:
/// палитра не должна становиться обходом прав — то, чего человеку не видно в разделе, не должно
/// находиться и поиском.
/// </summary>
public sealed record BranchSearchScope(
    bool Seats,
    bool Players,
    bool Reservations,
    bool Receipts,
    bool Orders)
{
    public bool Nothing => !Seats && !Players && !Reservations && !Receipts && !Orders;
}
