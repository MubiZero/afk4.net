using AFK4.Platform.Api.Billing;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;

namespace AFK4.Platform.Api.Payments;

public sealed class ManualPaymentProvider : IPaymentProvider
{
    public Task<BillingCommandServiceResult<PaymentProviderResult>> AcceptManualPaymentAsync(
        ManualPaymentRequest request,
        MoneyDto expectedAmount,
        CancellationToken cancellationToken)
    {
        if (request.PaymentMethod is not PaymentMethodNames.Cash and not PaymentMethodNames.CardManual)
        {
            return Task.FromResult(BillingCommandServiceResult<PaymentProviderResult>.Invalid("Unsupported manual payment method."));
        }

        if (request.Amount.MinorUnits <= 0)
        {
            return Task.FromResult(BillingCommandServiceResult<PaymentProviderResult>.Invalid("Payment amount must be positive."));
        }

        if (!string.Equals(request.Amount.CurrencyCode, expectedAmount.CurrencyCode, StringComparison.OrdinalIgnoreCase) ||
            request.Amount.MinorUnits != expectedAmount.MinorUnits)
        {
            return Task.FromResult(BillingCommandServiceResult<PaymentProviderResult>.Invalid("Payment amount must equal the sale total."));
        }

        var result = new PaymentProviderResult(
            "manual",
            request.PaymentMethod,
            new MoneyDto(expectedAmount.CurrencyCode.ToUpperInvariant(), expectedAmount.MinorUnits),
            request.Note.Trim());

        return Task.FromResult(BillingCommandServiceResult<PaymentProviderResult>.Ok(result));
    }
}
