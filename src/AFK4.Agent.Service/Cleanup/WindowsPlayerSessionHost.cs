using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using AFK4.Agent.Service.Shell;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace AFK4.Agent.Service.Cleanup;

/// <summary>Не Windows: убирать нечего и нечем.</summary>
public sealed class UnsupportedPlayerSessionHost : IPlayerSessionHost
{
    public bool IsSupported => false;

    public IReadOnlyList<string> ProtectedRoots => [];

    public PlayerSessionUser? ConsoleUser() => null;

    public IReadOnlyList<SessionProcess> Processes(PlayerSessionUser user) => [];

    public bool TryTerminate(int processId) => false;

    public bool IsRunning(int processId) => false;

    public string? SteamDirectory(PlayerSessionUser user) => null;

    public bool DeleteUserValue(PlayerSessionUser user, string subKey, string valueName) => false;

    public bool SetUserDword(PlayerSessionUser user, string subKey, string valueName, int value) => false;
}

/// <summary>
/// Уборка на Windows. Агент — служба LocalSystem в нулевой сессии: пользователя консоли он узнаёт
/// по его маркеру (<c>WTSQueryUserToken</c>), а процессы игрока отличает по сессии и владельцу —
/// чужие процессы той же сессии (службы, DWM) не его и не трогаются.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPlayerSessionHost(IOptions<AgentOptions> options) : IPlayerSessionHost
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int TokenUserClass = 1;

    public bool IsSupported => true;

    public IReadOnlyList<string> ProtectedRoots
    {
        get
        {
            var roots = new List<string>
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                // Сам AFK4: агент лежит в «AFK4\Agent Service», оболочка — рядом.
                Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)) ?? AppContext.BaseDirectory,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "EdgeWebView"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "EdgeWebView")
            };
            var shell = options.Value.PlayerShellExecutablePath;
            if (!string.IsNullOrWhiteSpace(shell) && Path.GetDirectoryName(shell) is { Length: > 0 } shellDirectory)
            {
                roots.Add(shellDirectory);
            }

            return roots.Where(root => !string.IsNullOrWhiteSpace(root)).ToList();
        }
    }

    public PlayerSessionUser? ConsoleUser()
    {
        var sessionId = NativeMethods.WTSGetActiveConsoleSessionId();
        if (sessionId == NativeMethods.NoActiveSession || !NativeMethods.WTSQueryUserToken(sessionId, out var token))
        {
            return null;
        }

        try
        {
            var sid = TokenUserSid(token);
            var profile = sid is null ? null : ProfileDirectory(sid);
            return sid is null || profile is null ? null : new PlayerSessionUser(checked((int)sessionId), sid, profile);
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }

    public IReadOnlyList<SessionProcess> Processes(PlayerSessionUser user)
    {
        var processes = new List<SessionProcess>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.SessionId != user.SessionId || process.Id == Environment.ProcessId)
                    {
                        continue;
                    }

                    var handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
                    if (handle == IntPtr.Zero)
                    {
                        continue;
                    }

                    try
                    {
                        if (!string.Equals(ProcessOwnerSid(handle), user.Sid, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var path = ImagePath(handle);
                        processes.Add(new SessionProcess(
                            process.Id,
                            path is null ? process.ProcessName + ".exe" : Path.GetFileName(path),
                            path,
                            StartTime(handle)));
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(handle);
                    }
                }
                catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
                {
                    // Процесс успел выйти, пока его перечисляли.
                }
            }
        }

        return processes;
    }

    public bool TryTerminate(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.Kill(entireProcessTree: false);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    public bool IsRunning(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or Win32Exception)
        {
            return false;
        }
    }

    public string? SteamDirectory(PlayerSessionUser user)
    {
        var machine = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam")?.GetValue("InstallPath") as string;
        var fromUser = Registry.Users.OpenSubKey($@"{user.Sid}\Software\Valve\Steam")?.GetValue("SteamPath") as string;
        var directory = machine ?? fromUser?.Replace('/', '\\');
        return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory) ? directory : null;
    }

    public bool DeleteUserValue(PlayerSessionUser user, string subKey, string valueName)
    {
        using var key = Registry.Users.OpenSubKey($@"{user.Sid}\{subKey}", writable: true);
        if (key?.GetValue(valueName) is null)
        {
            return false;
        }

        key.DeleteValue(valueName, throwOnMissingValue: false);
        return true;
    }

    public bool SetUserDword(PlayerSessionUser user, string subKey, string valueName, int value)
    {
        using var key = Registry.Users.OpenSubKey($@"{user.Sid}\{subKey}", writable: true);
        if (key is null)
        {
            return false;
        }

        key.SetValue(valueName, value, RegistryValueKind.DWord);
        return true;
    }

    private static string? ProfileDirectory(string sid)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList\{sid}");
        return key?.GetValue("ProfileImagePath") is string path && path.Length > 0
            ? Environment.ExpandEnvironmentVariables(path)
            : null;
    }

    private static string? ProcessOwnerSid(IntPtr process)
    {
        if (!OpenProcessToken(process, NativeMethods.TokenQuery, out var token))
        {
            return null;
        }

        try
        {
            return TokenUserSid(token);
        }
        finally
        {
            NativeMethods.CloseHandle(token);
        }
    }

    private static string? TokenUserSid(IntPtr token)
    {
        GetTokenInformation(token, TokenUserClass, IntPtr.Zero, 0, out var length);
        if (length <= 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!GetTokenInformation(token, TokenUserClass, buffer, length, out _))
            {
                return null;
            }

            // TOKEN_USER начинается с SID_AND_ATTRIBUTES: первым полем — указатель на SID.
            return new SecurityIdentifier(Marshal.ReadIntPtr(buffer)).Value;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string? ImagePath(IntPtr process)
    {
        var builder = new StringBuilder(1024);
        var size = builder.Capacity;
        return QueryFullProcessImageName(process, 0, builder, ref size) ? builder.ToString(0, size) : null;
    }

    private static DateTimeOffset? StartTime(IntPtr process) =>
        GetProcessTimes(process, out var creation, out _, out _, out _)
            ? DateTimeOffset.FromFileTime(creation)
            : null;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returnLength);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);
}
