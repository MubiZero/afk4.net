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

    [Fact]
    public void UpdateTariffRequest_RoundTripsThroughJson()
    {
        var request = new UpdateTariffRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Name: "Standard Plus",
            IsActive: false);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<UpdateTariffRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationId, copy.OrganizationId);
        Assert.Equal(request.Name, copy.Name);
        Assert.Equal(request.IsActive, copy.IsActive);
    }

    [Fact]
    public void UpdateTariffVersionRequest_RoundTripsThroughJson()
    {
        var request = new UpdateTariffVersionRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            CurrencyCode: "TJS",
            PricePerMinuteMinorUnits: 75,
            MinimumBillableMinutes: 20,
            RoundingIncrementMinutes: 5,
            EffectiveFromUtc: DateTimeOffset.Parse("2026-05-22T10:00:00Z"),
            IsActive: true);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<UpdateTariffVersionRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.PricePerMinuteMinorUnits, copy.PricePerMinuteMinorUnits);
        Assert.Equal(request.EffectiveFromUtc, copy.EffectiveFromUtc);
        Assert.Equal(request.IsActive, copy.IsActive);
    }
}
