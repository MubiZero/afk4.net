using AFK4.Shared.Contracts.Payments;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PaymentContractSerializationTests
{
    [Fact]
    public void Constants_ExposeStablePaymentMethodNames()
    {
        Assert.Equal("cash", PaymentMethodNames.Cash);
        Assert.Equal("card_manual", PaymentMethodNames.CardManual);
    }
}
