using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.OrganizationAdmin.App.Pos;

public interface IOperatorPosApiClient
{
    Task<IReadOnlyList<PosProductDto>> GetCatalogAsync(
        Guid branchId,
        CancellationToken cancellationToken);

    Task<PosSaleDto> CreateSaleAsync(
        Guid branchId,
        CreatePosSaleRequest request,
        CancellationToken cancellationToken);

    Task<PosSaleDto> PaySaleManualAsync(
        Guid saleId,
        ManualPaymentRequest request,
        CancellationToken cancellationToken);

    Task<PosSaleDto> RefundSaleAsync(
        Guid saleId,
        RefundPosSaleRequest request,
        CancellationToken cancellationToken);

    Task<PosSaleDto> VoidSaleAsync(
        Guid saleId,
        VoidPosSaleRequest request,
        CancellationToken cancellationToken);

    Task<PosSaleDto> GetSaleAsync(
        Guid saleId,
        CancellationToken cancellationToken);

    Task<ReceiptDto> GetReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken);
}

public sealed class UnconfiguredOperatorPosApiClient : IOperatorPosApiClient
{
    public Task<IReadOnlyList<PosProductDto>> GetCatalogAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<PosSaleDto> CreateSaleAsync(
        Guid branchId,
        CreatePosSaleRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<PosSaleDto> PaySaleManualAsync(
        Guid saleId,
        ManualPaymentRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<PosSaleDto> RefundSaleAsync(
        Guid saleId,
        RefundPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<PosSaleDto> VoidSaleAsync(
        Guid saleId,
        VoidPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<PosSaleDto> GetSaleAsync(
        Guid saleId,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<ReceiptDto> GetReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    private static InvalidOperationException CreateException()
    {
        return new InvalidOperationException("Operator POS API client is not configured.");
    }
}
