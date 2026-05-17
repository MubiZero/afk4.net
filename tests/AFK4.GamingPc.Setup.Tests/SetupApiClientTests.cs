using System.Net;
using System.Text;
using System.Text.Json;
using AFK4.GamingPc.Setup.Core;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.GamingPc.Setup.Tests;

public sealed class SetupApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SignInAsync_PostsStaffCredentialsToStagingOrganization()
    {
        var handler = new RecordingHandler(_ => JsonResponse(new StaffSignInResponse(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            StagingSetupDefaults.OrganizationId,
            "Smoke Operator",
            "access-token",
            DateTimeOffset.UtcNow.AddMinutes(15),
            "refresh-token",
            DateTimeOffset.UtcNow.AddHours(8),
            [StagingSetupDefaults.BranchId],
            [])));
        var client = CreateClient(handler);

        await client.SignInAsync("operator@afk4.test", "secret", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
        Assert.Equal("https://afk4.staging.mubi.dev/api/auth/staff/sign-in", handler.Requests.Single().RequestUri!.ToString());
        var body = await handler.Requests.Single().Content!.ReadAsStringAsync();
        Assert.Contains(StagingSetupDefaults.OrganizationId.ToString("D"), body);
        Assert.Contains("operator@afk4.test", body);
    }

    [Fact]
    public async Task CreateEnrollmentCodeAsync_SendsBearerTokenToBranchEndpoint()
    {
        var handler = new RecordingHandler(_ => JsonResponse(new DeviceEnrollmentCodeDto(
            StagingSetupDefaults.OrganizationId,
            StagingSetupDefaults.BranchId,
            "ABC123",
            DateTimeOffset.UtcNow.AddMinutes(5))));
        var client = CreateClient(handler);

        await client.CreateEnrollmentCodeAsync("access-token", CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(
            $"https://afk4.staging.mubi.dev/api/branches/{StagingSetupDefaults.BranchId:D}/device-enrollment-codes",
            request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("access-token", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task EnrollDeviceAsync_PostsMachineEnrollmentPayload()
    {
        var handler = new RecordingHandler(_ => JsonResponse(new DeviceEnrollmentResponse(
            StagingSetupDefaults.OrganizationId,
            StagingSetupDefaults.BranchId,
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            Guid.Parse("55555555-5555-5555-5555-555555555555"),
            "device-secret",
            DateTimeOffset.UtcNow)));
        var client = CreateClient(handler);

        await client.EnrollDeviceAsync("ABC123", "VM-AFK4-001", CancellationToken.None);

        var request = handler.Requests.Single();
        Assert.Equal("https://afk4.staging.mubi.dev/api/devices/enroll", request.RequestUri!.ToString());
        var body = await request.Content!.ReadAsStringAsync();
        Assert.Contains("ABC123", body);
        Assert.Contains("VM-AFK4-001", body);
        Assert.Contains(StagingSetupDefaults.AgentVersion, body);
    }

    private static SetupApiClient CreateClient(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = StagingSetupDefaults.PlatformBaseUrl });

    private static HttpResponseMessage JsonResponse<T>(T value) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json")
        };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }
}
