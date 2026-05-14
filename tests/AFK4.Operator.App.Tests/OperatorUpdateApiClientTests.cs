using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Updates;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorUpdateApiClientTests
{
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
        Assert.Equal($"/api/branches/{BranchId:D}/updates/rollouts", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    private static HttpOperatorUpdateApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        };

        return new HttpOperatorUpdateApiClient(httpClient, new StaticOperatorTokenStore());
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;

            return Task.FromResult(responder(request));
        }
    }

    private sealed class StaticOperatorTokenStore : IOperatorTokenStore
    {
        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OperatorTokenSnapshot?>(new OperatorTokenSnapshot(
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
