using System.Text.Json;
using AFK4.OrganizationAdmin.App.Connection;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.OrganizationAdmin.Web;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminWebHostBridgeTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    [Fact]
    public async Task HandleAsync_LoadConnection_ReturnsStoredSnapshot()
    {
        var connectionStore = new RecordingOrganizationAdminConnectionStore
        {
            Snapshot = new OrganizationAdminConnectionSnapshot(
                OrganizationId,
                "afk4-dushanbe",
                "AFK4 Dushanbe",
                BranchId,
                "central",
                "Central",
                "Dushanbe",
                DateTimeOffset.Parse("2026-05-23T10:00:00Z"))
        };
        var bridge = new OrganizationAdminWebHostBridge(connectionStore);

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
        var bridge = new OrganizationAdminWebHostBridge(new RecordingOrganizationAdminConnectionStore());

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
        var connectionStore = new RecordingOrganizationAdminConnectionStore();
        var bridge = new OrganizationAdminWebHostBridge(connectionStore);

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
        var connectionStore = new RecordingOrganizationAdminConnectionStore();
        var bridge = new OrganizationAdminWebHostBridge(connectionStore);

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
        var connectionStore = new RecordingOrganizationAdminConnectionStore
        {
            Snapshot = new OrganizationAdminConnectionSnapshot(
                OrganizationId,
                "afk4-dushanbe",
                "AFK4 Dushanbe",
                BranchId,
                "central",
                "Central",
                "Dushanbe",
                DateTimeOffset.Parse("2026-05-23T10:00:00Z"))
        };
        var bridge = new OrganizationAdminWebHostBridge(connectionStore);

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
    public async Task HandleAsync_RejectsAuthPrefix_NowUnrecognizedByBridge()
    {
        // The bridge no longer accepts the "auth:" prefix — the web UI signs itself in over
        // HTTP directly. This guards against silently resurrecting the auth bridge.
        var bridge = new OrganizationAdminWebHostBridge(new RecordingOrganizationAdminConnectionStore());

        var responseJson = await bridge.HandleAsync(
            JsonSerializer.Serialize(new { type = "auth:signIn", requestId = "request-1" }),
            CancellationToken.None);

        Assert.Null(responseJson);
    }

    [Fact]
    public async Task HandleAsync_TracksConcurrentUpdateActivityByOperationId()
    {
        var state = new OrganizationAdminActivityState();
        var bridge = new OrganizationAdminWebHostBridge(new RecordingOrganizationAdminConnectionStore(), state);

        await bridge.HandleAsync(JsonSerializer.Serialize(new { type = "update-activity:start", operationId = "one" }), CancellationToken.None);
        await bridge.HandleAsync(JsonSerializer.Serialize(new { type = "update-activity:start", operationId = "one" }), CancellationToken.None);
        await bridge.HandleAsync(JsonSerializer.Serialize(new { type = "update-activity:start", operationId = "two" }), CancellationToken.None);
        await bridge.HandleAsync(JsonSerializer.Serialize(new { type = "update-activity:finish", operationId = "one" }), CancellationToken.None);

        Assert.True(state.HasCriticalCommandInFlight);
        await bridge.HandleAsync(JsonSerializer.Serialize(new { type = "update-activity:finish", operationId = "two" }), CancellationToken.None);
        Assert.False(state.HasCriticalCommandInFlight);
    }

    [Theory]
    [InlineData("{not-json")]
    [InlineData("{\"type\":\"update-activity:start\"}")]
    [InlineData("{\"type\":\"update-activity:start\",\"operationId\":\"\"}")]
    [InlineData("{\"type\":\"update-activity:unknown\",\"operationId\":\"one\"}")]
    public async Task HandleAsync_IgnoresMalformedUpdateActivity(string message)
    {
        var state = new OrganizationAdminActivityState();
        var bridge = new OrganizationAdminWebHostBridge(new RecordingOrganizationAdminConnectionStore(), state);

        var response = await bridge.HandleAsync(message, CancellationToken.None);

        Assert.Null(response);
        Assert.False(state.HasCriticalCommandInFlight);
    }

    [Fact]
    public async Task HandleAsync_RestartAndInstall_PersistsBoundAcknowledgementAndRequestsShutdown()
    {
        var rolloutId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var acknowledgements = new RecordingAcknowledgementStore();
        var shutdownCount = 0;
        var bridge = new OrganizationAdminWebHostBridge(
            new RecordingOrganizationAdminConnectionStore(),
            new OrganizationAdminActivityState(),
            acknowledgements,
            () => shutdownCount++);

        var responseJson = await bridge.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "update:restartAndInstall",
            requestId = "update-1",
            payload = new { updateRolloutId = rolloutId, updatePackageId = packageId }
        }), CancellationToken.None);

        Assert.NotNull(responseJson);
        using var document = JsonDocument.Parse(responseJson);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(document.RootElement.GetProperty("payload").GetProperty("accepted").GetBoolean());
        Assert.Equal(rolloutId, acknowledgements.Last?.UpdateRolloutId);
        Assert.Equal(packageId, acknowledgements.Last?.UpdatePackageId);
        Assert.Equal(1, shutdownCount);
    }

    [Theory]
    [InlineData("not-a-guid", "40381761-6043-482d-8eb8-3808f141b636")]
    [InlineData("40381761-6043-482d-8eb8-3808f141b636", "00000000-0000-0000-0000-000000000000")]
    public async Task HandleAsync_RestartAndInstall_RejectsInvalidBinding(string rolloutId, string packageId)
    {
        var acknowledgements = new RecordingAcknowledgementStore();
        var shutdownCount = 0;
        var bridge = new OrganizationAdminWebHostBridge(
            new RecordingOrganizationAdminConnectionStore(),
            new OrganizationAdminActivityState(),
            acknowledgements,
            () => shutdownCount++);

        var responseJson = await bridge.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "update:restartAndInstall",
            requestId = "update-invalid",
            payload = new { updateRolloutId = rolloutId, updatePackageId = packageId }
        }), CancellationToken.None);

        Assert.NotNull(responseJson);
        using var document = JsonDocument.Parse(responseJson);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("update_invalid", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Null(acknowledgements.Last);
        Assert.Equal(0, shutdownCount);
    }

    [Fact]
    public async Task HandleAsync_RestartAndInstall_RejectsWhileCriticalCommandIsActive()
    {
        var state = new OrganizationAdminActivityState();
        using var criticalCommand = state.BeginCriticalCommand();
        var acknowledgements = new RecordingAcknowledgementStore();
        var shutdownCount = 0;
        var bridge = new OrganizationAdminWebHostBridge(
            new RecordingOrganizationAdminConnectionStore(),
            state,
            acknowledgements,
            () => shutdownCount++);

        var responseJson = await bridge.HandleAsync(JsonSerializer.Serialize(new
        {
            type = "update:restartAndInstall",
            requestId = "update-busy",
            payload = new { updateRolloutId = Guid.NewGuid(), updatePackageId = Guid.NewGuid() }
        }), CancellationToken.None);

        Assert.NotNull(responseJson);
        using var document = JsonDocument.Parse(responseJson);
        Assert.False(document.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal("update_busy", document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Null(acknowledgements.Last);
        Assert.Equal(0, shutdownCount);
    }

    private sealed class RecordingOrganizationAdminConnectionStore : IOrganizationAdminConnectionStore
    {
        public OrganizationAdminConnectionSnapshot? Snapshot { get; set; }

        public Task SaveAsync(OrganizationAdminConnectionSnapshot snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task<OrganizationAdminConnectionSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Snapshot);
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAcknowledgementStore : IOrganizationAdminShutdownAcknowledgementStore
    {
        public OrganizationAdminShutdownAcknowledgement? Last { get; private set; }

        public Task PersistAsync(OrganizationAdminShutdownAcknowledgement acknowledgement, CancellationToken cancellationToken)
        {
            Last = acknowledgement;
            return Task.CompletedTask;
        }
    }
}
