using System.IO;
using System.Text.Json;

namespace AFK4.OrganizationAdmin.App.Updates;

public sealed record OrganizationAdminShutdownAcknowledgement(
    Guid UpdateRolloutId,
    Guid UpdatePackageId,
    DateTimeOffset AcknowledgedAt);

public interface IOrganizationAdminShutdownAcknowledgementStore
{
    Task PersistAsync(OrganizationAdminShutdownAcknowledgement acknowledgement, CancellationToken cancellationToken);
}

public sealed class FileOrganizationAdminShutdownAcknowledgementStore(string path)
    : IOrganizationAdminShutdownAcknowledgementStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task PersistAsync(
        OrganizationAdminShutdownAcknowledgement acknowledgement,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("The shutdown acknowledgement path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(acknowledgement, JsonOptions),
                cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }
}
