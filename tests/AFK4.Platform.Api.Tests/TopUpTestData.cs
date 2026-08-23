using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

/// <summary>Игрок с PIN, его клуб и настроенный мерчант банка — заготовка для тестов пополнения.</summary>
internal static class TopUpTestData
{
    internal sealed record SeededPlayer(Guid OrgId, Guid BranchId, Guid PlayerId, string Phone);

    public static async Task<SeededPlayer> SeedPlayerAsync(PlatformApiFactory factory, string pin)
    {
        var org = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var player = Guid.NewGuid();
        var phone = TestPhones.Next();
        var now = DateTimeOffset.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            // Без строки организации права на возможности резолвятся как «клуб неизвестен», и
            // онлайн-пополнение выключено ещё до всякого мерчанта.
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = org,
                Name = "Клуб с онлайн-оплатой",
                CreatedAtUtc = now
            });
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = player,
                OrganizationId = org,
                HomeBranchId = branch,
                DisplayName = "Игрок",
                PhoneNumber = phone,
                PreferredLocale = "ru",
                IsActive = true,
                CreatedAtUtc = now
            });
            await db.SaveChangesAsync();
        }

        await PlayerPinTestData.AttachPersonWithPinAsync(factory, player, phone, pin);
        return new SeededPlayer(org, branch, player, phone);
    }

    public static async Task SeedMerchantConfigAsync(
        PlatformApiFactory factory, Guid organizationId, int merchantId = 48741)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<AFK4.Platform.Api.Security.ISecretProtector>();
        db.EskhataMerchantConfigs.Add(new EskhataMerchantConfigEntity
        {
            EskhataMerchantConfigId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = null,
            BaseUrl = "https://bank.test",
            CompanyId = "test-company",
            MerchantId = merchantId,
            HashKeyEncrypted = protector.Protect("test-hash-key"),
            Status = EskhataMerchantConfigStatus.Configured,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public static async Task AuthenticateAsync(HttpClient client, SeededPlayer player, string pin)
    {
        var signIn = await client.PostAsJsonAsync(
            "/api/public/player/sign-in", new PlayerSignInRequest(player.OrgId, player.Phone, pin));
        signIn.EnsureSuccessStatusCode();
        var tokens = await signIn.Content.ReadFromJsonAsync<PlayerSignInResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens!.AccessToken);
    }
}
