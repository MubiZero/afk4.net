using System.IO;
using System.Text.Json;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Operator.App.FloorMap;

/// <summary>
/// File-backed <see cref="IFloorMapCache"/> stored under the operator app-data directory. Mirrors the
/// atomic-write + corrupt-self-heal durability of the agent's <c>FileSessionLeaseStore</c>: each branch
/// gets its own file, writes go through a temp file, and a parse failure deletes the poisoned file and
/// degrades to "no cache" rather than crashing the workspace.
/// </summary>
public sealed class FileFloorMapCache : IFloorMapCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly object syncRoot = new();
    private readonly string stateDirectory;

    public FileFloorMapCache()
        : this(ResolveDefaultStateDirectory())
    {
    }

    public FileFloorMapCache(string stateDirectory)
    {
        this.stateDirectory = stateDirectory;
    }

    public void Save(FloorMapDto floorMap, DateTimeOffset cachedAtUtc)
    {
        var entry = new FloorMapCacheEntry(floorMap, cachedAtUtc);
        var path = ResolveCacheFilePath(floorMap.BranchId);

        lock (syncRoot)
        {
            Directory.CreateDirectory(stateDirectory);
            WriteAtomically(path, entry);
        }
    }

    public FloorMapCacheEntry? Load(Guid branchId)
    {
        var path = ResolveCacheFilePath(branchId);

        lock (syncRoot)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(path);
                var entry = JsonSerializer.Deserialize<FloorMapCacheEntry>(stream, JsonOptions);
                if (entry?.FloorMap is null)
                {
                    TryDelete(path);
                    return null;
                }

                return entry;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                TryDelete(path);
                return null;
            }
        }
    }

    private string ResolveCacheFilePath(Guid branchId)
    {
        return Path.Combine(stateDirectory, $"floor-map-cache-{branchId:N}.json");
    }

    private static string ResolveDefaultStateDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "AFK4", "Operator", "cache");
    }

    private static void WriteAtomically(string path, FloorMapCacheEntry entry)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, entry, JsonOptions);
            }

            File.Copy(tempPath, path, overwrite: true);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
