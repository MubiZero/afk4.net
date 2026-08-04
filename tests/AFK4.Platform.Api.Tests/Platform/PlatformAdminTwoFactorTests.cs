using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminTwoFactorTests
{
    [Fact]
    public async Task PasswordAlone_DoesNotIssueWorkingSession()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);
        var response = await client.PostAsJsonAsync("/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest(PlatformAdminTestHelper.DefaultUserName, PlatformAdminTestHelper.DefaultPassword));
        var challenge = await response.Content.ReadFromJsonAsync<PlatformAdminSignInChallengeResponse>();

        Assert.NotNull(challenge);
        Assert.False(string.IsNullOrWhiteSpace(challenge!.ChallengeToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", challenge.ChallengeToken);
        var organizations = await client.GetAsync("/api/platform/organizations");
        Assert.Equal(HttpStatusCode.Unauthorized, organizations.StatusCode);
    }

    [Fact]
    public async Task SetupThenCorrectCode_IssuesSessionAndReturnsRecoveryCodesOnce()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var challenge = await TwoFactorTestHelper.StartChallengeAsync(client);
        var setup = await TwoFactorTestHelper.BeginSetupAsync(client, challenge.ChallengeToken);
        var code = TotpCodeGenerator.Generate(TwoFactorTestHelper.DecodeBase32(setup.Secret!), DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var completed = await TwoFactorTestHelper.CompleteSetupAsync(client, challenge.ChallengeToken, code);

        Assert.NotNull(completed.Session);
        Assert.Equal(10, completed.RecoveryCodes.Count);
        var repeat = await TwoFactorTestHelper.BeginSetupAsync(client, challenge.ChallengeToken);
        Assert.Null(repeat.Secret);
    }

    [Fact]
    public async Task FiveWrongCodes_LockVerificationForFifteenMinutes()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await TwoFactorTestHelper.ConfigureTwoFactorAsync(factory, client, out var secret);
        var challenge = await TwoFactorTestHelper.StartChallengeAsync(client);

        for (var attempt = 0; attempt < 5; attempt++)
            await TwoFactorTestHelper.VerifyAsync(client, challenge.ChallengeToken, "000000");
        var correct = TotpCodeGenerator.Generate(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var afterLockout = await TwoFactorTestHelper.VerifyAsync(client, challenge.ChallengeToken, correct);

        Assert.Equal(HttpStatusCode.TooManyRequests, afterLockout.StatusCode);
    }

    [Fact]
    public async Task RecoveryCode_WorksOnceAndBurns()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var codes = await TwoFactorTestHelper.ConfigureTwoFactorAsync(factory, client, out _);
        var challenge = await TwoFactorTestHelper.StartChallengeAsync(client);

        var first = await TwoFactorTestHelper.VerifyAsync(client, challenge.ChallengeToken, codes[0]);
        var secondChallenge = await TwoFactorTestHelper.StartChallengeAsync(client);
        var second = await TwoFactorTestHelper.VerifyAsync(client, secondChallenge.ChallengeToken, codes[0]);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Reset_RequiresManagePermissionAndClearsSecret()
    {
        await using var factory = new PlatformApiFactory();
        using var supportClient = factory.CreateClient();
        var support = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, supportClient,
            userName: "support@platform.test", roles: [PlatformAdminRoleNames.PlatformSupport]);
        using var adminClient = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, adminClient, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var denied = await supportClient.PostAsync($"/api/platform/admins/{support.PlatformAdminId:D}/2fa/reset", null);
        var allowed = await adminClient.PostAsync($"/api/platform/admins/{support.PlatformAdminId:D}/2fa/reset", null);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = await db.PlatformAdminUsers.SingleAsync(x => x.PlatformAdminUserId == support.PlatformAdminId);
        Assert.Null(user.TotpSecretEncrypted);
        Assert.Null(user.TotpEnabledAtUtc);
    }

    [Fact]
    public async Task SetupResponse_CarriesOtpAuthUriWithIssuer()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, totpSecret: []);
        var challenge = await TwoFactorTestHelper.StartChallengeAsync(client);

        var setup = await TwoFactorTestHelper.BeginSetupAsync(client, challenge.ChallengeToken);

        Assert.StartsWith("otpauth://totp/AFK4", setup.OtpAuthUri);
        Assert.Contains("issuer=AFK4", setup.OtpAuthUri);
    }
}
