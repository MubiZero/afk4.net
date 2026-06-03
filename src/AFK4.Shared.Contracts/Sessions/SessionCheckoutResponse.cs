using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Result of a unified session checkout: the bill breakdown, the recorded payment
/// parts, the single receipt covering time + POS, the ending session, and the lock
/// command dispatched to the device.
/// </summary>
public sealed record SessionCheckoutResponse(
    string IdempotencyKey,
    Guid SessionId,
    MoneyDto TimeCharge,
    MoneyDto PosTotal,
    MoneyDto GrandTotal,
    IReadOnlyList<PaymentPartDto> Payments,
    ReceiptDto Receipt,
    SessionDto Session,
    IReadOnlyList<DeviceCommandDto> DeviceCommands);
