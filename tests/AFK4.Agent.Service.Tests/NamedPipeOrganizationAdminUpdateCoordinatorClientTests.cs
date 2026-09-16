using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Канал сервера-заглушки заводится на потоке теста, а не внутри фоновой задачи.
///
/// Раньше тест стартовал <c>ServeOnceAsync</c> и тут же звал клиента, а экземпляр канала создавался внутри
/// задачи. На загруженном Windows-раннере задача из пула могла не получить поток дольше, чем весь
/// пятисекундный таймаут клиента: клиент сдавался и закрывал канал, а сервер, наконец дождавшись
/// своей очереди, читал конец потока и падал с EndOfStreamException. Выглядело это как случайное мигание
/// разными методами класса.
///
/// Сигнала «сервер готов» здесь намеренно нет: на Unix привязка случается внутри WaitForConnectionAsync,
/// и любой такой сигнал был бы преждевременным. На Windows экземпляр создаёт сам конструктор, поэтому
/// вызова на потоке теста достаточно; на Unix клиент переспрашивает сам, как и раньше.
///
/// Таймаут клиента там, где проверяется не он сам, взят заведомо избыточным. Пяти секунд на
/// загруженном раннере не хватало: клиент сдавался посреди обмена и закрывал канал, сервер читал
/// конец потока — и протокольная проверка падала по бюджету времени, к которому не имеет отношения.
/// Свой таймаут проверяет <see cref="QueryState_WhenServerDoesNotAnswer_ReturnsNotRunningAfterTimeout"/>,
/// и там он остаётся коротким.
/// </summary>
public sealed class NamedPipeOrganizationAdminUpdateCoordinatorClientTests
{
    [Fact]
    public async Task QueryState_WritesAuthenticatedFramedRequestAndReadsResponse()
    {
        var pipeName = $"afk4-admin-update-{Guid.NewGuid():N}";
        var requestTask = ServeOnceAsync(CreateServerPipe(pipeName), new(LocalUpdateCoordinationStatuses.Idle, "ready"));
        var client = CreateClient(pipeName, "machine-secret", GenerousTimeoutMilliseconds);

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
        var requestTask = ServeOnceAsync(CreateServerPipe(pipeName), new(LocalUpdateCoordinationStatuses.ShutdownAcknowledged, "closing"));
        var client = CreateClient(pipeName, "machine-secret", GenerousTimeoutMilliseconds);
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
        var serverTask = AcceptWithoutAnswerAsync(CreateServerPipe(pipeName), TimeSpan.FromMilliseconds(250));
        var client = CreateClient(pipeName, "machine-secret", 25);

        var result = await client.QueryStateAsync(CancellationToken.None);
        await serverTask;

        Assert.Equal(LocalUpdateCoordinationStatuses.NotRunning, result.Status);
    }

    [Fact]
    public async Task QueryState_WhenCallerCancels_PropagatesCancellation()
    {
        // Здесь бюджет остаётся коротким намеренно: если отмена перестанет пробрасываться, проверка
        // должна упасть за секунды, а не висеть минуту.
        var client = CreateClient($"missing-{Guid.NewGuid():N}", "machine-secret", 5000);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.QueryStateAsync(cancellation.Token));
    }

    /// Бюджет времени для проверок, которые проверяют протокол, а не таймаут.
    private const int GenerousTimeoutMilliseconds = 60_000;

    private static NamedPipeOrganizationAdminUpdateCoordinatorClient CreateClient(string pipeName, string secret, int timeout) =>
        new(Options.Create(new AgentOptions { OrganizationAdminUpdateCoordinationPipeName = pipeName, OrganizationAdminUpdateCoordinationSecret = secret, OrganizationAdminUpdateCoordinationTimeoutMilliseconds = timeout }));

    private static NamedPipeServerStream CreateServerPipe(string pipeName) =>
        new(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

    private static async Task<LocalUpdateCoordinationRequest> ServeOnceAsync(
        NamedPipeServerStream server,
        LocalUpdateCoordinationResponse response)
    {
        await using var pipe = server;
        await pipe.WaitForConnectionAsync();
        var prefix = new byte[4]; await pipe.ReadExactlyAsync(prefix);
        var payload = new byte[BinaryPrimitives.ReadInt32BigEndian(prefix)]; await pipe.ReadExactlyAsync(payload);
        var request = JsonSerializer.Deserialize<LocalUpdateCoordinationRequest>(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var responsePayload = JsonSerializer.SerializeToUtf8Bytes(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        BinaryPrimitives.WriteInt32BigEndian(prefix, responsePayload.Length);
        await pipe.WriteAsync(prefix); await pipe.WriteAsync(responsePayload);
        return request;
    }

    private static async Task AcceptWithoutAnswerAsync(NamedPipeServerStream server, TimeSpan delay)
    {
        await using var pipe = server;
        await pipe.WaitForConnectionAsync();
        await Task.Delay(delay);
    }
}
