using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PaymentContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ManualPaymentRequest_RoundTrips()
    {
        var organizationId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var request = new ManualPaymentRequest(
            organizationId,
            PaymentMethodNames.Cash,
            new MoneyDto("TJS", 2400),
            "cash drawer",
            "pay-001");

        var copy = JsonSerializer.Deserialize<ManualPaymentRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.Equal(request, copy);
    }

    [Fact]
    public void Constants_ExposeStablePaymentMethodNames()
    {
        Assert.Equal("cash", PaymentMethodNames.Cash);
        Assert.Equal("card_manual", PaymentMethodNames.CardManual);
    }
}
