using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Вход игрока через агента (спека оболочки, §5.3–5.4): ключ ПК есть только у агента, поэтому и
/// ПИН-код, и заявка QR идут через него, а токены уходят хосту одним кадром auth.
/// </summary>
public sealed class PlayerSignInTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");

    [Fact]
    public async Task TheRightPin_AnswersOk_AndHandsTheTokensToTheHost()
    {
        var fixture = new Fixture();
        var frames = fixture.Host.Attach();

        var reply = await fixture.SignIn.SignInWithPinAsync(PinRequest(), CancellationToken.None);

        Assert.True(reply.Ok);
        Assert.Equal(("+992900000007", "123456"), fixture.Client.PinAttempts.Single());
        Assert.True(frames.TryRead(out var frame));
        Assert.Equal(ShellPipeMessageTypeNames.Auth, frame.Type);
        Assert.Equal("access-token", frame.Auth!.AccessToken);
    }

    [Theory]
    [InlineData(DevicePlayerSignInErrorCodeNames.SignInRefused, ShellPipeErrorCodeNames.SignInRefused)]
    [InlineData(DevicePlayerSignInErrorCodeNames.TooManyAttempts, ShellPipeErrorCodeNames.TooManyAttempts)]
    [InlineData(DevicePlayerSignInErrorCodeNames.SessionNotYours, ShellPipeErrorCodeNames.SessionNotYours)]
    [InlineData(DevicePlayerSignInErrorCodeNames.DeviceInMaintenance, ShellPipeErrorCodeNames.DeviceInMaintenance)]
    public async Task ARefusal_ReachesTheScreenUnderItsOwnName(string serverCode, string pipeCode)
    {
        var fixture = new Fixture();
        var frames = fixture.Host.Attach();
        fixture.Client.Next = PlayerSignInOutcome.Refused(serverCode);

        var reply = await fixture.SignIn.SignInWithPinAsync(PinRequest(), CancellationToken.None);

        Assert.False(reply.Ok);
        Assert.Equal(pipeCode, reply.ErrorCode);
        Assert.False(frames.TryRead(out _));
    }

    [Fact]
    public async Task NoRoadToTheClub_IsSaidPlainly()
    {
        var fixture = new Fixture();
        fixture.Client.Throw = new HttpRequestException("offline");

        var reply = await fixture.SignIn.SignInWithPinAsync(PinRequest(), CancellationToken.None);

        Assert.Equal(ShellPipeErrorCodeNames.PlatformUnreachable, reply.ErrorCode);
    }

    [Fact]
    public async Task APcUnderMaintenance_DoesNotEvenAskTheServer()
    {
        var fixture = new Fixture();
        fixture.RuntimeState.Save(AgentRuntimeState.Maintenance(Now));

        var reply = await fixture.SignIn.SignInWithPinAsync(PinRequest(), CancellationToken.None);

        Assert.Equal(ShellPipeErrorCodeNames.DeviceInMaintenance, reply.ErrorCode);
        Assert.Empty(fixture.Client.PinAttempts);
    }

    [Fact]
    public async Task AnEmptyPin_IsNotSentAnywhere()
    {
        var fixture = new Fixture();

        var reply = await fixture.SignIn.SignInWithPinAsync(PinRequest(pin: " "), CancellationToken.None);

        Assert.Equal(ShellPipeErrorCodeNames.InvalidPayload, reply.ErrorCode);
        Assert.Empty(fixture.Client.PinAttempts);
    }

    [Fact]
    public async Task AQrClaim_IsRedeemedOnce_EvenWhenItArrivesTwice()
    {
        // Событие хаба и сердцебиение приносят одну и ту же заявку — забрать её надо один раз.
        var fixture = new Fixture();
        var frames = fixture.Host.Attach();
        var claimId = Guid.NewGuid();

        await fixture.SignIn.RedeemClaimAsync(claimId, CancellationToken.None);
        await fixture.SignIn.RedeemClaimAsync(claimId, CancellationToken.None);

        Assert.Equal([claimId], fixture.Client.Redeemed);
        Assert.True(frames.TryRead(out var frame));
        Assert.Equal(ShellPipeMessageTypeNames.Auth, frame.Type);
        Assert.False(frames.TryRead(out _));
    }

    [Fact]
    public async Task AQrClaim_WaitsWhileThePlayerScreenIsAway()
    {
        // Токены без экрана ушли бы в пустоту: заявка подождёт в сердцебиении или истечёт.
        var fixture = new Fixture();

        await fixture.SignIn.RedeemClaimAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(fixture.Client.Redeemed);
    }

    [Fact]
    public async Task AQrClaim_DoesNotOpenAPcUnderMaintenance()
    {
        var fixture = new Fixture();
        fixture.Host.Attach();
        fixture.RuntimeState.Save(AgentRuntimeState.Maintenance(Now));

        await fixture.SignIn.RedeemClaimAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(fixture.Client.Redeemed);
    }

    [Fact]
    public async Task AClaimThatDidNotGetThrough_IsTriedAgainNextTime()
    {
        var fixture = new Fixture();
        var frames = fixture.Host.Attach();
        var claimId = Guid.NewGuid();
        fixture.Client.Throw = new HttpRequestException("offline");

        await fixture.SignIn.RedeemClaimAsync(claimId, CancellationToken.None);
        fixture.Client.Throw = null;
        await fixture.SignIn.RedeemClaimAsync(claimId, CancellationToken.None);

        Assert.Equal([claimId, claimId], fixture.Client.Redeemed);
        Assert.True(frames.TryRead(out _));
    }

    [Fact]
    public async Task ThePipeRequestHandler_PassesSignInOn()
    {
        var fixture = new Fixture();
        fixture.Host.Attach();
        var handler = new PlayerShellRequestHandler(
            new NoApps(),
            new NoLauncher(),
            fixture.RuntimeState,
            new NoAssistance(),
            TimeProvider.System,
            NullLogger<PlayerShellRequestHandler>.Instance,
            fixture.SignIn);

        var reply = await handler.HandleAsync(PinRequest(), CancellationToken.None);

        Assert.True(reply.Ok);
    }

    private static ShellPipeRequestDto PinRequest(string phone = "+992900000007", string pin = "123456") => new(
        Guid.NewGuid(),
        ShellPipeRequestTypeNames.SignInPin,
        new Dictionary<string, string> { [PlayerSignIn.PhonePayloadKey] = phone, [PlayerSignIn.PinPayloadKey] = pin });

    private static PlatformPersonSessionResponse Session() => new(
        PlayerAccountId: Guid.NewGuid(),
        OrganizationId: Guid.NewGuid(),
        DisplayName: "Фарход",
        PhoneVerified: true,
        AccessToken: "access-token",
        AccessTokenExpiresAtUtc: Now.AddMinutes(15),
        RefreshToken: "refresh-token",
        RefreshTokenExpiresAtUtc: Now.AddHours(12),
        PlatformPersonId: Guid.NewGuid(),
        PreferredLocale: "ru",
        ProfileCompleted: true);

    private sealed class Fixture
    {
        public Fixture()
        {
            SignIn = new PlayerSignIn(Client, RuntimeState, Host, new ShellStateSignal(), TimeProvider.System, NullLogger<PlayerSignIn>.Instance);
        }

        public FakeSignInClient Client { get; } = new();

        public PlayerShellStateBuilderTests.MemoryRuntimeStateStore RuntimeState { get; } = new(AgentRuntimeState.Locked(Now));

        public ShellHostChannel Host { get; } = new();

        public PlayerSignIn SignIn { get; }
    }

    private sealed class FakeSignInClient : IPlayerSignInClient
    {
        public PlayerSignInOutcome Next { get; set; } = new(Session(), null);

        public Exception? Throw { get; set; }

        public List<(string Phone, string Pin)> PinAttempts { get; } = [];

        public List<Guid> Redeemed { get; } = [];

        public Task<PlayerSignInOutcome> SignInWithPinAsync(string phone, string pin, CancellationToken cancellationToken)
        {
            PinAttempts.Add((phone, pin));
            return Throw is null ? Task.FromResult(Next) : Task.FromException<PlayerSignInOutcome>(Throw);
        }

        public Task<PlayerSignInOutcome> RedeemClaimAsync(Guid claimId, CancellationToken cancellationToken)
        {
            Redeemed.Add(claimId);
            return Throw is null ? Task.FromResult(Next) : Task.FromException<PlayerSignInOutcome>(Throw);
        }
    }

    private sealed class NoApps : IProcessPolicyEnforcer
    {
        public AgentLauncherAppOptions? FindAllowedLauncherApp(string appId) => null;

        public Task EnforceAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoLauncher : IProcessLauncher
    {
        public Task LaunchAsync(string executablePath, string arguments, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class NoAssistance : IAssistanceRequestReporter
    {
        public Task ReportAsync(DateTimeOffset requestedAtUtc, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
