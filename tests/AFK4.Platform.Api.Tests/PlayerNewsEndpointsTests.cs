using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.News;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class PlayerNewsEndpointsTests
{
    private sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

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
        return new SeededPlayer(org, branch, player, phone);
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

    private static async Task SeedNewsAsync(PlatformApiFactory factory, Guid orgId, Guid? branchId,
        bool published, DateTimeOffset? publishAt, DateTimeOffset? expiresAt, string title)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.NewsItems.Add(new NewsItemEntity
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            BranchId = branchId,
            Title = title,
            Body = "Body",
            IsPublished = published,
            PublishAtUtc = publishAt,
            ExpiresAtUtc = expiresAt,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetNews_ReturnsOrgWideAndOwnBranchPublishedItems()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var player = await SeedPlayerAsync(factory, "1234");

        await SeedNewsAsync(factory, player.OrgId, null, true, null, null, "OrgWide");
        await SeedNewsAsync(factory, player.OrgId, player.BranchId, true, null, null, "MyBranch");
        await SeedNewsAsync(factory, player.OrgId, Guid.NewGuid(), true, null, null, "OtherBranch");
        await SeedNewsAsync(factory, player.OrgId, null, false, null, null, "Draft");
        await SeedNewsAsync(factory, player.OrgId, null, true, DateTimeOffset.UtcNow.AddHours(1), null, "Future");

        await AuthenticateAsync(client, player.OrgId, player.Phone, "1234");
        var items = await client.GetFromJsonAsync<PlayerNewsItemDto[]>("/api/me/news");

        Assert.Equal(2, items!.Length);
        Assert.Contains(items, news => news.Title == "OrgWide");
        Assert.Contains(items, news => news.Title == "MyBranch");
    }

    [Fact]
    public async Task GetNews_RequiresAuthentication()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/news");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
