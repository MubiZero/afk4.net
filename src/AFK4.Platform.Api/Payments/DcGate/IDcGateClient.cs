namespace AFK4.Platform.Api.Payments.DcGate;

public interface IDcGateClient
{
    Task<DcGatePaymentResult> CreatePaymentAsync(
        long amountMinorUnits,
        string currencyCode,
        string externalOrderId,
        object metadata,
        CancellationToken cancellationToken);
}

public sealed record DcGatePaymentResult(
    string PaymentId,
    string Status,
    string Amount,
    string Currency,
    string Comment,
    DateTimeOffset? ExpiresAt,
    string PayUrl);
