using System.Diagnostics;
using System.Runtime.InteropServices;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

public interface IPlayerShellProcessQuery
{
    bool IsRunning(string executablePath, int sessionId);
}

public interface IPlayerShellProcessStarter
{
    void Start(string executablePath, string arguments, PlayerShellLaunchTarget launchTarget);
}

public interface IPlayerShellLaunchContext
{
    PlayerShellLaunchTarget? GetActiveUserSession();
}

public sealed record PlayerShellLaunchTarget(int SessionId, bool IsCurrentProcessSession);

public sealed class PlayerShellProcessSupervisor(
    IOptions<AgentOptions> options,
    IPlayerShellProcessQuery processQuery,
    IPlayerShellProcessStarter processStarter,
    IPlayerShellLaunchContext launchContext,
    ILogger<PlayerShellProcessSupervisor> logger) : IPlayerShellProcessSupervisor
{
    private static readonly HashSet<string> StatesRequiringShell = new(StringComparer.Ordinal)
    {
        PlayerShellStateNames.Locked,
        PlayerShellStateNames.Active,
        PlayerShellStateNames.Grace,
        PlayerShellStateNames.Ending,
        PlayerShellStateNames.Maintenance,
        PlayerShellStateNames.Offline,
        PlayerShellStateNames.Error
    };

    public Task EnsureRunningAsync(AgentRuntimeState runtimeState, CancellationToken cancellationToken)
    {
        if (!StatesRequiringShell.Contains(runtimeState.State))
        {
            return Task.CompletedTask;
        }

        if (!options.Value.PlayerShellAutoStartEnabled)
        {
            logger.LogDebug("Player Shell auto-start is disabled by configuration; state publishing remains active.");
            return Task.CompletedTask;
        }

        var launchTarget = launchContext.GetActiveUserSession();
        if (launchTarget is null)
        {
            logger.LogInformation("Player Shell auto-start skipped because no active interactive user session is available.");
            return Task.CompletedTask;
        }

        var executablePath = options.Value.PlayerShellExecutablePath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            logger.LogDebug("Player Shell executable path is not configured.");
            return Task.CompletedTask;
        }

        if (!File.Exists(executablePath))
        {
            logger.LogWarning("Player Shell executable path {ExecutablePath} does not exist.", executablePath);
            return Task.CompletedTask;
        }

        if (processQuery.IsRunning(executablePath, launchTarget.SessionId))
        {
            return Task.CompletedTask;
        }

        try
        {
            processStarter.Start(executablePath, options.Value.PlayerShellStartArguments, launchTarget);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Player Shell process start failed for {ExecutablePath} in session {SessionId}.",
                executablePath,
                launchTarget.SessionId);
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "Player Shell process start requested for {ExecutablePath} in session {SessionId}.",
            executablePath,
            launchTarget.SessionId);

        return Task.CompletedTask;
    }
}

public sealed class PlayerShellLaunchContext : IPlayerShellLaunchContext
{
    public PlayerShellLaunchTarget? GetActiveUserSession()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Environment.UserInteractive && Process.GetCurrentProcess().SessionId != 0
                ? new PlayerShellLaunchTarget(Process.GetCurrentProcess().SessionId, IsCurrentProcessSession: true)
                : null;
        }

        var activeSessionId = NativeMethods.WTSGetActiveConsoleSessionId();
        if (activeSessionId == NativeMethods.NoActiveSession)
        {
            return null;
        }

        var currentSessionId = Process.GetCurrentProcess().SessionId;
        return new PlayerShellLaunchTarget(
            checked((int)activeSessionId),
            Environment.UserInteractive && currentSessionId == activeSessionId);
    }
}

