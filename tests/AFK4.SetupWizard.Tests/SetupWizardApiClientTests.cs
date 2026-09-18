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
    public async Task SignInByPhoneAsync_PostsToPhoneEndpoint_ReturnsToken()
    {
        var expected = new StaffSignInResponse(
            Guid.NewGuid(), Guid.NewGuid(), "Сотрудник", "access-123",
            DateTimeOffset.UnixEpoch.AddHours(8), "refresh-123", DateTimeOffset.UnixEpoch.AddDays(30),
            new[] { Guid.NewGuid() }, new[] { "organization.devices.install" });
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        var result = await client.SignInByPhoneAsync("+992 93 738-00-70", "246813", CancellationToken.None);

        Assert.Equal("access-123", result.AccessToken);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(StaffAuthRoutes.SignInByPhone, request.RequestUri!.AbsolutePath);
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
            new[] { Guid.NewGuid() }, new[] { "organization.devices.install" });
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        var result = await client.SignInByLoginAsync("owner@club.tj", "246813", CancellationToken.None);

        Assert.NotNull(result.SignedIn);
        Assert.Equal("access-123", result.SignedIn!.AccessToken);
        Assert.Empty(result.Clubs);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(StaffAuthRoutes.SignInByLogin, request.RequestUri!.AbsolutePath);
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

        var result = await client.SignInByLoginAsync("owner@club.tj", "246813", CancellationToken.None);

        Assert.Null(result.SignedIn);
        Assert.Equal(2, result.Clubs.Count);
        Assert.Equal("Клуб А", result.Clubs[0].Name);
    }

    [Fact]
    public async Task SignInToClubAsync_PostsChosenOrganizationInTheBody()
    {
        var expected = new StaffSignInResponse(
            Guid.NewGuid(), OrganizationId, "Сотрудник", "access-123",
            DateTimeOffset.UnixEpoch.AddHours(8), "refresh-123", DateTimeOffset.UnixEpoch.AddDays(30),
            new[] { Guid.NewGuid() }, new[] { "organization.devices.install" });
        var handler = new RecordingHandler(_ => JsonResponse(expected));
        var client = CreateClient(handler);

        var result = await client.SignInToClubAsync(OrganizationId, "owner@club.tj", "246813", CancellationToken.None);

        Assert.Equal("access-123", result.AccessToken);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(StaffAuthRoutes.SignIn, request.RequestUri!.AbsolutePath);
        var body = handler.RequestBodies.Single();
        Assert.Contains(OrganizationId.ToString("D"), body);
        Assert.Contains("owner@club.tj", body);
    }

    [Fact]
    public async Task ResetPasswordByEmailAsync_PostsCodeAndPasswordToResetEndpoint()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = CreateClient(handler);

        await client.ResetPasswordByEmailAsync("owner@club.tj", "123456", "121212", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/auth/staff/reset-password", request.RequestUri!.AbsolutePath);
        var body = handler.RequestBodies.Single();
        Assert.Contains("owner@club.tj", body);
        Assert.Contains("123456", body);
        Assert.Contains("121212", body);
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

        await client.ResetPasswordByPhoneAsync("+992937380070", "123456", "121212", CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/auth/staff/reset-password-by-phone", request.RequestUri!.AbsolutePath);
        var body = handler.RequestBodies.Single();
        Assert.Contains("123456", body);
        Assert.Contains("121212", body);
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
            () => client.ResetPasswordByPhoneAsync("+992937380070", "000000", "121212", CancellationToken.None));

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

        var organizationId = Guid.NewGuid();

        await client.EnrollAuthenticatedAsync(
            organizationId,
            "access-123",
            new AuthenticatedInstallEnrollRequest(Guid.NewGuid(), Guid.NewGuid(), "GamingPc", "Стенд 5", "WIN-1", "pem"),
            CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        // Путь строится общей константой, а не строкой в клиенте: пока их было две, мастер стучался
        // в несуществующий адрес, и обе стороны были зелёными.
        Assert.Equal(InstallRoutes.AuthenticatedEnroll(organizationId), request.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("access-123", request.Headers.Authorization.Parameter);
    }

    // Мастер работает на трёх языках, а текст отказа с сервера всегда английский: показать его
    // человеку нельзя. Код рядом с текстом — единственное, по чему экран назовёт причину сам.
    [Fact]
    public async Task CreateTariffAsync_WhenTheNameIsTaken_SurfacesTheServerCode()
    {
        var handler = new RecordingHandler(_ => ErrorResponse(
            HttpStatusCode.BadRequest,
            """{"error":"Tariff name already exists.","code":"tariff_name_taken"}"""));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SetupWizardApiException>(() => client.CreateTariffAsync(
            OrganizationId, BranchId, "access-123", "Стандарт", 1000, CancellationToken.None));

        Assert.Equal("tariff_name_taken", exception.Code);
    }

    // Код может приехать и в поле error — так отвечают касса, склад и сброс пароля.
    [Fact]
    public async Task EnrollAuthenticatedAsync_WhenTheSeatIsTaken_SurfacesTheServerCode()
    {
        var handler = new RecordingHandler(_ => ErrorResponse(
            HttpStatusCode.Conflict,
            """{"error":"seat_occupied"}"""));
        var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<SetupWizardApiException>(() => client.EnrollAuthenticatedAsync(
            OrganizationId,
            "access-123",
            new AuthenticatedInstallEnrollRequest(BranchId, SeatId, "GamingPc", "Стенд 5", "WIN-1", "pem"),
            CancellationToken.None));

        Assert.Equal("seat_occupied", exception.Code);
    }

    // Свободный текст кодом не считаем: иначе английская фраза уехала бы в интерфейс под видом
    // машинного имени, и экран показал бы её как «причину».
    [Fact]
    public async Task CreateSeatAuthenticatedAsync_WithAPlainEnglishReason_StaysAnOrdinaryHttpFailure()
    {
        var handler = new RecordingHandler(_ => ErrorResponse(
            HttpStatusCode.BadRequest,
            """{"error":"Seat name is required."}"""));
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.CreateSeatAuthenticatedAsync(
            OrganizationId,
            "access-123",
            BranchId,
            ZoneId,
            string.Empty,
            CancellationToken.None));
    }

    private static HttpResponseMessage ErrorResponse(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

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
