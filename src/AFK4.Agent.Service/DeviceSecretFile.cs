using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace AFK4.Agent.Service;

/// <summary>
/// Сужение прав на файл, в котором лежит ключ этой машины.
///
/// За игровым ПК сидит гость под обычной учётной записью, и всё, что доступно ему на чтение,
/// считай опубликовано. Ключ устройства — это право говорить от имени этого ПК: им подписан
/// каждый запрос к платформе, включая аренду сессии. Установщик кладёт первый ключ в
/// bootstrap.json и сразу сужает на него права; сменённый ключ агент писал рядом обычным файлом
/// с унаследованными правами — то есть открытым для всех, кому доступна папка.
///
/// Способ тот же, что у мастера установки: icacls. Своей реализации через FileSecurity здесь нет
/// намеренно — она требует отдельного пакета, а результат тот же.
/// </summary>
public static class DeviceSecretFile
{
    /// <summary>
    /// Оставить доступ только системе и администраторам. Лучшее из возможного: если сузить права
    /// не удалось, ключ всё равно должен сохраниться — иначе машина потеряет связь с платформой.
    /// </summary>
    public static void RestrictAccess(string path, Action<Exception>? onFailure = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            RunIcacls(path);
        }
        catch (Exception exception)
        {
            onFailure?.Invoke(exception);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void RunIcacls(string path)
    {
        var icacls = Path.Combine(Environment.SystemDirectory, "icacls.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = File.Exists(icacls) ? icacls : "icacls.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("/inheritance:r");
        startInfo.ArgumentList.Add("/grant:r");
        startInfo.ArgumentList.Add("*S-1-5-18:F");      // NT AUTHORITY\SYSTEM
        startInfo.ArgumentList.Add("/grant:r");
        startInfo.ArgumentList.Add("*S-1-5-32-544:F");  // BUILTIN\Administrators

        // Тот, от чьего имени работает агент. В клубе это система, и запись просто повторяет
        // первую. Нужна она для другого: без права переписать собственный файл следующая смена
        // ключа упрётся в им же поставленные права и оставит машину со старым ключом.
        var currentUser = WindowsIdentity.GetCurrent().User;
        if (currentUser is not null)
        {
            startInfo.ArgumentList.Add("/grant:r");
            startInfo.ArgumentList.Add($"*{currentUser.Value}:F");
        }

        using var process = Process.Start(startInfo);
        process?.WaitForExit(5000);
    }
}
