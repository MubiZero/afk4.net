namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Settle a session in one action: pay the unified bill (time charge + attached
/// POS sales) with one or more <see cref="PaymentPartDto"/> parts, then lock the PC.
/// </summary>
public sealed record SessionCheckoutRequest(
    Guid OrganizationId,
    IReadOnlyList<PaymentPartDto> Payments,
    string IdempotencyKey);
