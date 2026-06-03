namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerPurchaseLineDto(
    string ProductName,
    int Quantity,
    long UnitPriceMinorUnits,
    long LineTotalMinorUnits);
