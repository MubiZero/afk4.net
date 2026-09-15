namespace AFK4.Shared.Contracts.Loyalty;

/// <summary>Начисление кешбэка: сколько, когда и за что.</summary>
public sealed record CashbackEntryDto(
    long AmountMinorUnits,
    string CurrencyCode,
    // Служебная причина вида `cashback:topup` или `cashback:shop:{id}`. Разбирается на экране в
    // человеческую подпись: показывать игроку внутреннее имя события незачем.
    string Reason,
    DateTimeOffset CreatedAtUtc);
