using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.OrganizationAdmin.App.Auth;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OperatorUpdateApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid RolloutId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid PackageId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");

    [Fact]
    public async Task GetRolloutStatusesAsync_SendsBearerTokenAndReadsStatusList()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse<IReadOnlyList<UpdateRolloutStatusDto>>(
        [
            new UpdateRolloutStatusDto(
                RolloutId,
                OrganizationId,
                BranchId,
                PackageId,
                UpdateComponentNames.AgentService,
                "1.2.3",
                UpdateChannelNames.Beta,
                UpdateRolloutStateNames.Active,
                UpdateTargetKindNames.Branch,
                [],
                BatchPercent: 100,
                CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T12:00:00Z"),
                StartsAtUtc: DateTimeOffset.Parse("2026-05-14T12:30:00Z"),
                CompletedAtUtc: null,
                DeviceStatuses:
                [
                    new DeviceUpdateStatusSnapshotDto(
                        DeviceId,
                        RolloutId,
                        PackageId,
                        UpdateComponentNames.AgentService,
                        "1.2.2",
                        "1.2.3",
                        UpdateStatusNames.Installed,
                        "installed",
                        DateTimeOffset.Parse("2026-05-14T12:45:00Z"))
                ])
        ]));
        var client = CreateClient(handler);

        var result = await client.GetRolloutStatusesAsync(BranchId, CancellationToken.None);

        var rollout = Assert.Single(result);
        Assert.Equal(RolloutId, rollout.UpdateRolloutId);
        Assert.Equal(UpdateStatusNames.Installed, rollout.DeviceStatuses.Single().Status);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/updates/rollouts", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    [Fact]
    public async Task RegisterPackageAsync_SendsBearerTokenAndPackageRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(CreatePackageDto()));
        var client = CreateClient(handler);
        var request = CreatePackageRequest();

        var result = await client.RegisterPackageAsync(BranchId, request, CancellationToken.None);

        Assert.Equal(PackageId, result.UpdatePackageId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/updates/packages", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
        var captured = JsonSerializer.Deserialize<CreateUpdatePackageRequest>(handler.LastBodyJson!, JsonOptions);
        Assert.NotNull(captured);
        Assert.Equal(request.OrganizationId, captured.OrganizationId);
        Assert.Equal(request.ArtifactUri, captured.ArtifactUri);
        Assert.Equal(request.Signature, captured.Signature);
    }

    [Fact]
    public async Task ChangePackageStateAsync_PostsStateChange()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(CreatePackageDto(UpdatePackageStateNames.Validated)));
        var client = CreateClient(handler);
        var request = new UpdatePackageStateChangeRequest(
            OrganizationId,
            UpdatePackageStateNames.Validated,
            "Signature checked.");

        var result = await client.ChangePackageStateAsync(BranchId, PackageId, request, CancellationToken.None);

        Assert.Equal(UpdatePackageStateNames.Validated, result.State);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/updates/packages/{PackageId:D}/state", handler.LastPathAndQuery);
        var captured = JsonSerializer.Deserialize<UpdatePackageStateChangeRequest>(handler.LastBodyJson!, JsonOptions);
        Assert.NotNull(captured);
        Assert.Equal(UpdatePackageStateNames.Validated, captured.State);
        Assert.Equal("Signature checked.", captured.Reason);
    }

    [Fact]
    public async Task CreateRolloutAsync_PostsRolloutRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(CreateRolloutDto()));
        var client = CreateClient(handler);
        var request = new CreateUpdateRolloutRequest(
            OrganizationId,
            PackageId,
            UpdateChannelNames.Beta,
            UpdateTargetKindNames.Branch,
            [],
            BatchPercent: 25,
            StartsAtUtc: DateTimeOffset.Parse("2026-05-14T13:00:00Z"),
            Reason: "Canary rollout.");

        var result = await client.CreateRolloutAsync(BranchId, request, CancellationToken.None);

        Assert.Equal(RolloutId, result.UpdateRolloutId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/updates/rollouts", handler.LastPathAndQuery);
        var captured = JsonSerializer.Deserialize<CreateUpdateRolloutRequest>(handler.LastBodyJson!, JsonOptions);
        Assert.NotNull(captured);
        Assert.Equal(25, captured.BatchPercent);
        Assert.Equal("Canary rollout.", captured.Reason);
    }

    [Fact]
    public async Task ChangeRolloutStateAsync_PostsStateChange()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(CreateRolloutDto(UpdateRolloutStateNames.Paused)));
        var client = CreateClient(handler);
        var request = new UpdateRolloutStateChangeRequest(
            OrganizationId,
            UpdateRolloutStateNames.Paused,
            "Pause while checking failures.");

        var result = await client.ChangeRolloutStateAsync(BranchId, RolloutId, request, CancellationToken.None);

        Assert.Equal(UpdateRolloutStateNames.Paused, result.State);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/updates/rollouts/{RolloutId:D}/state", handler.LastPathAndQuery);
        var captured = JsonSerializer.Deserialize<UpdateRolloutStateChangeRequest>(handler.LastBodyJson!, JsonOptions);
        Assert.NotNull(captured);
        Assert.Equal(UpdateRolloutStateNames.Paused, captured.State);
        Assert.Equal("Pause while checking failures.", captured.Reason);
    }

    private static HttpOperatorUpdateApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        };

        return new HttpOperatorUpdateApiClient(httpClient, new StaticOrganizationAdminTokenStore());
    }

    private static CreateUpdatePackageRequest CreatePackageRequest()
    {
        return new CreateUpdatePackageRequest(
            OrganizationId,
            UpdateComponentNames.AgentService,
            "1.2.3",
            UpdateChannelNames.Beta,
            "https://cdn.afk4.test/agent-service/beta/1.2.3/agent.zip",
            new string('a', 64),
            "signature",
            UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            SizeBytes: 4096,
            ReleaseNotes: "Beta Agent release.");
    }

    private static UpdatePackageDto CreatePackageDto(string state = UpdatePackageStateNames.Registered)
    {
        var request = CreatePackageRequest();
        return new UpdatePackageDto(
            PackageId,
            OrganizationId,
            BranchId,
            request.Component,
            request.Version,
            request.Channel,
            request.ArtifactUri,
            request.Sha256,
            request.Signature,
            request.SignatureAlgorithm,
            request.SizeBytes,
            state,
            request.ReleaseNotes,
            DateTimeOffset.Parse("2026-05-14T12:00:00Z"));
    }

    private static UpdateRolloutDto CreateRolloutDto(string state = UpdateRolloutStateNames.Active)
    {
        return new UpdateRolloutDto(
            RolloutId,
            OrganizationId,
            BranchId,
            PackageId,
            UpdateComponentNames.AgentService,
            "1.2.3",
            UpdateChannelNames.Beta,
            state,
            UpdateTargetKindNames.Branch,
            [],
            BatchPercent: 25,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-14T12:45:00Z"),
            StartsAtUtc: DateTimeOffset.Parse("2026-05-14T13:00:00Z"),
            CompletedAtUtc: null);
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

        public string? LastBodyJson { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;
            LastBodyJson = request.Content?.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();

            return Task.FromResult(responder(request));
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
