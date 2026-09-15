using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.OrganizationAdmin.App.Tests;

/// <summary>
/// Ожидание здесь строится на событиях, а не на засыпании.
///
/// Раньше ответ сервера ждали через <c>Task.Delay(25)</c>, а счётчик выключений инкрементировался
/// на чужом потоке без синхронизации: на загруженном раннере двадцать пять миллисекунд — не срок,
/// и проверка читала счётчик до того, как его успели увеличить.
/// </summary>
public sealed class NamedPipeUpdateCoordinationServerTests
{
    [Fact]
    public async Task BadSecret_IsRejectedWithoutShutdown()
    {
        var shutdowns = new ShutdownCounter(); var pipeName = $"afk4-admin-server-{Guid.NewGuid():N}";
        using var server = new NamedPipeUpdateCoordinationServer(pipeName, "correct-secret", new(), new RecordingStore(), shutdowns.Record);
        server.Start();

        var response = await SendAsync(pipeName, new("wrong-secret", LocalUpdateCoordinationOperations.RequestShutdown, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(LocalUpdateCoordinationStatuses.Rejected, response.Status);
        Assert.Equal(0, shutdowns.Count);
    }

    [Fact]
    public async Task CriticalCommand_DefersOtherwiseAcknowledgesBoundShutdown()
    {
        var shutdowns = new ShutdownCounter(); var pipeName = $"afk4-admin-server-{Guid.NewGuid():N}"; var state = new OrganizationAdminActivityState();
        var store = new RecordingStore();
        using var server = new NamedPipeUpdateCoordinationServer(pipeName, "secret", state, store, shutdowns.Record); server.Start();
        using var critical = state.BeginCriticalCommand();
        var request = new LocalUpdateCoordinationRequest("secret", LocalUpdateCoordinationOperations.RequestShutdown, Guid.NewGuid(), Guid.NewGuid());

        var busy = await SendAsync(pipeName, request);
        critical.Dispose();
        var accepted = await SendAsync(pipeName, request);
        // Выключение вызывается после ответа, на серверном потоке — его ждут, а не засыпают в надежде.
        await shutdowns.WaitForFirstAsync();

        Assert.Equal(LocalUpdateCoordinationStatuses.CriticalCommandActive, busy.Status);
        Assert.Equal(LocalUpdateCoordinationStatuses.ShutdownAcknowledged, accepted.Status);
        Assert.Equal(request.UpdateRolloutId, store.Last?.UpdateRolloutId);
        Assert.Equal(request.UpdatePackageId, store.Last?.UpdatePackageId);
        Assert.Equal(1, shutdowns.Count);
    }

    [Fact]
    public async Task QueryState_WhenIdle_ReturnsIdleWithoutPersistingOrShutdown()
    {
        var shutdowns = new ShutdownCounter(); var store = new RecordingStore(); var pipeName = $"afk4-admin-server-{Guid.NewGuid():N}";
        using var server = new NamedPipeUpdateCoordinationServer(pipeName, "secret", new(), store, shutdowns.Record); server.Start();

        var response = await SendAsync(pipeName, new("secret", LocalUpdateCoordinationOperations.QueryState, null, null));

        Assert.Equal(LocalUpdateCoordinationStatuses.Idle, response.Status);
        Assert.Null(store.Last);
        Assert.Equal(0, shutdowns.Count);
    }

    [Fact]
    public async Task ShutdownWithMissingReleaseIdentity_IsRejected()
    {
        var shutdowns = new ShutdownCounter(); var store = new RecordingStore(); var pipeName = $"afk4-admin-server-{Guid.NewGuid():N}";
        using var server = new NamedPipeUpdateCoordinationServer(pipeName, "secret", new(), store, shutdowns.Record); server.Start();

        var response = await SendAsync(pipeName, new("secret", LocalUpdateCoordinationOperations.RequestShutdown, Guid.Empty, Guid.NewGuid()));

        Assert.Equal(LocalUpdateCoordinationStatuses.Rejected, response.Status);
        Assert.Null(store.Last);
        Assert.Equal(0, shutdowns.Count);
    }

    [Fact]
    public async Task Shutdown_WhenAcknowledgementCannotBePersisted_IsRejectedWithoutShutdown()
    {
        var shutdowns = new ShutdownCounter(); var pipeName = $"afk4-admin-server-{Guid.NewGuid():N}";
        using var server = new NamedPipeUpdateCoordinationServer(pipeName, "secret", new(), new FailingStore(), shutdowns.Record); server.Start();

        var response = await SendAsync(pipeName, new("secret", LocalUpdateCoordinationOperations.RequestShutdown, Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(LocalUpdateCoordinationStatuses.Rejected, response.Status);
        Assert.Equal(0, shutdowns.Count);
    }

    /// <summary>
    /// Счётчик выключений, который можно дождаться и безопасно прочитать: вызов приходит с
    /// серверного потока, и <c>shutdowns++</c> без синхронизации ничего не гарантировал читающему.
    /// </summary>
    private sealed class ShutdownCounter
    {
        private readonly TaskCompletionSource first = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int count;

        public int Count => Volatile.Read(ref count);

        public void Record()
        {
            Interlocked.Increment(ref count);
            first.TrySetResult();
        }

        public Task WaitForFirstAsync() => first.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class RecordingStore : IOrganizationAdminShutdownAcknowledgementStore
    {
        public OrganizationAdminShutdownAcknowledgement? Last { get; private set; }

        public Task PersistAsync(OrganizationAdminShutdownAcknowledgement acknowledgement, CancellationToken cancellationToken)
        {
            Last = acknowledgement;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingStore : IOrganizationAdminShutdownAcknowledgementStore
    {
        public Task PersistAsync(OrganizationAdminShutdownAcknowledgement acknowledgement, CancellationToken cancellationToken) =>
            throw new IOException("disk unavailable");
    }

    private static async Task<LocalUpdateCoordinationResponse> SendAsync(string pipeName, LocalUpdateCoordinationRequest request)
    {
        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await pipe.ConnectAsync(timeout.Token);
        var payload = JsonSerializer.SerializeToUtf8Bytes(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var prefix = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);
        await pipe.WriteAsync(prefix, timeout.Token); await pipe.WriteAsync(payload, timeout.Token); await pipe.FlushAsync(timeout.Token);
        await pipe.ReadExactlyAsync(prefix, timeout.Token); var responsePayload = new byte[BinaryPrimitives.ReadInt32BigEndian(prefix)];
        await pipe.ReadExactlyAsync(responsePayload, timeout.Token);
        return JsonSerializer.Deserialize<LocalUpdateCoordinationResponse>(responsePayload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }
}
