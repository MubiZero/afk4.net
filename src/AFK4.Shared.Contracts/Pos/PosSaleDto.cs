using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Shared.Contracts.Pos;

public sealed record PosSaleDto(
    Guid PosSaleId,
    Guid OrganizationId,
    Guid BranchId,
    Guid ShiftId,
    string State,
    IReadOnlyList<PosSaleLineDto> Lines,
    MoneyDto Total,
    Guid CreatedByStaffUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? RefundedAtUtc,
    DateTimeOffset? VoidedAtUtc,
    ReceiptDto? LatestReceipt = null);
