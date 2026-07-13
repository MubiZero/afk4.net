using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Platform.Api.Pos;

public static class PosSaleProjection
{
    public static PosSaleDto ToDto(
        PosSaleEntity sale,
        IReadOnlyList<PosSaleLineEntity> lines,
        ReceiptEntity? latestReceipt = null,
        Guid? shopOrderId = null) =>
        new(
            sale.PosSaleId,
            sale.OrganizationId,
            sale.BranchId,
            sale.ShiftId,
            sale.State,
            lines.Select(ToDto).ToList(),
            new MoneyDto(sale.CurrencyCode, sale.TotalMinorUnits),
            sale.CreatedByStaffUserId,
            sale.CreatedAtUtc,
            sale.PaidAtUtc,
            sale.RefundedAtUtc,
            sale.VoidedAtUtc,
            latestReceipt is null ? null : ToDto(latestReceipt),
            sale.PlayerAccountId,
            shopOrderId);

    private static ReceiptDto ToDto(ReceiptEntity receipt) =>
        new(
            receipt.ReceiptId,
            receipt.OrganizationId,
            receipt.BranchId,
            receipt.PosSaleId,
            receipt.ReceiptNumber,
            receipt.ReceiptType,
            new MoneyDto(receipt.CurrencyCode, receipt.TotalMinorUnits),
            receipt.CreatedAtUtc);

    private static PosSaleLineDto ToDto(PosSaleLineEntity line) =>
        new(
            line.ProductId,
            line.ProductName,
            line.Quantity,
            new MoneyDto(line.CurrencyCode, line.UnitPriceMinorUnits),
            new MoneyDto(line.CurrencyCode, line.LineTotalMinorUnits));
}
