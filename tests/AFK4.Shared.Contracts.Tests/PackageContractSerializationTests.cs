using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PackageContractSerializationTests
{
    [Fact]
    public void PlayerPackage_RoundTripsPurchasedSnapshotAndRemainingSeconds()
    {
        var package = new PlayerPackageDto(
            PlayerPackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            PackageDefinitionId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            PlayerAccountId: Guid.Parse("cccccccc-cccc-4ccc-cccc-cccccccccccc"),
            Name: "Night 5h",
            PurchasedPrice: new MoneyDto("TJS", 4000),
            IncludedSeconds: 18000,
            BonusSeconds: 1800,
            RemainingIncludedSeconds: 12000,
            RemainingBonusSeconds: 1800,
            PurchasedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-06-13T10:00:00Z"));

        var json = JsonSerializer.Serialize(package);
        var copy = JsonSerializer.Deserialize<PlayerPackageDto>(json);

        Assert.NotNull(copy);
        Assert.Equal("Night 5h", copy.Name);
        Assert.Equal(12000, copy.RemainingIncludedSeconds);
        Assert.Equal(1800, copy.RemainingBonusSeconds);
    }
}
