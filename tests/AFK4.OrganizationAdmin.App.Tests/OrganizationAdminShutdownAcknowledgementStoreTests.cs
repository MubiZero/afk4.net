using System.Text.Json;
using AFK4.OrganizationAdmin.App.Updates;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminShutdownAcknowledgementStoreTests
{
    [Fact]
    public async Task PersistAsync_WritesReleaseBoundAcknowledgement()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"afk4-ack-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "acknowledgement.json");
        var acknowledgement = new OrganizationAdminShutdownAcknowledgement(
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        try
        {
            await new FileOrganizationAdminShutdownAcknowledgementStore(path)
                .PersistAsync(acknowledgement, CancellationToken.None);

            var saved = JsonSerializer.Deserialize<OrganizationAdminShutdownAcknowledgement>(
                await File.ReadAllTextAsync(path),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Assert.Equal(acknowledgement, saved);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }
}
