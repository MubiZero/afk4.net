using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using AFK4.Agent.Service.Cleanup;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests.Cleanup;

/// <summary>
/// Настоящая Windows: уборка видит процессы своего пользователя с путём и временем старта и умеет
/// их закрыть. Консольного пользователя на раннере может не быть — поэтому пользователь здесь тот,
/// под кем идут тесты.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsPlayerSessionHostTests
{
    [WindowsOnlyFact]
    public void SeesTheUsersProcess_WithItsPath_AndClosesIt()
    {
        var host = new WindowsPlayerSessionHost(Options.Create(new AgentOptions()));
        using var identity = WindowsIdentity.GetCurrent();
        var user = new PlayerSessionUser(
            Process.GetCurrentProcess().SessionId,
            identity.User!.Value,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        using var child = Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "ping.exe"),
            Arguments = "-n 60 127.0.0.1",
            CreateNoWindow = true,
            UseShellExecute = false
        })!;

        try
        {
            var seen = Assert.Single(host.Processes(user), process => process.ProcessId == child.Id);
            Assert.Equal("PING.EXE", seen.ImageName.ToUpperInvariant());
            Assert.Equal(Path.Combine(Environment.SystemDirectory, "ping.exe"), seen.ExecutablePath, ignoreCase: true);
            Assert.True(seen.StartedAtUtc >= before, $"started {seen.StartedAtUtc}, test began {before}");
            // Windows защищена от уборки: ping из System32 она не закрыла бы.
            Assert.Contains(host.ProtectedRoots, root => SessionTraceCatalog.IsInside(seen.ExecutablePath!, root));

            Assert.True(host.TryTerminate(child.Id));
            Assert.True(child.WaitForExit(5000));
            Assert.False(host.IsRunning(child.Id));
        }
        finally
        {
            if (!child.HasExited)
            {
                child.Kill();
            }
        }
    }

    [WindowsOnlyFact]
    public void AnotherUsersSid_SeesNothing()
    {
        var host = new WindowsPlayerSessionHost(Options.Create(new AgentOptions()));
        var stranger = new PlayerSessionUser(Process.GetCurrentProcess().SessionId, "S-1-5-21-1-2-3-500", @"C:\Users\Nobody");

        Assert.Empty(host.Processes(stranger));
    }
}
