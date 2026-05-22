using System.Text.Json;
using AFK4.Shared.Contracts.Operator;

namespace AFK4.Shared.Contracts.Tests;

public sealed class OperatorReferenceContractSerializationTests
{
    [Fact]
    public void PlayerSearchResultDto_RoundTripsThroughJson()
    {
        var result = new PlayerSearchResultDto(
            PlayerAccountId: Guid.Parse("65b9b565-eb5c-4ff5-890c-85f3e12a0fc2"),
            DisplayName: "Alex Player",
            PhoneNumber: "+992000000001",
            WalletBalanceMinorUnits: 12000,
            DebtBalanceMinorUnits: 0,
            ActivePackageCount: 1,
            IsActive: true);

        var json = JsonSerializer.Serialize(result);
        var copy = JsonSerializer.Deserialize<PlayerSearchResultDto>(json);

        Assert.NotNull(copy);
        Assert.Equal("Alex Player", copy.DisplayName);
        Assert.Equal(12000, copy.WalletBalanceMinorUnits);
        Assert.Equal(1, copy.ActivePackageCount);
    }

    [Fact]
    public void TariffOptionDto_RoundTripsThroughJson()
    {
        var option = new TariffOptionDto(
            TariffId: Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            TariffVersionId: Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            Name: "Standard",
            TariffRuleVersionId: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
            VersionNumber: 2,
            CurrencyCode: "TJS",
            PricePerMinuteMinorUnits: 50,
            MinimumBillableMinutes: 30,
            RoundingIncrementMinutes: 15,
            EffectiveFromUtc: DateTimeOffset.Parse("2026-05-13T09:00:00Z"));

        var json = JsonSerializer.Serialize(option);
        var copy = JsonSerializer.Deserialize<TariffOptionDto>(json);

        Assert.NotNull(copy);
        Assert.Equal("Standard", copy.Name);
        Assert.Equal(option.TariffRuleVersionId, copy.TariffRuleVersionId);
        Assert.Equal(50, copy.PricePerMinuteMinorUnits);
        Assert.Equal(option.EffectiveFromUtc, copy.EffectiveFromUtc);
    }

    [Fact]
    public void PackageOptionDto_RoundTripsThroughJson()
    {
        var option = new PackageOptionDto(
            PackageDefinitionId: Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc"),
            Name: "Night 5h",
            CurrencyCode: "TJS",
            PriceMinorUnits: 4000,
            IncludedSeconds: 18000,
            BonusSeconds: 1800,
            ExpiresAfterDays: 30);

        var json = JsonSerializer.Serialize(option);
        var copy = JsonSerializer.Deserialize<PackageOptionDto>(json);

        Assert.NotNull(copy);
        Assert.Equal("Night 5h", copy.Name);
        Assert.Equal(4000, copy.PriceMinorUnits);
        Assert.Equal(19800, copy.IncludedSeconds + copy.BonusSeconds);
    }
}
