using System.Text.Json;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Shared.Contracts.Tests;

/// <summary>
/// Первый из трёх рубежей приватности репутации — сам контракт. Клуб видит агрегат и только его,
/// поэтому состав полей закреплён здесь, а не в договорённостях: поле с названием чужого клуба,
/// датой визита или суммой не должно появиться незаметно ни при какой правке.
/// </summary>
public sealed class ReputationContractTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PlayerReputationDto_CarriesExactlyTheFourAllowedFields()
    {
        var actual = typeof(PlayerReputationDto)
            .GetProperties()
            .Where(property => property.Name != "EqualityContract")
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "CalculatedAtUtc", "NetworkBanned", "NetworkNoShows", "NetworkVisits" },
            actual);
    }

    [Fact]
    public void PlayerReputationDto_JsonCarriesNothingBeyondTheAggregate()
    {
        var json = JsonSerializer.Serialize(
            new PlayerReputationDto(14, 0, NetworkBanned: false, DateTimeOffset.Parse("2026-08-20T03:00:00Z")),
            Options);

        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            new[] { "calculatedAtUtc", "networkBanned", "networkNoShows", "networkVisits" },
            names);
    }

    [Fact]
    public void ReputationContracts_RoundTripThroughJson()
    {
        var reputation = new PlayerReputationDto(
            NetworkVisits: 14,
            NetworkNoShows: 2,
            NetworkBanned: true,
            CalculatedAtUtc: DateTimeOffset.Parse("2026-08-20T03:00:00Z"));

        var lookup = new PlayerReputationLookupRequest("+992 90 000-00-01");

        Assert.Equal(
            reputation,
            JsonSerializer.Deserialize<PlayerReputationDto>(JsonSerializer.Serialize(reputation, Options), Options));
        Assert.Equal(
            lookup,
            JsonSerializer.Deserialize<PlayerReputationLookupRequest>(JsonSerializer.Serialize(lookup, Options), Options));
    }
}