public sealed class PlayerShellProcessQuery : IPlayerShellProcessQuery
{
    public bool IsRunning(string executablePath, int sessionId)
    {
        var processName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var processes = Process.GetProcessesByName(processName);
        try
        {
            foreach (var process in processes)
            {
                if (process.SessionId == sessionId && IsMatchingProcess(process, executablePath))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static bool IsMatchingProcess(Process process, string executablePath)
    {
        try
        {
            var processPath = process.MainModule?.FileName;
            return string.Equals(processPath, executablePath, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }
}

public sealed class PlayerShellProcessStarter : IPlayerShellProcessStarter
{
    public void Start(string executablePath, string arguments, PlayerShellLaunchTarget launchTarget)
    {
        if (launchTarget.IsCurrentProcessSession)
        {
            StartInCurrentSession(executablePath, arguments);
            return;
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Starting Player Shell in another user session requires Windows.");
        }

        WindowsInteractiveProcessLauncher.Start(executablePath, arguments, launchTarget.SessionId);
    }

    private static void StartInCurrentSession(string executablePath, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
        };

        Process.Start(startInfo);
    }
}

internal static class WindowsInteractiveProcessLauncher
{
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint TokenAccess =
        NativeMethods.StandardRightsRequired |
        NativeMethods.TokenAssignPrimary |
        NativeMethods.TokenDuplicate |
        NativeMethods.TokenImpersonate |
        NativeMethods.TokenQuery |
        NativeMethods.TokenAdjustDefault |
        NativeMethods.TokenAdjustSessionId;
    private const uint CreateUnicodeEnvironment = 0x00000400;

    public static void Start(string executablePath, string arguments, int sessionId)
    {
        var userToken = IntPtr.Zero;
        var primaryToken = IntPtr.Zero;
        var environmentBlock = IntPtr.Zero;

        try
        {
            if (!NativeMethods.WTSQueryUserToken((uint)sessionId, out userToken))
            {
                ThrowLastWin32Exception("query active user token");
            }

            if (!NativeMethods.DuplicateTokenEx(
                    userToken,
                    TokenAccess,
                    IntPtr.Zero,
                    SecurityImpersonation,
                    TokenPrimary,
                    out primaryToken))
            {
                ThrowLastWin32Exception("duplicate active user token");
            }

            if (!NativeMethods.CreateEnvironmentBlock(out environmentBlock, primaryToken, inherit: false))
            {
                ThrowLastWin32Exception("create Player Shell environment block");
            }

            var startupInfo = new NativeMethods.StartupInfo
            {
                cb = Marshal.SizeOf<NativeMethods.StartupInfo>(),
                lpDesktop = @"winsta0\default"
            };

            var commandLine = QuoteArgument(executablePath);
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                commandLine += " " + arguments;
            }

            var processInformation = new NativeMethods.ProcessInformation();
            var workingDirectory = Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory;
            if (!NativeMethods.CreateProcessAsUser(
                    primaryToken,
                    executablePath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    inheritHandles: false,
                    CreateUnicodeEnvironment,
                    environmentBlock,
                    workingDirectory,
                    ref startupInfo,
                    out processInformation))
            {
                ThrowLastWin32Exception("start Player Shell in the active user session");
            }

            NativeMethods.CloseHandle(processInformation.hProcess);
            NativeMethods.CloseHandle(processInformation.hThread);
        }
        finally
        {
            if (environmentBlock != IntPtr.Zero)
            {
                NativeMethods.DestroyEnvironmentBlock(environmentBlock);
            }

            if (primaryToken != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(primaryToken);
            }

            if (userToken != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(userToken);
            }
        }
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static void ThrowLastWin32Exception(string operation)
    {
        var error = Marshal.GetLastWin32Error();
        throw new InvalidOperationException(
            $"Could not {operation}. Win32 error {error}.",
            new System.ComponentModel.Win32Exception(error));
    }
}

internal static partial class NativeMethods
{
    public const uint NoActiveSession = 0xFFFFFFFF;
    public const uint StandardRightsRequired = 0x000F0000;
    public const uint TokenAssignPrimary = 0x0001;
    public const uint TokenDuplicate = 0x0002;
    public const uint TokenImpersonate = 0x0004;
    public const uint TokenQuery = 0x0008;
    public const uint TokenAdjustDefault = 0x0080;
    public const uint TokenAdjustSessionId = 0x0100;

    [DllImport("kernel32.dll")]
    public static extern uint WTSGetActiveConsoleSessionId();

    [DllImport("wtsapi32.dll", SetLastError = true)]
    public static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        IntPtr tokenAttributes,
        int impersonationLevel,
        int tokenType,
        out IntPtr newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    public static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    public static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessAsUser(
        IntPtr token,
        string applicationName,
        string commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct StartupInfo
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }
}
