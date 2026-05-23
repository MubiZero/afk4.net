using System.Text;
using AFK4.Operator.App.Connection;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorConnectionStoreTests
{
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsSnapshotAndKeepsPlaintextOffDisk()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afk4-connection-{Guid.NewGuid():N}.bin");
        var store = new ProtectedDataOperatorConnectionStore(path);
        var snapshot = new OperatorConnectionSnapshot(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            OrganizationSlug: "afk4-dushanbe",
            OrganizationName: "AFK4 Dushanbe",
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            BranchSlug: "central",
            BranchName: "Central",
            BranchCity: "Dushanbe",
            StoredAtUtc: DateTimeOffset.Parse("2026-05-23T10:00:00Z"));

        try
        {
            await store.SaveAsync(snapshot, CancellationToken.None);

            var loaded = await store.LoadAsync(CancellationToken.None);
            var protectedBytes = await File.ReadAllBytesAsync(path, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(snapshot.OrganizationId, loaded.OrganizationId);
            Assert.Equal(snapshot.OrganizationSlug, loaded.OrganizationSlug);
            Assert.Equal(snapshot.OrganizationName, loaded.OrganizationName);
            Assert.Equal(snapshot.BranchId, loaded.BranchId);
            Assert.Equal(snapshot.BranchSlug, loaded.BranchSlug);
            Assert.Equal(snapshot.BranchName, loaded.BranchName);
            Assert.Equal(snapshot.BranchCity, loaded.BranchCity);
            Assert.Equal(snapshot.StoredAtUtc, loaded.StoredAtUtc);
            Assert.False(ContainsSequence(protectedBytes, Encoding.UTF8.GetBytes(snapshot.OrganizationName)));
            Assert.False(ContainsSequence(protectedBytes, Encoding.UTF8.GetBytes(snapshot.BranchSlug)));

            await store.ClearAsync(CancellationToken.None);

            Assert.Null(await store.LoadAsync(CancellationToken.None));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afk4-connection-missing-{Guid.NewGuid():N}.bin");
        var store = new ProtectedDataOperatorConnectionStore(path);

        var loaded = await store.LoadAsync(CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ClearAsync_IsIdempotent_WhenFileDoesNotExist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afk4-connection-missing-{Guid.NewGuid():N}.bin");
        var store = new ProtectedDataOperatorConnectionStore(path);

        await store.ClearAsync(CancellationToken.None);

        Assert.False(File.Exists(path));
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }

        return false;
    }
}
