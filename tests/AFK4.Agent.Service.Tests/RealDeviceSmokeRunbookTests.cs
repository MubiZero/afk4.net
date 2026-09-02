namespace AFK4.Agent.Service.Tests;

public sealed class RealDeviceSmokeRunbookTests
{
    [Fact]
    public void AgentServiceHost_IsConfiguredForWindowsServiceRuntime()
    {
        var project = File.ReadAllText(RepositoryPath("src/AFK4.Agent.Service/AFK4.Agent.Service.csproj"));
        var program = File.ReadAllText(RepositoryPath("src/AFK4.Agent.Service/Program.cs"));

        Assert.Contains("Microsoft.Extensions.Hosting.WindowsServices", project, StringComparison.Ordinal);
        Assert.Contains("AddWindowsService", program, StringComparison.Ordinal);
        Assert.Contains("AFK4.Agent.Service", program, StringComparison.Ordinal);
    }

    [Fact]
    public void RealDeviceSmokeRunbook_CoversStagingWindowsPcFlowAndEvidence()
    {
        var runbook = NormalizeLineEndings(File.ReadAllText(RepositoryPath("docs/operations/real-device-windows-pc-smoke.md")));

        Assert.Contains("https://api.afk4.net", runbook, StringComparison.Ordinal);
        Assert.Contains("Windows 10/11", runbook, StringComparison.Ordinal);
        Assert.Contains("POST /api/organizations/{organizationId}/install/discover", runbook, StringComparison.Ordinal);
        Assert.Contains("POST /api/organizations/{organizationId}/install/seats", runbook, StringComparison.Ordinal);
        Assert.Contains("POST /api/organizations/{organizationId}/install/enroll", runbook, StringComparison.Ordinal);
        Assert.Contains("POST /api/devices/{deviceId}/heartbeat", runbook, StringComparison.Ordinal);
        Assert.Contains("/hubs/devices", runbook, StringComparison.Ordinal);
        Assert.Contains("POST /api/organizations/{organizationId}/branches/{branchId}/sessions/start", runbook, StringComparison.Ordinal);
        Assert.Contains("POST /api/organizations/{organizationId}/sessions/{sessionId}/end", runbook, StringComparison.Ordinal);
        Assert.Contains("refresh-session-lease", runbook, StringComparison.Ordinal);
        Assert.Contains("lease expiry", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("lock/unlock", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Player Shell", runbook, StringComparison.Ordinal);
        Assert.Contains("installed apps", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GET /api/organizations/{organizationId}/branches/{branchId}/diagnostics", runbook, StringComparison.Ordinal);
        Assert.Contains("POST /api/devices/{deviceId}/updates/check", runbook, StringComparison.Ordinal);
        Assert.Contains("Pass criteria", runbook, StringComparison.Ordinal);
        Assert.Contains("Evidence to collect", runbook, StringComparison.Ordinal);
        Assert.Contains("Do not commit secrets", runbook, StringComparison.Ordinal);

        Assert.DoesNotContain("curl -k", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("local club server", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("web admin panel", runbook, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Linux service", runbook, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryPath(string relativePath)
    {
        return Path.GetFullPath(Path.Combine(GetRepositoryRoot(), relativePath));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AFK4.sln")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Repository root was not found.");
        }

        return directory.FullName;
    }

    private static string NormalizeLineEndings(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
    }
}
