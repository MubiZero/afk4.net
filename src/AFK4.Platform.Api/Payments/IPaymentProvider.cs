using AFK4.Platform.Api.Billing;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;

namespace AFK4.Platform.Api.Payments;

public interface IPaymentProvider
{
    Task<BillingCommandServiceResult<PaymentProviderResult>> AcceptManualPaymentAsync(
        ManualPaymentRequest request,
        MoneyDto expectedAmount,
        CancellationToken cancellationToken);
}

public sealed record PaymentProviderResult(
    string Provider,
    string PaymentMethod,
    MoneyDto Amount,
    string Note);
