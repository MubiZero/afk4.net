using System.Security.Cryptography;
using System.Text;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Shell;

/// <summary>
/// Картинки экрана игрока на диске ПК — обложки игр и витрина. Имя файла несёт отпечаток адреса:
/// сменилась картинка на сервере — новый файл, и WebView2 не покажет старую из своего кэша.
/// Качается только https и только картинка не больше заданного размера.
/// </summary>
public static class CachedImages
{
    private static readonly string[] Extensions = [".webp", ".png", ".jpg", ".jpeg"];

    public static Uri? Parse(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps ? uri : null;

    public static string Extension(Uri uri) =>
        Extensions.FirstOrDefault(candidate => uri.AbsolutePath.EndsWith(candidate, StringComparison.OrdinalIgnoreCase)) ?? ".jpg";

    public static string Fingerprint(Uri uri) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri)))[..12];

    /// <summary>Адрес файла для страницы: хост отдаёт папку витрины отдельным виртуальным хостом.</summary>
    public static string PageUri(string subfolder, string fileName) =>
        $"https://{ShellShowcaseAssets.VirtualHost}/{subfolder}/{fileName}";

    public static async Task DownloadAsync(HttpClient client, Uri uri, string path, long maxBytes, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
        {
            throw new InvalidDataException($"{uri} is not an image.");
        }

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            throw new InvalidDataException($"{uri} is larger than {maxBytes} bytes.");
        }

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(temporary))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > maxBytes)
                    {
                        throw new InvalidDataException($"{uri} is larger than {maxBytes} bytes.");
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    /// <summary>Убирает из папки всё, чего нет в списке нужного: уехавшие картинки не копятся на диске.</summary>
    public static void Prune(string folder, IReadOnlySet<string> wanted)
    {
        foreach (var stale in Directory.EnumerateFiles(folder).Where(file => !wanted.Contains(Path.GetFileName(file))).ToList())
        {
            TryDelete(stale);
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
