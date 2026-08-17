using AFK4.Platform.Api.Common;
using Microsoft.Extensions.Configuration;

namespace AFK4.Platform.Api.Tests;

public sealed class CorsOriginsTests
{
    private static readonly string[] DeveloperDefaults =
    [
        "https://operator.afk4.local",
        "http://localhost:4174",
        "http://127.0.0.1:4174"
    ];

    private static IConfiguration Configuration(params string[] origins)
    {
        var values = new Dictionary<string, string?>();
        for (var index = 0; index < origins.Length; index++)
        {
            values[$"Cors:OperatorWebOrigins:{index}"] = origins[index];
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void Resolve_OutsideProduction_KeepsDeveloperOriginsAlongsideConfiguredOnes()
    {
        var origins = CorsOrigins.Resolve(
            Configuration("https://admin.afk4.staging.mubi.dev"),
            "Cors:OperatorWebOrigins",
            DeveloperDefaults,
            allowDeveloperDefaults: true);

        // Браузерная проверка со стенда живёт именно на этом порту, и стенд ради неё не
        // перенастраивают — см. docs/operations/organization-admin-access.md.
        Assert.Contains("http://localhost:4174", origins);
        Assert.Contains("https://admin.afk4.staging.mubi.dev", origins);
    }

    [Fact]
    public void Resolve_InProduction_KeepsOnlyConfiguredOrigins()
    {
        var origins = CorsOrigins.Resolve(
            Configuration("https://admin.afk4.example"),
            "Cors:OperatorWebOrigins",
            DeveloperDefaults,
            allowDeveloperDefaults: false);

        Assert.Equal(["https://admin.afk4.example"], origins);
    }

    [Fact]
    public void Resolve_InProductionWithoutConfiguration_AllowsNothing()
    {
        var origins = CorsOrigins.Resolve(
            Configuration(),
            "Cors:OperatorWebOrigins",
            DeveloperDefaults,
            allowDeveloperDefaults: false);

        // Пустой список закрывает браузерные кабинеты целиком. Это заметно сразу и чинится
        // конфигурацией, в отличие от тихо открытого localhost.
        Assert.Empty(origins);
    }

    [Fact]
    public void Resolve_NormalisesAndDeduplicates()
    {
        var origins = CorsOrigins.Resolve(
            Configuration("  http://localhost:4174/  ", "HTTP://LOCALHOST:4174", "  "),
            "Cors:OperatorWebOrigins",
            DeveloperDefaults,
            allowDeveloperDefaults: true);

        Assert.Equal(DeveloperDefaults.Length, origins.Length);
    }
}
