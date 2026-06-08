using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Connection;
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
        var bridge = new OperatorWebHostBridge(authClient, new RecordingOperatorTokenStore(), new RecordingOperatorConnectionStore());

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
        var bridge = new OperatorWebHostBridge(new RecordingOperatorAuthApiClient(), tokenStore, new RecordingOperatorConnectionStore());

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
            new RecordingOperatorTokenStore(),
            new RecordingOperatorConnectionStore());

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
        var bridge = new OperatorWebHostBridge(new RecordingOperatorAuthApiClient(), tokenStore, new RecordingOperatorConnectionStore());

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new { type = "auth:signOut", requestId = "request-3" }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        Assert.Null(tokenStore.Snapshot);
        using var document = JsonDocument.Parse(responseJson);

        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(document.RootElement.GetProperty("payload").GetProperty("signedOut").GetBoolean());
    }

    [Fact]
    public async Task HandleAsync_LoadConnection_ReturnsStoredSnapshot()
    {
        var connectionStore = new RecordingOperatorConnectionStore
        {
            Snapshot = new OperatorConnectionSnapshot(
                OrganizationId,
                "afk4-dushanbe",
                "AFK4 Dushanbe",
                BranchId,
                "central",
                "Central",
                "Dushanbe",
                DateTimeOffset.Parse("2026-05-23T10:00:00Z"))
        };
        var bridge = new OperatorWebHostBridge(
            new RecordingOperatorAuthApiClient(),
            new RecordingOperatorTokenStore(),
            connectionStore);

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new { type = "connection:loadConnection", requestId = "request-conn-1" }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Equal(OrganizationId, payload.GetProperty("organizationId").GetGuid());
        Assert.Equal("afk4-dushanbe", payload.GetProperty("organizationSlug").GetString());
        Assert.Equal("AFK4 Dushanbe", payload.GetProperty("organizationName").GetString());
        Assert.Equal(BranchId, payload.GetProperty("branchId").GetGuid());
        Assert.Equal("central", payload.GetProperty("branchSlug").GetString());
        Assert.Equal("Dushanbe", payload.GetProperty("branchCity").GetString());
        Assert.Equal(DateTimeOffset.Parse("2026-05-23T10:00:00Z"), payload.GetProperty("storedAtUtc").GetDateTimeOffset());
    }

    [Fact]
    public async Task HandleAsync_LoadConnectionWithoutStoredSnapshot_ReturnsExplicitNullPayload()
    {
        var bridge = new OperatorWebHostBridge(
            new RecordingOperatorAuthApiClient(),
            new RecordingOperatorTokenStore(),
            new RecordingOperatorConnectionStore());

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new { type = "connection:loadConnection", requestId = "request-conn-empty" }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;

        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.True(root.TryGetProperty("payload", out var payload));
        Assert.Equal(JsonValueKind.Null, payload.ValueKind);
    }

    [Fact]
    public async Task HandleAsync_SaveConnection_PersistsTrimmedSnapshotAndEchoesPayload()
    {
        var connectionStore = new RecordingOperatorConnectionStore();
        var bridge = new OperatorWebHostBridge(
            new RecordingOperatorAuthApiClient(),
            new RecordingOperatorTokenStore(),
            connectionStore);

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new
            {
                type = "connection:saveConnection",
                requestId = "request-conn-save",
                payload = new
                {
                    organizationId = OrganizationId.ToString("D"),
                    organizationSlug = "  afk4-dushanbe  ",
                    organizationName = "  AFK4 Dushanbe  ",
                    branchId = BranchId.ToString("D"),
                    branchSlug = "central",
                    branchName = "Central",
                    branchCity = "Dushanbe",
                    storedAtUtc = "2026-05-23T10:00:00Z"
                }
            }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        Assert.NotNull(connectionStore.Snapshot);
        Assert.Equal("afk4-dushanbe", connectionStore.Snapshot!.OrganizationSlug);
        Assert.Equal("AFK4 Dushanbe", connectionStore.Snapshot.OrganizationName);
        Assert.Equal(OrganizationId, connectionStore.Snapshot.OrganizationId);
        Assert.Equal(BranchId, connectionStore.Snapshot.BranchId);
        Assert.Equal(DateTimeOffset.Parse("2026-05-23T10:00:00Z"), connectionStore.Snapshot.StoredAtUtc);

        using var document = JsonDocument.Parse(responseJson);
        var payload = document.RootElement.GetProperty("payload");
        Assert.Equal("afk4-dushanbe", payload.GetProperty("organizationSlug").GetString());
        Assert.Equal(OrganizationId, payload.GetProperty("organizationId").GetGuid());
    }

    [Fact]
    public async Task HandleAsync_SaveConnectionRejectsInvalidGuid()
    {
        var connectionStore = new RecordingOperatorConnectionStore();
        var bridge = new OperatorWebHostBridge(
            new RecordingOperatorAuthApiClient(),
            new RecordingOperatorTokenStore(),
            connectionStore);

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new
            {
                type = "connection:saveConnection",
                requestId = "request-conn-bad",
                payload = new
                {
                    organizationId = "not-a-guid",
                    organizationSlug = "afk4-dushanbe",
                    organizationName = "AFK4 Dushanbe",
                    branchId = BranchId.ToString("D"),
                    branchSlug = "central",
                    branchName = "Central"
                }
            }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        Assert.Null(connectionStore.Snapshot);
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;

        Assert.False(root.GetProperty("ok").GetBoolean());
        Assert.Equal("connection_failed", root.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task HandleAsync_ClearConnection_DropsStoredSnapshot()
    {
        var connectionStore = new RecordingOperatorConnectionStore
        {
            Snapshot = new OperatorConnectionSnapshot(
                OrganizationId,
                "afk4-dushanbe",
                "AFK4 Dushanbe",
                BranchId,
                "central",
                "Central",
                "Dushanbe",
                DateTimeOffset.Parse("2026-05-23T10:00:00Z"))
        };
        var bridge = new OperatorWebHostBridge(
            new RecordingOperatorAuthApiClient(),
            new RecordingOperatorTokenStore(),
            connectionStore);

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new { type = "connection:clearConnection", requestId = "request-conn-clear" }),
            CancellationToken.None);

        Assert.NotNull(responseJson);
        Assert.Null(connectionStore.Snapshot);
        using var document = JsonDocument.Parse(responseJson);

        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(document.RootElement.GetProperty("payload").GetProperty("cleared").GetBoolean());
    }

    [Fact]
    public async Task HandleAsync_ForgotByEmail_CallsClientAndReturnsOk()
    {
        var authClient = new RecordingOperatorAuthApiClient();
        var bridge = new OperatorWebHostBridge(authClient, new RecordingOperatorTokenStore(), new RecordingOperatorConnectionStore());

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new
            {
                type = "auth:forgotByEmail",
                requestId = "request-1",
                payload = new { userNameOrEmail = " owner@demo.test " }
            }),
            CancellationToken.None);

        using var document = JsonDocument.Parse(responseJson!);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("owner@demo.test", authClient.LastForgotEmail);
    }

    [Fact]
    public async Task HandleAsync_ResetByPhone_ForwardsCodeAndRemainingAttempts()
    {
        var authClient = new RecordingOperatorAuthApiClient
        {
            ResetException = new OperatorAuthApiException("invalid_code", "bad code", 2)
        };
        var bridge = new OperatorWebHostBridge(authClient, new RecordingOperatorTokenStore(), new RecordingOperatorConnectionStore());

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new
            {
                type = "auth:resetByPhone",
                requestId = "request-2",
                payload = new { phoneNumber = "+992937380070", code = "000000", newPassword = "Passw0rd!New" }
            }),
            CancellationToken.None);

        using var document = JsonDocument.Parse(responseJson!);
        var root = document.RootElement;
        Assert.False(root.GetProperty("ok").GetBoolean());
        var error = root.GetProperty("error");
        Assert.Equal("invalid_code", error.GetProperty("code").GetString());
        Assert.Equal(2, error.GetProperty("remainingAttempts").GetInt32());
    }

    private sealed class RecordingOperatorAuthApiClient : IOperatorAuthApiClient
    {
        public Guid LastOrganizationId { get; private set; }

        public string LastUserName { get; private set; } = string.Empty;

        public string? LastForgotEmail { get; private set; }
        public (string UserNameOrEmail, string Code, string NewPassword)? LastResetEmail { get; private set; }
        public string? LastForgotPhone { get; private set; }
        public (string Phone, string Code, string NewPassword)? LastResetPhone { get; private set; }
        public OperatorAuthApiException? ResetException { get; set; }

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

        public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
        {
            LastForgotEmail = userNameOrEmail;
            return ResetException is null ? Task.CompletedTask : throw ResetException;
        }

        public Task ResetPasswordByEmailAsync(string userNameOrEmail, string code, string newPassword, CancellationToken cancellationToken)
        {
            LastResetEmail = (userNameOrEmail, code, newPassword);
            return ResetException is null ? Task.CompletedTask : throw ResetException;
        }

        public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
        {
            LastForgotPhone = phoneNumber;
            return ResetException is null ? Task.CompletedTask : throw ResetException;
        }

        public Task ResetPasswordByPhoneAsync(string phoneNumber, string code, string newPassword, CancellationToken cancellationToken)
        {
            LastResetPhone = (phoneNumber, code, newPassword);
            return ResetException is null ? Task.CompletedTask : throw ResetException;
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

    private sealed class RecordingOperatorConnectionStore : IOperatorConnectionStore
    {
        public OperatorConnectionSnapshot? Snapshot { get; set; }

        public Task SaveAsync(OperatorConnectionSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task<OperatorConnectionSnapshot?> LoadAsync(CancellationToken cancellationToken)
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
