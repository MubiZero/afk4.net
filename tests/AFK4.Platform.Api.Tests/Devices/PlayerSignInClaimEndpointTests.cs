using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Devices;

/// <summary>
/// Вход на ПК с телефона по QR (спека оболочки, §5.4): телефон заводит заявку по коду с
/// монитора, ПК забирает её ключом устройства. Код при этом одноразовый — подсмотревший его через
/// плечо больше не успеет им воспользоваться.
/// </summary>
public sealed class PlayerSignInClaimEndpointTests
{
    [Fact]
    public async Task ScanningTheQr_LetsThePcSignThePlayerIn()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();
        var code = (await fixture.HeartbeatAsync()).SeatingCode!;

        var claim = await CreateClaimAsync(phone, code);
        Assert.Equal(PlayerSignInClaimStatusNames.Pending, claim.Status);
        Assert.Equal("ПК 07", claim.SeatLabel);

        // Сигнал мог потеряться — заявку принесёт сердцебиение.
        var beat = await fixture.HeartbeatAsync();
        Assert.Equal(claim.ClaimId, beat.PendingSignInClaim!.ClaimId);

        var redeemed = await RedeemAsync(fixture, claim.ClaimId);
        Assert.Equal(HttpStatusCode.OK, redeemed.StatusCode);
        var session = await redeemed.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>();
        Assert.Equal(fixture.PlayerAccountId, session!.PlayerAccountId);
        Assert.All(await fixture.LiveTokensAsync(), token => Assert.Equal(fixture.Device.DeviceId, token.DeviceId));

        // Приложение узнаёт, что вход состоялся, и пишет «Вы вошли на ПК 07».
        var status = await phone.GetFromJsonAsync<PlayerSignInClaimDto>($"/api/me/devices/sign-in-claims/{claim.ClaimId}");
        Assert.Equal(PlayerSignInClaimStatusNames.Redeemed, status!.Status);
        Assert.Null((await fixture.HeartbeatAsync()).PendingSignInClaim);
    }

    [Fact]
    public async Task TheCode_IsSingleUse_AndThePcGetsANewOne()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();
        var code = (await fixture.HeartbeatAsync()).SeatingCode!;

        await CreateClaimAsync(phone, code);
        var second = await phone.PostAsJsonAsync(
            "/api/me/devices/sign-in-claims", new CreatePlayerSignInClaimRequest(code, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains(SeatingCodeErrorCodeNames.Invalid, await second.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.NotEqual(code, (await fixture.HeartbeatAsync()).SeatingCode);
    }

    [Fact]
    public async Task ARepeatAfterADroppedConnection_ReturnsTheSameClaim()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();
        var code = (await fixture.HeartbeatAsync()).SeatingCode!;
        var key = Guid.NewGuid().ToString("N");

        var first = await CreateClaimAsync(phone, code, key);
        var repeat = await CreateClaimAsync(phone, code, key);

        Assert.Equal(first.ClaimId, repeat.ClaimId);
    }

    [Fact]
    public async Task AClaim_IsRedeemedOnce()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();
        var claim = await CreateClaimAsync(phone, (await fixture.HeartbeatAsync()).SeatingCode!);

        Assert.Equal(HttpStatusCode.OK, (await RedeemAsync(fixture, claim.ClaimId)).StatusCode);
        var again = await RedeemAsync(fixture, claim.ClaimId);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Contains(PlayerSignInClaimErrorCodeNames.AlreadyRedeemed, await again.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AClaimThePcDidNotPickUpInAMinute_Expires()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();
        var claim = await CreateClaimAsync(phone, (await fixture.HeartbeatAsync()).SeatingCode!);

        fixture.Clock.Advance(PlayerSignInClaimService.Lifetime);
        var late = await RedeemAsync(fixture, claim.ClaimId);

        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        Assert.Contains(PlayerSignInClaimErrorCodeNames.Expired, await late.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await fixture.LiveTokensAsync());
    }

    [Fact]
    public async Task OnlyThePcTheCodeCameFrom_CanRedeemTheClaim()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();
        var claim = await CreateClaimAsync(phone, (await fixture.HeartbeatAsync()).SeatingCode!);
        var otherPc = await fixture.EnrollAnotherDeviceAsync();

        using var message = new HttpRequestMessage(
            HttpMethod.Post, $"/api/devices/{otherPc.DeviceId}/sign-in-claims/{claim.ClaimId}/redeem")
        {
            Content = JsonContent.Create(new DeviceRedeemSignInClaimRequest(otherPc.OrganizationId, otherPc.BranchId, otherPc.DeviceId))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, otherPc.CredentialSecret);
        var response = await fixture.Client.SendAsync(message);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WrongCodes_AreCountedPerPlayer()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();

        for (var attempt = 0; attempt < SeatingCodeAttemptGuard.MaxPlayerFailures; attempt++)
        {
            var wrong = await phone.PostAsJsonAsync(
                "/api/me/devices/sign-in-claims", new CreatePlayerSignInClaimRequest("000000", Guid.NewGuid().ToString("N")));
            Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        }

        // Даже верный код не принимается, пока окно не прошло: иначе перебор просто продолжится.
        var code = (await fixture.HeartbeatAsync()).SeatingCode!;
        var closed = await phone.PostAsJsonAsync(
            "/api/me/devices/sign-in-claims", new CreatePlayerSignInClaimRequest(code, Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.TooManyRequests, closed.StatusCode);
        Assert.Contains(SeatingCodeErrorCodeNames.AttemptsExceeded, await closed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        fixture.Clock.Advance(SeatingCodeAttemptGuard.Window);
        var reopened = await phone.PostAsJsonAsync(
            "/api/me/devices/sign-in-claims",
            new CreatePlayerSignInClaimRequest((await fixture.HeartbeatAsync()).SeatingCode!, Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
    }

    [Fact]
    public async Task AClaimForAPcWhereSomeoneElseIsPlaying_IsRefused()
    {
        await using var fixture = DevicePlayerFixture.Create();
        await fixture.SeedAsync();
        using var phone = await fixture.PhoneClientAsync();
        var code = (await fixture.HeartbeatAsync()).SeatingCode!;
        await fixture.StartSessionAsync(playerAccountId: null);

        var response = await phone.PostAsJsonAsync(
            "/api/me/devices/sign-in-claims", new CreatePlayerSignInClaimRequest(code, Guid.NewGuid().ToString("N")));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains(DevicePlayerSignInErrorCodeNames.SessionNotYours, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        // Отказ по чужой сессии код не сжигает и неудачей не считается: код был верный.
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.False(await db.SeatingCodeAttemptCounters.AnyAsync());
    }

    private static async Task<PlayerSignInClaimDto> CreateClaimAsync(HttpClient phone, string code, string? key = null)
    {
        var response = await phone.PostAsJsonAsync(
            "/api/me/devices/sign-in-claims",
            new CreatePlayerSignInClaimRequest(code, key ?? Guid.NewGuid().ToString("N")));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlayerSignInClaimDto>())!;
    }

    private static Task<HttpResponseMessage> RedeemAsync(DevicePlayerFixture fixture, Guid claimId)
    {
        var message = new HttpRequestMessage(
            HttpMethod.Post, $"/api/devices/{fixture.Device.DeviceId}/sign-in-claims/{claimId}/redeem")
        {
            Content = JsonContent.Create(new DeviceRedeemSignInClaimRequest(
                fixture.Device.OrganizationId, fixture.Device.BranchId, fixture.Device.DeviceId))
        };
        message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, fixture.Device.CredentialSecret);
        return fixture.Client.SendAsync(message);
    }
}
