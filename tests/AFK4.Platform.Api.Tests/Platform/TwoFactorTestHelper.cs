using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

// Thin HTTP wrappers around the four 2FA routes introduced in this task. Kept separate from
// PlatformAdminTestHelper because that one only needs to get PAST 2fa/verify for unrelated tests;
// this one exercises the 2FA flow itself (setup, wrong codes, recovery codes, lockout).
internal static class TwoFactorTestHelper
{
    public static async Task<PlatformAdminSignInChallengeResponse> StartChallengeAsync(
        HttpClient client,
        string userName = PlatformAdminTestHelper.DefaultUserName,
        string password = PlatformAdminTestHelper.DefaultPassword)
    {
        // Some callers (the plain setup-flow tests) reach this without seeding an admin first —
        // that's the whole point of that flow (no 2FA configured yet). Seed lazily via the ambient
        // factory rather than requiring every call site to pass one explicitly.
        await EnsureAdminExistsAsync(userName, password);

        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest(userName, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var challenge = await response.Content.ReadFromJsonAsync<PlatformAdminSignInChallengeResponse>();
        Assert.NotNull(challenge);
        return challenge!;
    }

    public static async Task<TwoFactorSetupResult> BeginSetupAsync(HttpClient client, string challengeToken)
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/2fa/setup",
            new { ChallengeToken = challengeToken });

        // Non-2xx covers both "already configured" (409 — the happy-path repeat-setup case) and
        // "challenge no longer valid" (401 — e.g. it was already consumed by a completed sign-in).
        // Callers that care about telling those apart use the raw route directly; this thin wrapper
        // only promises "no secret came back".
        if (response.StatusCode != HttpStatusCode.OK)
        {
            return new TwoFactorSetupResult(null, null);
        }

        var body = await response.Content.ReadFromJsonAsync<TwoFactorSetupResult>();
        Assert.NotNull(body);
        return body!;
    }

    public static async Task<TwoFactorSetupConfirmResult> CompleteSetupAsync(HttpClient client, string challengeToken, string code)
    {
        var response = await client.PostAsJsonAsync(
            "/api/platform/auth/2fa/setup/confirm",
            new { ChallengeToken = challengeToken, Code = code });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TwoFactorSetupConfirmResult>();
        Assert.NotNull(body);
        return body!;
    }

    public static Task<HttpResponseMessage> VerifyAsync(HttpClient client, string challengeToken, string code)
    {
        return client.PostAsJsonAsync(
            "/api/platform/auth/2fa/verify",
            new { ChallengeToken = challengeToken, Code = code });
    }

    // Seeds a fresh admin without 2FA, walks it through start -> setup -> confirm, and returns the
    // recovery codes issued at setup time (they are never retrievable again afterwards). The decoded
    // raw TOTP secret comes back via `out` so callers can generate valid codes for later assertions.
    public static Task<IReadOnlyList<string>> ConfigureTwoFactorAsync(
        PlatformApiFactory factory,
        HttpClient client,
        out byte[] secret)
    {
        PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, totpSecret: []).GetAwaiter().GetResult();
        var challenge = StartChallengeAsync(client).GetAwaiter().GetResult();
        var setup = BeginSetupAsync(client, challenge.ChallengeToken).GetAwaiter().GetResult();
        secret = DecodeBase32(setup.Secret!);
        var code = TotpCodeGenerator.Generate(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var completed = CompleteSetupAsync(client, challenge.ChallengeToken, code).GetAwaiter().GetResult();

        return Task.FromResult(completed.RecoveryCodes);
    }

    // Reverses TotpCodeGenerator.ToBase32 (RFC 4648 base32, no padding).
    public static byte[] DecodeBase32(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in base32)
        {
            var value = alphabet.IndexOf(char.ToUpperInvariant(c));
            if (value < 0)
            {
                continue;
            }

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                bytes.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }

        return bytes.ToArray();
    }

    // Seeds a fresh, not-yet-2FA-configured admin under `userName` if one doesn't already exist in
    // the currently active test database. Resolves the DB through the ambient PlatformApiFactory
    // (see PlatformApiFactory.CurrentAmbientServices) since this helper only receives an HttpClient.
    private static async Task EnsureAdminExistsAsync(string userName, string password)
    {
        var services = PlatformApiFactory.CurrentAmbientServices;
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var normalizedUserName = userName.Trim().ToUpperInvariant();

        var exists = await dbContext.PlatformAdminUsers
            .AnyAsync(admin => admin.NormalizedUserName == normalizedUserName);
        if (exists)
        {
            return;
        }

        await PlatformAdminTestHelper.SeedPlatformAdminAsync(
            services, userName: userName, password: password, totpSecret: []);
    }

    public sealed record TwoFactorSetupResult(string? Secret, string? OtpAuthUri);

    public sealed record TwoFactorSetupConfirmResult(PlatformAdminSignInResponse? Session, IReadOnlyList<string> RecoveryCodes);
}
