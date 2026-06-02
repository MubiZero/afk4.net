using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Sessions;

/// <summary>One part of a split payment: a method and the amount tendered with it.</summary>
public sealed record PaymentPartDto(string PaymentMethod, MoneyDto Amount);
