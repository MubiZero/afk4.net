using AFK4.Agent.Service.Cleanup;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Tests.Cleanup;

public sealed class SessionTraceCatalogTests
{
    private static readonly string Profile = Path.Combine(Path.GetTempPath(), "Users", "AFK4 Player");
    private static readonly string Steam = Path.Combine(Path.GetTempPath(), "Program Files (x86)", "Steam");

    [Fact]
    public void EveryPath_StaysInsideTheProfileOrSteam()
    {
        var targets = SessionTraceCatalog.Targets(SessionTraceNames.All, Profile, Steam);

        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.True(
            SessionTraceCatalog.IsInside(target.Path, Profile) || SessionTraceCatalog.IsInside(target.Path, Steam) || target.Path == Steam,
            target.Path));
    }

    // Сохранения не трогаются (§6.4): ни одного пути в «Документы» и «Сохранённые игры».
    [Fact]
    public void NoPath_ReachesSaveGames()
    {
        var targets = SessionTraceCatalog.Targets(SessionTraceNames.All, Profile, Steam);

        Assert.DoesNotContain(targets, target =>
            target.Path.Contains("Documents", StringComparison.OrdinalIgnoreCase) ||
            target.Path.Contains("Saved Games", StringComparison.OrdinalIgnoreCase) ||
            target.Path.Contains("steamapps", StringComparison.OrdinalIgnoreCase) ||
            target.Path.Contains("userdata", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Steam_ForgetsTheAccounts_AndItsLoginCache()
    {
        var targets = SessionTraceCatalog.Targets([SessionTraceNames.Steam], Profile, Steam);

        Assert.Contains(targets, target => target.Kind == TraceTargetKind.File && target.Path == Path.Combine(Steam, "config", "loginusers.vdf"));
        Assert.Contains(targets, target => target.Kind == TraceTargetKind.SteamConnectCache && target.Path == Path.Combine(Steam, "config", "config.vdf"));
        Assert.Contains(targets, target => target.Kind == TraceTargetKind.FilesMatching && target.Pattern == "ssfn*");
        Assert.Contains(SessionTraceCatalog.RegistryTraces([SessionTraceNames.Steam]), trace => trace.ValueName == "AutoLoginUser");
        Assert.Contains("steam.exe", SessionTraceCatalog.ProcessesToClose([SessionTraceNames.Steam]));
    }

    [Fact]
    public void WithoutSteam_OnlyItsBrowserCacheInTheProfileIsLeft()
    {
        var target = Assert.Single(SessionTraceCatalog.Targets([SessionTraceNames.Steam], Profile, steamDirectory: null));

        Assert.True(SessionTraceCatalog.IsInside(target.Path, Profile));
    }

    [Fact]
    public void ATurnedOffItem_ClearsNothing_AndClosesNothing()
    {
        Assert.Empty(SessionTraceCatalog.Targets([], Profile, Steam));
        Assert.Empty(SessionTraceCatalog.ProcessesToClose([]));
        Assert.Empty(SessionTraceCatalog.RegistryTraces([SessionTraceNames.Browsers]));
    }

    [Fact]
    public void IsInside_RefusesNeighboursRootsAndEscapes()
    {
        var root = Path.Combine(Path.GetTempPath(), "Users", "AFK4 Player");

        Assert.True(SessionTraceCatalog.IsInside(Path.Combine(root, "AppData", "Local"), root));
        Assert.False(SessionTraceCatalog.IsInside(root, root));
        Assert.False(SessionTraceCatalog.IsInside(root + "2" + Path.DirectorySeparatorChar + "x", root));
        Assert.False(SessionTraceCatalog.IsInside(Path.Combine(root, "..", "Admin", "x"), root));
        Assert.False(SessionTraceCatalog.IsInside(Path.Combine(root, "x"), Path.GetPathRoot(root)!));
        Assert.False(SessionTraceCatalog.IsInside(Path.Combine(root, "x"), string.Empty));
    }
}
