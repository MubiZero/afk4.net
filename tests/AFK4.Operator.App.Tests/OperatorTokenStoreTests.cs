using System.Text;
using AFK4.Operator.App.Auth;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorTokenStoreTests
{
    [Fact]
    public async Task SaveAsync_ThenLoadAsync_ReturnsStoredTokenSnapshotWithoutPlaintextTokens()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afk4-token-{Guid.NewGuid():N}.bin");
        var store = new ProtectedDataOperatorTokenStore(path);
        var snapshot = new OperatorTokenSnapshot(
            StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            DisplayName: "Tech One",
            AccessToken: "access-token",
            AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"),
            RefreshToken: "refresh-token",
            RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-06-11T01:00:00Z"))
        {
            RoleNames = ["cashier_operator"]
        };

        try
        {
            await store.SaveAsync(snapshot, CancellationToken.None);

            var loaded = await store.LoadAsync(CancellationToken.None);
            var protectedBytes = await File.ReadAllBytesAsync(path, CancellationToken.None);

            Assert.NotNull(loaded);
            Assert.Equal(snapshot.StaffUserId, loaded.StaffUserId);
            Assert.Equal(snapshot.OrganizationId, loaded.OrganizationId);
            Assert.Equal(snapshot.DisplayName, loaded.DisplayName);
            Assert.Equal(snapshot.AccessToken, loaded.AccessToken);
            Assert.Equal(snapshot.AccessTokenExpiresAtUtc, loaded.AccessTokenExpiresAtUtc);
            Assert.Equal(snapshot.RefreshToken, loaded.RefreshToken);
            Assert.Equal(snapshot.RefreshTokenExpiresAtUtc, loaded.RefreshTokenExpiresAtUtc);
            Assert.Equal(snapshot.BranchIds, loaded.BranchIds);
            Assert.Equal(snapshot.Permissions, loaded.Permissions);
            Assert.Equal(snapshot.RoleNames, loaded.RoleNames);
            Assert.False(ContainsSequence(protectedBytes, Encoding.UTF8.GetBytes(snapshot.AccessToken)));
            Assert.False(ContainsSequence(protectedBytes, Encoding.UTF8.GetBytes(snapshot.RefreshToken)));

            await store.ClearAsync(CancellationToken.None);

            Assert.Null(await store.LoadAsync(CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
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
