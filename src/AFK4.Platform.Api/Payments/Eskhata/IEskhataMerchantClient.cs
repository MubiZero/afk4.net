namespace AFK4.Platform.Api.Payments.Eskhata;

public interface IEskhataMerchantClient
{
    Task<EskhataCreateOrderResult> CreateOrderAsync(
        string invoiceId, long amountMinor, string currencyCode, string description,
        int merchantId, CancellationToken cancellationToken);

    // Возвращает orderStatus (NEW/IN PROCESS/COMPLETED/CANCELED/REFUNDED) или null при неуспехе банка.
    Task<string?> GetOrderStatusAsync(
        string invoiceId, string orderId, long amountMinor, string currencyCode, int posId,
        CancellationToken cancellationToken);
}

public sealed record EskhataCreateOrderResult(
    string OrderId, string OrderStatus, string? Qr, string? InvoiceUrl, int PosId);
