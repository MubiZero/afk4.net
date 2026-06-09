using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class PlayerCatalogEndpointTests
{
    // ── seeding helpers ──────────────────────────────────────────────────────

    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, string Phone);

    private static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory, string pin)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = $"+99291{player.ToString("N")[..7]}";

        db.Branches.Add(new BranchEntity
        {
            BranchId = branch,
            OrganizationId = org,
            Slug = "test-branch",
            Name = "Test Branch",
            City = "Dushanbe",
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = player,
            OrganizationId = org,
            HomeBranchId = branch,
            DisplayName = "Test Player",
            PhoneNumber = phone,
            PreferredLocale = "ru",
            MarketingOptIn = false,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });

        var credential = new PlayerCredentialEntity
        {
            PlayerCredentialId = Guid.NewGuid(),
            PlayerAccountId = player,
            OrganizationId = org,
            PhoneVerified = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        credential.PasswordHash =
            new PasswordHasher<PlayerCredentialEntity>().HashPassword(credential, pin);
        db.PlayerCredentials.Add(credential);

        await db.SaveChangesAsync();
        return new SeededPlayer(org, branch, phone);
    }

    private static async Task SeedTariffAsync(
        PlatformApiFactory factory, Guid orgId, Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var tariffId = Guid.NewGuid();
        db.Tariffs.Add(new TariffEntity
        {
            TariffId = tariffId,
            OrganizationId = orgId,
            BranchId = branchId,
            Name = "Standard",
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = Guid.NewGuid(),
            TariffId = tariffId,
            OrganizationId = orgId,
            BranchId = branchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 50,
            MinimumBillableMinutes = 30,
            RoundingIncrementMinutes = 15,
            EffectiveFromUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            RetiredAtUtc = null,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedPackageAsync(
        PlatformApiFactory factory, Guid orgId, Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        db.PackageDefinitions.Add(new PackageDefinitionEntity
        {
            PackageDefinitionId = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            Name = "Night 5h",
            CurrencyCode = "TJS",
            PriceMinorUnits = 4000,
            IncludedSeconds = 18000,
            BonusSeconds = 1800,
            ExpiresAfterDays = 30,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task AuthenticateAsync(
        HttpClient client, Guid orgId, string phone, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new AFK4.Shared.Contracts.Players.PlayerSignInRequest(orgId, phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<AFK4.Shared.Contracts.Players.PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }

    // ── tariff tests ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ListTariffs_ForOwnOrgBranch_ReturnsOptions()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedTariffAsync(factory, p.OrgId, p.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.GetAsync(
            $"/api/me/branches/{p.BranchId:D}/tariffs");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<List<TariffOptionDto>>();
        Assert.NotNull(options);
        Assert.NotEmpty(options);
    }

    [Fact]
    public async Task ListTariffs_ForForeignBranch_Returns404()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var foreignBranchId = Guid.NewGuid();
        var response = await client.GetAsync(
            $"/api/me/branches/{foreignBranchId:D}/tariffs");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListTariffs_Unauthenticated_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/me/branches/{Guid.NewGuid():D}/tariffs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── package tests ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListPackages_ForOwnOrgBranch_ReturnsOptions()
    {
        await using var factory = new PlatformApiFactory();
        var p = await SeedPlayerAsync(factory, "1234");
        await SeedPackageAsync(factory, p.OrgId, p.BranchId);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, p.OrgId, p.Phone, "1234");

        var response = await client.GetAsync(
            $"/api/me/branches/{p.BranchId:D}/packages");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await response.Content.ReadFromJsonAsync<List<PackageOptionDto>>();
        Assert.NotNull(options);
        Assert.NotEmpty(options);
    }
}
