using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Players;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorPlayerApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid PlayerAccountId = Guid.Parse("65b9b565-eb5c-4ff5-890c-85f3e12a0fc2");

    [Fact]
    public async Task SearchPlayersAsync_GetsBearerAuthenticatedBranchPlayers()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse<IReadOnlyList<PlayerSearchResultDto>>(
            [
                new PlayerSearchResultDto(
                    PlayerAccountId,
                    "Alex Player",
                    "+992000000001",
                    WalletBalanceMinorUnits: 12000,
                    DebtBalanceMinorUnits: 0,
                    ActivePackageCount: 1,
                    IsActive: true,
                    CreatedAtUtc: default,
                    LastActivityAtUtc: null,
                    ActivePackageName: null,
                    ActivePackageRemainingMinutes: 0)
            ]));
        var client = CreateClient(handler);

        var results = await client.SearchPlayersAsync(BranchId, "Alex P", 20, CancellationToken.None);

        Assert.Single(results);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/players?query=Alex%20P&limit=20", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    [Fact]
    public async Task CreatePlayerAsync_PostsCreatePlayerRequest()
    {
        var response = new PlayerAccountDto(
            PlayerAccountId,
            OrganizationId,
            BranchId,
            "Alex Player",
            "+992000000001",
            IsActive: true,
            DateTimeOffset.Parse("2026-05-14T09:00:00Z"));
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(response));
        var client = CreateClient(handler);
        var request = new CreatePlayerAccountRequest(
            OrganizationId,
            "Alex Player",
            "+992000000001",
            "player-create-001");

        var result = await client.CreatePlayerAsync(BranchId, request, CancellationToken.None);

        Assert.Equal(PlayerAccountId, result.PlayerAccountId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/players", handler.LastPathAndQuery);

        var body = DeserializeRequest<CreatePlayerAccountRequest>(handler.LastRequestBody);
        Assert.Equal("player-create-001", body.IdempotencyKey);
    }

    [Fact]
    public async Task TopUpWalletAsync_PostsMoneyCommandToPlayerWallet()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateWalletSummary(walletMinorUnits: 15000, debtMinorUnits: 0)));
        var client = CreateClient(handler);
        var request = new TopUpWalletRequest(
            OrganizationId,
            new MoneyDto("USD", 3000),
            "cash top-up",
            "wallet-top-up-001");

        var response = await client.TopUpWalletAsync(PlayerAccountId, request, CancellationToken.None);

        Assert.Equal(15000, response.WalletBalance.MinorUnits);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/players/{PlayerAccountId:D}/wallet/top-ups", handler.LastPathAndQuery);

        var body = DeserializeRequest<TopUpWalletRequest>(handler.LastRequestBody);
        Assert.Equal("wallet-top-up-001", body.IdempotencyKey);
        Assert.Equal(3000, body.Amount.MinorUnits);
    }

    [Fact]
    public async Task GetWalletSummaryAsync_GetsPlayerWalletSummary()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateWalletSummary(walletMinorUnits: 12000, debtMinorUnits: 5000)));
        var client = CreateClient(handler);

        var response = await client.GetWalletSummaryAsync(PlayerAccountId, CancellationToken.None);

        Assert.Equal(12000, response.WalletBalance.MinorUnits);
        Assert.Equal(5000, response.DebtBalance.MinorUnits);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/players/{PlayerAccountId:D}/wallet-summary", handler.LastPathAndQuery);
    }

    [Fact]
    public async Task PayDebtAsync_PostsDebtPaymentCommand()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateWalletSummary(walletMinorUnits: 12000, debtMinorUnits: 0)));
        var client = CreateClient(handler);
        var request = new PayDebtRequest(
            OrganizationId,
            new MoneyDto("USD", 5000),
            "debt cash",
            "debt-pay-001");

        var response = await client.PayDebtAsync(PlayerAccountId, request, CancellationToken.None);

        Assert.Equal(0, response.DebtBalance.MinorUnits);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/players/{PlayerAccountId:D}/debts/payments", handler.LastPathAndQuery);

        var body = DeserializeRequest<PayDebtRequest>(handler.LastRequestBody);
        Assert.Equal("debt-pay-001", body.IdempotencyKey);
        Assert.Equal(5000, body.Amount.MinorUnits);
    }

    [Fact]
    public async Task GetPlayerPackagesAsync_GetsPlayerPackages()
    {
        var playerPackageId = Guid.Parse("77777777-7777-4777-8777-777777777777");
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse<IReadOnlyList<PlayerPackageDto>>(
            [
                new PlayerPackageDto(
                    playerPackageId,
                    Guid.Parse("88888888-8888-4888-8888-888888888888"),
                    PlayerAccountId,
                    "Night pack",
                    new MoneyDto("USD", 10000),
                    IncludedSeconds: 3600,
                    BonusSeconds: 0,
                    RemainingIncludedSeconds: 1800,
                    RemainingBonusSeconds: 0,
                    PurchasedAtUtc: DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                    ExpiresAtUtc: null)
            ]));
        var client = CreateClient(handler);

        var packages = await client.GetPlayerPackagesAsync(PlayerAccountId, CancellationToken.None);

        Assert.Single(packages);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/players/{PlayerAccountId:D}/packages", handler.LastPathAndQuery);
    }

    private static HttpOperatorPlayerApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        return new HttpOperatorPlayerApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, new StaticOperatorTokenStore());
    }

    private static WalletSummaryDto CreateWalletSummary(long walletMinorUnits, long debtMinorUnits)
    {
        return new WalletSummaryDto(
            PlayerAccountId,
            new MoneyDto("USD", walletMinorUnits),
            new MoneyDto("USD", debtMinorUnits),
            []);
    }

    private static T DeserializeRequest<T>(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result;
    }

    private static HttpResponseMessage JsonContentResponse<T>(T body)
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

        public string? LastRequestBody { get; private set; }

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

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

    private sealed class StaticOperatorTokenStore : IOperatorTokenStore
    {
        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OperatorTokenSnapshot?>(new OperatorTokenSnapshot(
                Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                OrganizationId,
                "Cashier One",
                "staff-access-token",
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                "refresh-token",
                DateTimeOffset.Parse("2026-05-15T10:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
