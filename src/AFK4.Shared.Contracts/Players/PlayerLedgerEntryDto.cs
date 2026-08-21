namespace AFK4.Shared.Contracts.Players;

using AFK4.Shared.Contracts.Billing;

/// <summary>
/// Строка выписки глазами игрока: что случилось с его деньгами и когда.
///
/// Не то же самое, что <see cref="LedgerEntryDto"/> у стойки, и не должно им быть: там есть
/// табельный номер проведшего сотрудника и служебная причина вида
/// <c>reservation_hold:{guid}</c>. Оператору это нужно — он разбирает спор; игроку это чужая
/// внутренняя кухня, которой в его выписке взяться неоткуда.
/// </summary>
/// <param name="EntryType">
/// Что произошло, кодом из <see cref="LedgerEntryTypeNames"/>. Приложение называет его словами на
/// языке человека — текст с сервера был бы на языке сервера.
/// </param>
/// <param name="QuantitySeconds">
/// Сколько времени принесла или забрала запись: у пакетов и бонусных часов деньги — не вся правда.
/// Ноль у обычных денежных строк.
/// </param>
public sealed record PlayerLedgerEntryDto(
    Guid LedgerEntryId,
    string EntryType,
    MoneyDto Amount,
    int QuantitySeconds,
    DateTimeOffset CreatedAtUtc);
