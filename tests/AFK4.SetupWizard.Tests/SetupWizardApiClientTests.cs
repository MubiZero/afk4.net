using System.Net;
using System.Text;
using System.Text.Json;
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Tests;

public sealed class SetupWizardApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid ZoneId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SeatId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    [Fact]
    public async Task DiscoverAsync_PostsOwnerCodeToInstallDiscover()
    {
        var handler = new RecordingHandler(_ => JsonResponse(new InstallDiscoverResponse("Owner", [])));
        var client = CreateClient(handler);

        await client.DiscoverAsync("12345678", CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://afk4.staging.mubi.dev/api/install/discover", request.RequestUri!.ToString());
        Assert.Contains("12345678", handler.RequestBodies.Single());
    }

    [Fact]
    public async Task CreateSeatAsync_PostsOwnerCodeScopedSeatRequest()
    {
        var handler = new RecordingHandler(_ => JsonResponse(new InstallCreateSeatResponse(
            OrganizationId,
            BranchId,
            ZoneId,
            SeatId,
            "PC-NEW",
            SortOrder: 3)));
        var client = CreateClient(handler);

        await client.CreateSeatAsync("12345678", BranchId, ZoneId, "PC-NEW", CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://afk4.staging.mubi.dev/api/install/seats", request.RequestUri!.ToString());
        var body = handler.RequestBodies.Single();
        Assert.Contains("12345678", body);
        Assert.Contains(BranchId.ToString("D"), body);
        Assert.Contains(ZoneId.ToString("D"), body);
        Assert.Contains("PC-NEW", body);
    }

    [Fact]
    public async Task EnrollAsync_PostsSelectedInstallEnrollmentPayload()
    {
        var handler = new RecordingHandler(_ => JsonResponse(new InstallEnrollResponse(
            OrganizationId,
            BranchId,
            Guid.Parse("33333333-3333-4333-8333-333333333333"),
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            "credential-secret",
            DeviceEnrollmentStateNames.Approved,
            "https://afk4.staging.mubi.dev",
            "internal",
            DateTimeOffset.Parse("2026-05-25T12:00:00Z"))));
        var client = CreateClient(handler);

        await client.EnrollAsync(
            new InstallEnrollRequest(
                "12345678",
                BranchId,
                SeatId,
                DeviceRoleNames.GamingPc,
                "PC-001",
                "WIN-PC-001",
                "device-public-key"),
            CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://afk4.staging.mubi.dev/api/install/enroll", request.RequestUri!.ToString());
        var body = handler.RequestBodies.Single();
        Assert.Contains("12345678", body);
        Assert.Contains(SeatId.ToString("D"), body);
        Assert.Contains(DeviceRoleNames.GamingPc, body);
        Assert.Contains("device-public-key", body);
    }

    [Fact]
    public async Task SignInByPhoneAsync_PostsToPhoneEndpoint_ReturnsToken()
    {
        var expected = new StaffSignInResponse(
            Guid.NewGuid(), Guid.NewGuid(), "Сотрудник", "access-123",
            DateTimeOffset.UnixEpoch.AddHours(8), "refresh-123", DateTimeOffset.UnixEpoch.AddDays(30),
            new[] { Guid.NewGuid() }, new[] { "devices.install" });
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        var result = await client.SignInByPhoneAsync("+992 93 738-00-70", "Passw0rd!", CancellationToken.None);

        Assert.Equal("access-123", result.AccessToken);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/auth/staff/sign-in-by-phone", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ForgotPasswordByEmailAsync_PostsLoginOrEmailToForgotEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.ForgotPasswordByEmailAsync("owner@club.tj", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/auth/staff/forgot-password", request.RequestUri!.AbsolutePath);
        Assert.Contains("owner@club.tj", handler.RequestBodies.Single());
    }

    [Fact]
    public async Task SignInByLoginAsync_SingleClub_PostsToSignInByLoginAndReturnsSession()
    {
        var expected = new StaffSignInResponse(
            Guid.NewGuid(), OrganizationId, "Сотрудник", "access-123",
            DateTimeOffset.UnixEpoch.AddHours(8), "refresh-123", DateTimeOffset.UnixEpoch.AddDays(30),
            new[] { Guid.NewGuid() }, new[] { "devices.install" });
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        var result = await client.SignInByLoginAsync("owner@club.tj", "Passw0rd!", CancellationToken.None);

        Assert.NotNull(result.SignedIn);
        Assert.Equal("access-123", result.SignedIn!.AccessToken);
        Assert.Empty(result.Clubs);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/auth/staff/sign-in-by-login", request.RequestUri!.AbsolutePath);
        Assert.Contains("owner@club.tj", handler.RequestBodies.Single());
    }

    [Fact]
    public async Task SignInByLoginAsync_MultipleClubs_ReturnsClubChoices()
    {
        var clubs = new StaffSignInChooseClubResponse(new[]
        {
            new StaffSignInClubChoice(OrganizationId, "Клуб А"),
            new StaffSignInClubChoice(Guid.NewGuid(), "Клуб Б"),
        });
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent(JsonSerializer.Serialize(clubs, JsonOptions), Encoding.UTF8, "application/json"),
        });
        var client = CreateClient(handler);

        var result = await client.SignInByLoginAsync("owner@club.tj", "Passw0rd!", CancellationToken.None);

        Assert.Null(result.SignedIn);
        Assert.Equal(2, result.Clubs.Count);
        Assert.Equal("Клуб А", result.Clubs[0].Name);
    }

    [Fact]
    public async Task SignInToClubAsync_PostsOrganizationScopedSignIn()
    {
        var expected = new StaffSignInResponse(
            Guid.NewGuid(), OrganizationId, "Сотрудник", "access-123",
            DateTimeOffset.UnixEpoch.AddHours(8), "refresh-123", DateTimeOffset.UnixEpoch.AddDays(30),
            new[] { Guid.NewGuid() }, new[] { "devices.install" });
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        var result = await client.SignInToClubAsync(OrganizationId, "owner@club.tj", "Passw0rd!", CancellationToken.None);

        Assert.Equal("access-123", result.AccessToken);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/auth/staff/sign-in", request.RequestUri!.AbsolutePath);
        var body = handler.RequestBodies.Single();
        Assert.Contains(OrganizationId.ToString("D"), body);
        Assert.Contains("owner@club.tj", body);
    }

    [Fact]
    public async Task ResetPasswordByEmailAsync_PostsCodeAndPasswordToResetEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.ResetPasswordByEmailAsync("owner@club.tj", "123456", "Passw0rd!New", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/auth/staff/reset-password", request.RequestUri!.AbsolutePath);
        var body = handler.RequestBodies.Single();
        Assert.Contains("owner@club.tj", body);
        Assert.Contains("123456", body);
        Assert.Contains("Passw0rd!New", body);
    }

    [Fact]
    public async Task ForgotPasswordByPhoneAsync_PostsPhoneToForgotByPhoneEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.ForgotPasswordByPhoneAsync("+992937380070", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/auth/staff/forgot-password-by-phone", request.RequestUri!.AbsolutePath);
        Assert.Contains("992937380070", handler.RequestBodies.Single());
    }

    [Fact]
    public async Task ResetPasswordByPhoneAsync_PostsCodeAndPasswordToResetByPhoneEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.ResetPasswordByPhoneAsync("+992937380070", "123456", "Passw0rd!New", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/auth/staff/reset-password-by-phone", request.RequestUri!.AbsolutePath);
        var body = handler.RequestBodies.Single();
        Assert.Contains("123456", body);
        Assert.Contains("Passw0rd!New", body);
    }

    [Fact]
    public async Task ResetPasswordByPhoneAsync_OnError_ThrowsWithCodeAndRemainingAttempts()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                "{\"error\":\"invalid_code\",\"remainingAttempts\":2}",
                Encoding.UTF8,
                "application/json")
        });
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SetupWizardApiException>(
            () => client.ResetPasswordByPhoneAsync("+992937380070", "000000", "Passw0rd!New", CancellationToken.None));

        Assert.Equal("invalid_code", exception.Code);
        Assert.Equal(2, exception.RemainingAttempts);
    }

    [Fact]
    public async Task EnrollAuthenticatedAsync_AttachesBearerToken_PostsToAuthEnroll()
    {
        var expected = new InstallEnrollResponse(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "secret",
            "Approved", "https://api", "stable", DateTimeOffset.UnixEpoch);
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        await client.EnrollAuthenticatedAsync(
            "access-123",
            new AuthenticatedInstallEnrollRequest(Guid.NewGuid(), Guid.NewGuid(), "GamingPc", "Стенд 5", "WIN-1", "pem"),
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/api/install/auth/enroll", request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("access-123", request.Headers.Authorization.Parameter);
    }

    private static SetupWizardApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = SetupWizardDefaults.PlatformBaseUrl });

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (request.Content is not null)
            {
                RequestBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return respond(request);
        }
    }
}
