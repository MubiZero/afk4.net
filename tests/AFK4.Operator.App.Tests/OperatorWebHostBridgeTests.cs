using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Web;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorWebHostBridgeTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");

    [Fact]
    public async Task HandleAsync_SignIn_ReturnsSanitizedAuthSession()
    {
        var authClient = new RecordingOperatorAuthApiClient();
        var bridge = new OperatorWebHostBridge(authClient, new RecordingOperatorTokenStore());

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new
            {
                type = "auth:signIn",
                requestId = "request-1",
                payload = new
                {
                    organizationId = OrganizationId.ToString("D"),
                    userName = " cashier ",
                    password = "password"
                }
            }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        Assert.DoesNotContain("refresh-token", responseJson, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal("host:response", root.GetProperty("type").GetString());
        Assert.Equal("request-1", root.GetProperty("requestId").GetString());
        Assert.Equal("access-token", payload.GetProperty("accessToken").GetString());
        Assert.Equal("Cashier One", payload.GetProperty("displayName").GetString());
        Assert.Equal(BranchId.ToString("D"), payload.GetProperty("activeBranchId").GetGuid().ToString("D"));
        Assert.Equal(OrganizationId, authClient.LastOrganizationId);
        Assert.Equal("cashier", authClient.LastUserName);
    }

    [Fact]
    public async Task HandleAsync_LoadToken_ReturnsStoredProtectedSessionContext()
    {
        var tokenStore = new RecordingOperatorTokenStore
        {
            Snapshot = new OperatorTokenSnapshot(
                StaffUserId,
                OrganizationId,
                "Cashier One",
                "stored-access-token",
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                "stored-refresh-token",
                DateTimeOffset.Parse("2026-05-15T10:00:00Z"))
            {
                BranchIds = [BranchId],
                Permissions = [StaffPermissionNames.ViewFloorMap]
            }
        };
        var bridge = new OperatorWebHostBridge(new RecordingOperatorAuthApiClient(), tokenStore);

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new { type = "auth:loadToken", requestId = "request-2" }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        Assert.DoesNotContain("stored-refresh-token", responseJson, StringComparison.Ordinal);
        using var document = JsonDocument.Parse(responseJson);
        var payload = document.RootElement.GetProperty("payload");

        Assert.Equal("stored-access-token", payload.GetProperty("accessToken").GetString());
        Assert.Equal(BranchId.ToString("D"), payload.GetProperty("activeBranchId").GetGuid().ToString("D"));
        Assert.Equal(StaffPermissionNames.ViewFloorMap, payload.GetProperty("permissions")[0].GetString());
    }

    [Fact]
    public async Task HandleAsync_LoadTokenWithoutStoredSnapshot_ReturnsExplicitNullPayload()
    {
        var bridge = new OperatorWebHostBridge(
            new RecordingOperatorAuthApiClient(),
            new RecordingOperatorTokenStore());

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new { type = "auth:loadToken", requestId = "request-empty" }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.True(root.TryGetProperty("payload", out var payload));
        Assert.Equal(JsonValueKind.Null, payload.ValueKind);
    }

    [Fact]
    public async Task HandleAsync_SignOut_ClearsProtectedTokenStore()
    {
        var tokenStore = new RecordingOperatorTokenStore
        {
            Snapshot = new OperatorTokenSnapshot(
                StaffUserId,
                OrganizationId,
                "Cashier One",
                "stored-access-token",
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                "stored-refresh-token",
                DateTimeOffset.Parse("2026-05-15T10:00:00Z"))
        };
        var bridge = new OperatorWebHostBridge(new RecordingOperatorAuthApiClient(), tokenStore);

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new { type = "auth:signOut", requestId = "request-3" }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        Assert.Null(tokenStore.Snapshot);
        using var document = JsonDocument.Parse(responseJson);

        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(document.RootElement.GetProperty("payload").GetProperty("signedOut").GetBoolean());
    }

    private sealed class RecordingOperatorAuthApiClient : IOperatorAuthApiClient
    {
        public Guid LastOrganizationId { get; private set; }

        public string LastUserName { get; private set; } = string.Empty;

        public Task<StaffSignInResponse> SignInAsync(
            Guid organizationId,
            string userName,
            string password,
            CancellationToken cancellationToken)
        {
            LastOrganizationId = organizationId;
            LastUserName = userName;
            return Task.FromResult(CreateResponse("access-token", "refresh-token"));
        }

        public Task<StaffSignInResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResponse("rotated-access-token", "rotated-refresh-token"));
        }

        private static StaffSignInResponse CreateResponse(string accessToken, string refreshToken)
        {
            return new StaffSignInResponse(
                StaffUserId,
                OrganizationId,
                "Cashier One",
                accessToken,
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                refreshToken,
                DateTimeOffset.Parse("2026-05-15T10:00:00Z"),
                [BranchId],
                [StaffPermissionNames.ViewFloorMap]);
        }
    }

    private sealed class RecordingOperatorTokenStore : IOperatorTokenStore
    {
        public OperatorTokenSnapshot? Snapshot { get; set; }

        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Snapshot);
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }
    }
}
