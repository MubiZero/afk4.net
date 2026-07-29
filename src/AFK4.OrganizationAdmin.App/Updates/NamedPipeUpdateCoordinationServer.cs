using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.OrganizationAdmin.App.Updates;

public sealed class NamedPipeUpdateCoordinationServer(
    string pipeName,
    string secret,
    OrganizationAdminActivityState activityState,
    IOrganizationAdminShutdownAcknowledgementStore acknowledgementStore,
    Action shutdown) : IDisposable
{
    private const int MaxPayloadBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource lifetime = new();
    private Task? serverTask;

    public void Start() => serverTask ??= Task.Run(() => RunAsync(lifetime.Token));

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var pipe = CreatePipe();
                await pipe.WaitForConnectionAsync(cancellationToken);
                var request = await ReadAsync(pipe, cancellationToken);
                var response = await HandleAsync(request, cancellationToken);
                await WriteAsync(pipe, response, cancellationToken);
                if (response.Status == LocalUpdateCoordinationStatuses.ShutdownAcknowledged) shutdown();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            catch (IOException) { }
            catch (JsonException) { }
            catch (InvalidDataException) { }
        }
    }

    private NamedPipeServerStream CreatePipe()
    {
        if (!OperatingSystem.IsWindows())
            return new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var currentUser = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("The current Windows user has no security identifier.");
        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var security = new PipeSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new PipeAccessRule(currentUser, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(localSystem, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 0, 0, security);
    }

    private async Task<LocalUpdateCoordinationResponse> HandleAsync(
        LocalUpdateCoordinationRequest request,
        CancellationToken cancellationToken)
    {
        if (!SecretEquals(secret, request.Secret))
            return new(LocalUpdateCoordinationStatuses.Rejected, "Coordination authentication failed.");
        if (request.Operation == LocalUpdateCoordinationOperations.QueryState)
            return new(activityState.QueryStatus(), "Organization Admin state was read.");
        if (request.Operation != LocalUpdateCoordinationOperations.RequestShutdown ||
            request.UpdateRolloutId is null || request.UpdateRolloutId == Guid.Empty ||
            request.UpdatePackageId is null || request.UpdatePackageId == Guid.Empty)
            return new(LocalUpdateCoordinationStatuses.Rejected, "Shutdown request is invalid.");
        if (activityState.HasCriticalCommandInFlight)
            return new(LocalUpdateCoordinationStatuses.CriticalCommandActive, "A critical command is still running.");
        try
        {
            await acknowledgementStore.PersistAsync(
                new(request.UpdateRolloutId.Value, request.UpdatePackageId.Value, DateTimeOffset.UtcNow),
                cancellationToken);
        }
        catch (IOException)
        {
            return new(LocalUpdateCoordinationStatuses.Rejected, "Organization Admin could not persist shutdown state.");
        }
        catch (UnauthorizedAccessException)
        {
            return new(LocalUpdateCoordinationStatuses.Rejected, "Organization Admin could not persist shutdown state.");
        }
        return new(LocalUpdateCoordinationStatuses.ShutdownAcknowledged, "Organization Admin persisted and accepted graceful shutdown.");
    }

    private static bool SecretEquals(string expected, string actual)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static async Task<LocalUpdateCoordinationRequest> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var prefix = new byte[4]; await stream.ReadExactlyAsync(prefix, cancellationToken);
        var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
        if (length is <= 0 or > MaxPayloadBytes) throw new InvalidDataException("Invalid payload length.");
        var payload = new byte[length]; await stream.ReadExactlyAsync(payload, cancellationToken);
        return JsonSerializer.Deserialize<LocalUpdateCoordinationRequest>(payload, JsonOptions)
            ?? throw new InvalidDataException("Empty request.");
    }

    private static async Task WriteAsync(Stream stream, LocalUpdateCoordinationResponse response, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
        var prefix = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken); await stream.WriteAsync(payload, cancellationToken); await stream.FlushAsync(cancellationToken);
    }

    public void Dispose()
    {
        lifetime.Cancel();
        try { serverTask?.Wait(TimeSpan.FromSeconds(1)); } catch (AggregateException) { }
        lifetime.Dispose();
    }
}
