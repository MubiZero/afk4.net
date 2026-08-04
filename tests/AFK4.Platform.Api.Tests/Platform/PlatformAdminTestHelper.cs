using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

internal static class PlatformAdminTestHelper
{
    public const string DefaultUserName = "owner@platform.test";
    public const string DefaultDisplayName = "Platform Owner";
    public const string DefaultPassword = "Passw0rd!";

    // Fixed test-only TOTP secret. Admins seeded without an explicit `totpSecret` come out "already
    // configured" with this secret, so most tests never have to walk the setup screen — they just
    // need AuthorizeAsAsync to clear /2fa/verify, which it does using this same constant.
    public static readonly byte[] DefaultTotpSecret = Encoding.ASCII.GetBytes("12345678901234567890");

    public static Task<PlatformAdminUserEntity> SeedPlatformAdminAsync(
        PlatformApiFactory factory,
        string userName = DefaultUserName,
        string displayName = DefaultDisplayName,
        string password = DefaultPassword,
        bool isActive = true,
        IEnumerable<string>? roles = null,
        byte[]? totpSecret = null) =>
        SeedPlatformAdminAsync(factory.Services, userName, displayName, password, isActive, roles, totpSecret);

    // Services-based core so callers that only have an ambient IServiceProvider (no PlatformApiFactory
    // reference at hand — see TwoFactorTestHelper) can still seed an admin the same way.
    public static async Task<PlatformAdminUserEntity> SeedPlatformAdminAsync(
        IServiceProvider services,
        string userName = DefaultUserName,
        string displayName = DefaultDisplayName,
        string password = DefaultPassword,
        bool isActive = true,
        IEnumerable<string>? roles = null,
        byte[]? totpSecret = null)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var secretProtector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();
        var hasher = new PasswordHasher<PlatformAdminUserEntity>();
        var roleNames = (roles ?? [PlatformAdminRoleNames.PlatformAdmin]).ToArray();
        var now = DateTimeOffset.Parse("2026-05-23T08:00:00Z");
        var admin = new PlatformAdminUserEntity
        {
            PlatformAdminUserId = Guid.NewGuid(),
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            DisplayName = displayName,
            RolesJson = JsonSerializer.Serialize(roleNames),
            IsActive = isActive,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        // An explicit empty array means "leave 2FA unconfigured" (used by setup-flow tests); the
        // default (null) falls back to DefaultTotpSecret so everyone else's admin comes pre-enrolled.
        var secret = totpSecret ?? DefaultTotpSecret;
        if (secret.Length > 0)
        {
            admin.TotpSecretEncrypted = secretProtector.Protect(Convert.ToBase64String(secret));
            admin.TotpEnabledAtUtc = now;
        }

        dbContext.PlatformAdminUsers.Add(admin);
        await dbContext.SaveChangesAsync();
        return admin;
    }

    public static async Task<PlatformAdminSignInResponse> AuthorizeAsAsync(
        PlatformApiFactory factory,
        HttpClient client,
        string userName = DefaultUserName,
        string password = DefaultPassword,
        IEnumerable<string>? roles = null)
    {
        await SeedPlatformAdminAsync(factory, userName: userName, password: password, roles: roles);

        var signIn = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest(userName, password));
        var challenge = await signIn.Content.ReadFromJsonAsync<PlatformAdminSignInChallengeResponse>();

        Assert.Equal(System.Net.HttpStatusCode.OK, signIn.StatusCode);
        Assert.NotNull(challenge);

        var code = TotpCodeGenerator.Generate(DefaultTotpSecret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var verify = await client.PostAsJsonAsync(
            "/api/platform/auth/2fa/verify",
            new { ChallengeToken = challenge!.ChallengeToken, Code = code });
        var body = await verify.Content.ReadFromJsonAsync<PlatformAdminSignInResponse>();

        Assert.Equal(System.Net.HttpStatusCode.OK, verify.StatusCode);
        Assert.NotNull(body);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        return body;
    }
}
