using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.Shared.Contracts.Tests;

public sealed class TariffContractSerializationTests
{
    [Fact]
    public void TariffCalculationResult_RoundTripsVersionAndAmount()
    {
        var result = new TariffCalculationResult(
            TariffId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            TariffVersionId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            TariffRuleVersionId: "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb",
            DurationMinutes: 75,
            BillableMinutes: 90,
            Amount: new MoneyDto("TJS", 4500));

        var json = JsonSerializer.Serialize(result);
        var copy = JsonSerializer.Deserialize<TariffCalculationResult>(json);

        Assert.NotNull(copy);
        Assert.Equal(90, copy.BillableMinutes);
        Assert.Equal(4500, copy.Amount.MinorUnits);
        Assert.Equal(result.TariffRuleVersionId, copy.TariffRuleVersionId);
    }
}
