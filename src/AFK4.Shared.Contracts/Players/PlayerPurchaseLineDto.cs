namespace AFK4.Shared.Contracts.Players;

/// <summary>Строка покупки: что, сколько и на какую сумму.</summary>
public sealed record PlayerPurchaseLineDto(
    string ProductName,
    int Quantity,
    long UnitPriceMinorUnits,
    long LineTotalMinorUnits);
