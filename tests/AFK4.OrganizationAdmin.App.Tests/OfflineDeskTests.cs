using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AFK4.OrganizationAdmin.App.Connection;
using AFK4.OrganizationAdmin.App.Offline;
using AFK4.OrganizationAdmin.Web;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.OrganizationAdmin.App.Tests;

/// <summary>
/// Офлайн у стойки со стороны ПК администратора (docs/roadmap/offline-front-desk.md): ключ этого ПК,
/// подписанная аренда для ПК в зале и журнал кассы на диске.
/// </summary>
public sealed class OfflineDeskTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("afk4-offline-desk-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    [Fact]
    public void TheDeskKey_IsCreatedOnce_AndSurvivesARestart()
    {
        var path = Path.Combine(directory, "key.bin");
        using var first = OfflineDeskKey.LoadOrCreate(path);
        using var again = OfflineDeskKey.LoadOrCreate(path);

        Assert.StartsWith("desk-", first.KeyId, StringComparison.Ordinal);
        Assert.Equal(first.KeyId, again.KeyId);
        Assert.Equal(first.PublicKeyPem, again.PublicKeyPem);
    }

    [Fact]
    public async Task ALease_GoesToThePc_SignedByTheDeskKey()
    {
        using var key = OfflineDeskKey.LoadOrCreate(Path.Combine(directory, "key.bin"));
        var handler = new RecordingHandler();
        var desk = new OfflineDesk(() => key, new HttpClient(handler));

        var response = await desk.SendLeaseAsync("192.168.1.23", UnsignedLease(), CancellationToken.None);

        Assert.True(response.Accepted);
        Assert.Equal(new Uri("http://192.168.1.23:47615/afk4/offline/lease"), handler.LastUri);
        var sent = handler.LastLease!;
        Assert.Equal(OfflineLeaseSigning.Algorithm(key.KeyId), sent.SignatureAlgorithm);
        using var verifier = ECDsa.Create();
        verifier.ImportFromPem(key.PublicKeyPem);
        Assert.True(verifier.VerifyData(
            Encoding.UTF8.GetBytes(SessionLeasePayloadCanonicalizer.CreatePayload(sent with { Signature = string.Empty })),
            Convert.FromBase64String(sent.Signature),
            HashAlgorithmName.SHA256));
    }

    // Страница не должна суметь отправить подписанную аренду куда угодно — только ПК в сети клуба.
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("127.0.0.1")]
    [InlineData("api.afk4.net")]
    public async Task ALease_NeverLeavesTheClubNetwork(string address)
    {
        using var key = OfflineDeskKey.LoadOrCreate(Path.Combine(directory, "key.bin"));
        var desk = new OfflineDesk(() => key, new HttpClient(new RecordingHandler()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => desk.SendLeaseAsync(address, UnsignedLease(), CancellationToken.None));
    }

    [Fact]
    public void TheJournal_KeepsRecordsInOrder_DropsAcknowledgedOnes_AndSurvivesATornLine()
    {
        var path = Path.Combine(directory, "journal.jsonl");
        var journal = new OfflineJournal(path);
        journal.Append(Json("""{"id":"a","kind":"session_started"}"""));
        journal.Append(Json("""{"id":"b","kind":"session_extended"}"""));
        // Свет погас посреди записи: последняя строка оборвана.
        File.AppendAllText(path, """{"id":"c","kind":"sess""");

        Assert.Equal(["a", "b"], new OfflineJournal(path).Read().Select(record => record.GetProperty("id").GetString()));
        Assert.Equal(1, journal.Acknowledge(new HashSet<string> { "a" }));
        Assert.Equal("b", Assert.Single(journal.Read()).GetProperty("id").GetString());
        Assert.Throws<InvalidOperationException>(() => journal.Append(Json("""{"kind":"no id"}""")));
    }

    [Fact]
    public async Task TheBridge_HandsOutThePublicKey_AndKeepsTheJournal()
    {
        using var key = OfflineDeskKey.LoadOrCreate(Path.Combine(directory, "key.bin"));
        var bridge = new OrganizationAdminWebHostBridge(
            new NoConnectionStore(),
            offlineDesk: new OfflineDesk(() => key, new HttpClient(new RecordingHandler())),
            offlineJournal: new OfflineJournal(Path.Combine(directory, "journal.jsonl")));

        var publicKey = await SendAsync(bridge, new { type = "offline:key", requestId = "1" });
        Assert.Equal(key.KeyId, publicKey.GetProperty("keyId").GetString());
        Assert.DoesNotContain("PRIVATE", publicKey.GetRawText(), StringComparison.Ordinal);

        await SendAsync(bridge, new { type = "offline:journalAppend", requestId = "2", payload = new { record = new { id = "r1", kind = "session_started" } } });
        var read = await SendAsync(bridge, new { type = "offline:journalRead", requestId = "3" });
        Assert.Equal("r1", read.GetProperty("records")[0].GetProperty("id").GetString());
        var ack = await SendAsync(bridge, new { type = "offline:journalAck", requestId = "4", payload = new { ids = new[] { "r1" } } });
        Assert.Equal(0, ack.GetProperty("remaining").GetInt32());
    }

    private static async Task<JsonElement> SendAsync(OrganizationAdminWebHostBridge bridge, object message)
    {
        var response = await bridge.HandleAsync(JsonSerializer.Serialize(message), CancellationToken.None);
        using var document = JsonDocument.Parse(response!);
        Assert.True(document.RootElement.GetProperty("ok").GetBoolean(), response);
        return document.RootElement.GetProperty("payload").Clone();
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private static SessionLeaseDto UnsignedLease() => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Active", 1,
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(1), string.Empty, string.Empty);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastUri { get; private set; }

        public SessionLeaseDto? LastLease { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUri = request.RequestUri;
            LastLease = await request.Content!.ReadFromJsonAsync<SessionLeaseDto>(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new OfflineSeatResponse(true, null, false, LastLease!.SessionId, LastLease.ExpiresAtUtc))
            };
        }
    }

    private sealed class NoConnectionStore : IOrganizationAdminConnectionStore
    {
        public Task SaveAsync(OrganizationAdminConnectionSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<OrganizationAdminConnectionSnapshot?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<OrganizationAdminConnectionSnapshot?>(null);

        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
