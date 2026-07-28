using System.IO;
using System.Text.Json;

namespace AFK4.OrganizationAdmin.Web;

// Reads/writes the remembered window geometry as JSON next to the operator's local app data.
// Both load and save swallow IO/parse failures: a missing, locked or corrupt placement file must
// never block startup or shutdown — the window just falls back to its default size.
public static class OrganizationAdminWindowPlacementStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Resolve() => Resolve(Environment.GetFolderPath);

    public static string Resolve(Func<Environment.SpecialFolder, string> getFolderPath)
    {
        ArgumentNullException.ThrowIfNull(getFolderPath);

        var localAppData = getFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("Local application data folder is not available.");
        }

        return Path.Combine(localAppData, "AFK4", "OrganizationAdmin", "window-placement.json");
    }

    public static OrganizationAdminWindowPlacement? Load()
    {
        try
        {
            var path = Resolve();
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<OrganizationAdminWindowPlacement>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public static void Save(OrganizationAdminWindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        try
        {
            var path = Resolve();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(placement, JsonOptions));
        }
        catch
        {
            // Best-effort: failing to persist the window shape is not worth interrupting the user.
        }
    }
}
