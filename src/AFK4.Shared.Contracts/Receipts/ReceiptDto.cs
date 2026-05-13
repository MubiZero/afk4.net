using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Receipts;

public sealed record ReceiptDto(
    Guid ReceiptId,
    Guid OrganizationId,
    Guid BranchId,
    Guid PosSaleId,
    string ReceiptNumber,
    string ReceiptType,
    MoneyDto Total,
    DateTimeOffset CreatedAtUtc);
