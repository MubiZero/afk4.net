using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class NamedPipeOrganizationAdminUpdateCoordinatorClient(IOptions<AgentOptions> options)
    : IOrganizationAdminUpdateCoordinatorClient
{
    private const int MaxPayloadBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<OrganizationAdminCoordinationResult> QueryStateAsync(CancellationToken cancellationToken) =>
        SendAsync(new(options.Value.OrganizationAdminUpdateCoordinationSecret,
            LocalUpdateCoordinationOperations.QueryState, null, null), cancellationToken);

    public Task<OrganizationAdminCoordinationResult> RequestShutdownAsync(
        Guid updateRolloutId, Guid updatePackageId, CancellationToken cancellationToken) =>
        SendAsync(new(options.Value.OrganizationAdminUpdateCoordinationSecret,
            LocalUpdateCoordinationOperations.RequestShutdown, updateRolloutId, updatePackageId), cancellationToken);

    private async Task<OrganizationAdminCoordinationResult> SendAsync(
        LocalUpdateCoordinationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Secret))
            return new(LocalUpdateCoordinationStatuses.NotRunning, "Organization Admin coordination is not configured.");

        await using var pipe = new NamedPipeClientStream(".", options.Value.OrganizationAdminUpdateCoordinationPipeName,
            PipeDirection.InOut, PipeOptions.Asynchronous);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.OrganizationAdminUpdateCoordinationTimeoutMilliseconds);
        try
        {
            await pipe.ConnectAsync(timeout.Token);
            await WriteAsync(pipe, request, timeout.Token);
            var response = await ReadAsync(pipe, timeout.Token);
            return new(response.Status, response.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(LocalUpdateCoordinationStatuses.NotRunning, "Organization Admin did not answer before the timeout.");
        }
        catch (IOException)
        {
            return new(LocalUpdateCoordinationStatuses.NotRunning, "Organization Admin is not running.");
        }
    }

    internal static async Task WriteAsync<T>(Stream stream, T message, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (payload.Length > MaxPayloadBytes) throw new InvalidDataException("Coordination payload is too large.");
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    internal static async Task<LocalUpdateCoordinationResponse> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var prefix = new byte[4];
        await stream.ReadExactlyAsync(prefix, cancellationToken);
        var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
        if (length is <= 0 or > MaxPayloadBytes) throw new InvalidDataException("Coordination payload length is invalid.");
        var payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<LocalUpdateCoordinationResponse>(payload, JsonOptions)
            ?? throw new InvalidDataException("Coordination response is empty.");
    }
}
