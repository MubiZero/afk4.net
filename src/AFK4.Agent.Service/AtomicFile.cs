using System.Text;

namespace AFK4.Agent.Service;

/// <summary>
/// Запись файла состояния так, чтобы отключение света посреди записи не оставило обрезанный файл.
/// Данные пишутся во временный файл рядом, сбрасываются на диск (<c>Flush(true)</c>: без этого NTFS
/// может зафиксировать переименование раньше, чем сами байты), и только потом временный файл
/// переименованием встаёт на место старого. Читатель видит либо старую версию, либо новую целиком.
/// Копирование поверх (как было) этого не даёт: обрыв на середине оставлял половину JSON, и при
/// следующем запуске оплаченная аренда гостя выбрасывалась как испорченная.
/// </summary>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void Write(string path, Action<Stream> write, Action<string>? beforeReplace = null)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            beforeReplace?.Invoke(temporary);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    public static void WriteAllText(string path, string contents) =>
        Write(path, stream => stream.Write(Utf8NoBom.GetBytes(contents)));
}
