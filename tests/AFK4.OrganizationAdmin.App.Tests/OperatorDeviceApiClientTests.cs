using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.OrganizationAdmin.App.Auth;
using AFK4.OrganizationAdmin.App.Devices;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OperatorDeviceApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
    private static readonly Guid CommandId = Guid.Parse("b1d839b7-90c8-4158-8668-88e1488cbe5a");
    private static readonly Guid CredentialId = Guid.Parse("12f392fd-8eca-448d-a9fc-e9c95651e83c");

    [Fact]
    public async Task CreateEnrollmentCodeAsync_SendsBearerTokenAndBranchRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(new DeviceEnrollmentCodeDto(
            OrganizationId,
            BranchId,
            "AFK4-TEST-CODE",
            DateTimeOffset.Parse("2026-05-12T00:05:00Z"))));
        var client = CreateClient(handler);

        var result = await client.CreateEnrollmentCodeAsync(BranchId, OrganizationId, 300, CancellationToken.None);

        Assert.Equal("AFK4-TEST-CODE", result.Code);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/device-enrollment-codes", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);

        var body = DeserializeRequest<CreateDeviceEnrollmentCodeRequest>(handler.LastRequestBody);
        Assert.Equal(OrganizationId, body.OrganizationId);
        Assert.Equal(300, body.ExpiresInSeconds);
    }

    [Fact]
    public async Task DispatchDeviceCommandAsync_SendsTypeAndPayloadToDeviceEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(new DeviceCommandDto(
            CommandId,
            "lock",
            DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            new Dictionary<string, string>
            {
                ["reason"] = "technician-workflow"
            })));
        var client = CreateClient(handler);

        var result = await client.DispatchDeviceCommandAsync(
            DeviceId,
            "lock",
            new Dictionary<string, string> { ["reason"] = "technician-workflow" },
            CancellationToken.None);

        Assert.Equal(CommandId, result.CommandId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/devices/{DeviceId:D}/commands", handler.LastPathAndQuery);

        var body = DeserializeRequest<DispatchDeviceCommandRequest>(handler.LastRequestBody);
        Assert.Equal("lock", body.Type);
        Assert.Equal("technician-workflow", body.Payload["reason"]);
    }

    [Fact]
    public async Task GetDeviceCommandStatusAsync_ReadsStatusFromDeviceEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(new DeviceCommandStatusDto(
            DeviceId,
            CommandId,
            "lock",
            "Pending",
            null,
            DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            DateTimeOffset.Parse("2026-05-12T00:00:00Z"))));
        var client = CreateClient(handler);

        var result = await client.GetDeviceCommandStatusAsync(DeviceId, CommandId, CancellationToken.None);

        Assert.Equal("Pending", result.Status);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/devices/{DeviceId:D}/commands/{CommandId:D}/status", handler.LastPathAndQuery);
    }

    [Fact]
    public async Task GetDeviceDetailAsync_ReadsDetailFromDeviceEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(new DeviceDetailDto(
            OrganizationId,
            BranchId,
            DeviceId,
            "PC-001",
            "0.1.1",
            "0.1.2",
            DateTimeOffset.Parse("2026-05-13T09:00:00Z"),
            DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            IsOnline: true,
            IsLocked: true,
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            SeatName: "Seat 01",
            ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
            ZoneName: "Main Hall",
            ActiveCredentialCount: 1,
            InstalledAppCount: 2,
            RecentCommands: [])));
        var client = CreateClient(handler);

        var result = await client.GetDeviceDetailAsync(DeviceId, CancellationToken.None);

        Assert.Equal(DeviceId, result.DeviceId);
        Assert.Equal("PC-001", result.MachineName);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/devices/{DeviceId:D}", handler.LastPathAndQuery);
    }

    [Fact]
    public async Task CredentialLifecycleAsync_PostsToRotateAndRevokeEndpoints()
    {
        var call = 0;
        var handler = new RecordingHttpMessageHandler(_ =>
        {
            call++;

            return call == 1
                ? JsonResponse(new RotateDeviceCredentialResponse(
                    OrganizationId,
                    BranchId,
                    DeviceId,
                    CredentialId,
                    "credential-secret",
                    DateTimeOffset.Parse("2026-05-12T00:00:00Z")))
                : JsonResponse(new RevokeDeviceCredentialResponse(
                    OrganizationId,
                    BranchId,
                    DeviceId,
                    CredentialId,
                    DateTimeOffset.Parse("2026-05-12T00:01:00Z")));
        });
        var client = CreateClient(handler);

        var rotated = await client.RotateDeviceCredentialAsync(DeviceId, CancellationToken.None);
        var rotatePath = handler.LastPathAndQuery;
        var revoked = await client.RevokeDeviceCredentialAsync(DeviceId, CredentialId, CancellationToken.None);

        Assert.Equal(CredentialId, rotated.CredentialId);
        Assert.Equal("credential-secret", rotated.CredentialSecret);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/devices/{DeviceId:D}/credentials/rotate", rotatePath);
        Assert.Equal(CredentialId, revoked.CredentialId);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/devices/{DeviceId:D}/credentials/{CredentialId:D}/revoke", handler.LastPathAndQuery);
    }

    private static HttpOperatorDeviceApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        };

        return new HttpOperatorDeviceApiClient(httpClient, new StaticOrganizationAdminTokenStore());
    }

    private static T DeserializeRequest<T>(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result;
    }

    private static HttpResponseMessage JsonResponse<T>(T body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }

        public string? LastPathAndQuery { get; private set; }

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }

    private sealed class StaticOrganizationAdminTokenStore : IOrganizationAdminTokenStore
    {
        public Task SaveAsync(OrganizationAdminTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<OrganizationAdminTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OrganizationAdminTokenSnapshot?>(new OrganizationAdminTokenSnapshot(
                StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                OrganizationId: OrganizationId,
                DisplayName: "Tech One",
                AccessToken: "staff-access-token",
                AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"),
                RefreshToken: "refresh-token",
                RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T00:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
