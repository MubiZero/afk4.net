using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class NamedPipeOrganizationAdminUpdateCoordinatorClientTests
{
    [Fact]
    public async Task QueryState_WritesAuthenticatedFramedRequestAndReadsResponse()
    {
        var pipeName = $"afk4-admin-update-{Guid.NewGuid():N}";
        var requestTask = ServeOnceAsync(pipeName, new(LocalUpdateCoordinationStatuses.Idle, "ready"));
        var client = CreateClient(pipeName, "machine-secret", 5000);

        var result = await client.QueryStateAsync(CancellationToken.None);
        var request = await requestTask;

        Assert.Equal(LocalUpdateCoordinationStatuses.Idle, result.Status);
        Assert.Equal("machine-secret", request.Secret);
        Assert.Equal(LocalUpdateCoordinationOperations.QueryState, request.Operation);
    }

    [Fact]
    public async Task RequestShutdown_SendsReleaseIdentity()
    {
        var pipeName = $"afk4-admin-update-{Guid.NewGuid():N}";
        var requestTask = ServeOnceAsync(pipeName, new(LocalUpdateCoordinationStatuses.ShutdownAcknowledged, "closing"));
        var client = CreateClient(pipeName, "machine-secret", 5000);
        var rolloutId = Guid.NewGuid(); var packageId = Guid.NewGuid();

        var result = await client.RequestShutdownAsync(rolloutId, packageId, CancellationToken.None);
        var request = await requestTask;

        Assert.Equal(LocalUpdateCoordinationStatuses.ShutdownAcknowledged, result.Status);
        Assert.Equal(rolloutId, request.UpdateRolloutId);
        Assert.Equal(packageId, request.UpdatePackageId);
    }

    [Fact]
    public async Task QueryState_WhenPipeIsUnavailable_ReturnsNotRunning()
    {
        var client = CreateClient($"missing-{Guid.NewGuid():N}", "machine-secret", 25);

        var result = await client.QueryStateAsync(CancellationToken.None);

        Assert.Equal(LocalUpdateCoordinationStatuses.NotRunning, result.Status);
    }

    [Fact]
    public async Task QueryState_WhenServerDoesNotAnswer_ReturnsNotRunningAfterTimeout()
    {
        var pipeName = $"afk4-admin-update-{Guid.NewGuid():N}";
        var serverTask = AcceptWithoutAnswerAsync(pipeName, TimeSpan.FromMilliseconds(250));
        var client = CreateClient(pipeName, "machine-secret", 25);

        var result = await client.QueryStateAsync(CancellationToken.None);
        await serverTask;

        Assert.Equal(LocalUpdateCoordinationStatuses.NotRunning, result.Status);
    }

    [Fact]
    public async Task QueryState_WhenCallerCancels_PropagatesCancellation()
    {
        var client = CreateClient($"missing-{Guid.NewGuid():N}", "machine-secret", 5000);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.QueryStateAsync(cancellation.Token));
    }

    private static NamedPipeOrganizationAdminUpdateCoordinatorClient CreateClient(string pipeName, string secret, int timeout) =>
        new(Options.Create(new AgentOptions { OrganizationAdminUpdateCoordinationPipeName = pipeName, OrganizationAdminUpdateCoordinationSecret = secret, OrganizationAdminUpdateCoordinationTimeoutMilliseconds = timeout }));

    private static async Task<LocalUpdateCoordinationRequest> ServeOnceAsync(string pipeName, LocalUpdateCoordinationResponse response)
    {
        await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await pipe.WaitForConnectionAsync();
        var prefix = new byte[4]; await pipe.ReadExactlyAsync(prefix);
        var payload = new byte[BinaryPrimitives.ReadInt32BigEndian(prefix)]; await pipe.ReadExactlyAsync(payload);
        var request = JsonSerializer.Deserialize<LocalUpdateCoordinationRequest>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var responsePayload = JsonSerializer.SerializeToUtf8Bytes(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        BinaryPrimitives.WriteInt32BigEndian(prefix, responsePayload.Length);
        await pipe.WriteAsync(prefix); await pipe.WriteAsync(responsePayload); await pipe.FlushAsync();
        return request;
    }

    private static async Task AcceptWithoutAnswerAsync(string pipeName, TimeSpan delay)
    {
        await using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await pipe.WaitForConnectionAsync();
        await Task.Delay(delay);
    }
}
