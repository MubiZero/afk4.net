using AFK4.Agent.Service.Cleanup;

namespace AFK4.Agent.Service.Tests.Games;

/// <summary>Консоль Windows с заданным списком процессов игрока; снятый процесс пропадает из списка.</summary>
internal sealed class FakePlayerSessionHost : IPlayerSessionHost
{
    public const string Windows = @"C:\Windows";
    public const string Afk4 = @"C:\Program Files\AFK4";

    public List<SessionProcess> Running { get; } = [];

    public List<int> Terminated { get; } = [];

    public bool SignedIn { get; set; } = true;

    public bool IsSupported => true;

    public IReadOnlyList<string> ProtectedRoots => [Windows, Afk4];

    public PlayerSessionUser? ConsoleUser() => SignedIn ? new PlayerSessionUser(1, "S-1-5-21-1", @"C:\Users\AFK4 Player") : null;

    public IReadOnlyList<SessionProcess> Processes(PlayerSessionUser user) => Running.ToList();

    public bool TryTerminate(int processId)
    {
        Terminated.Add(processId);
        Running.RemoveAll(process => process.ProcessId == processId);
        return true;
    }

    public bool IsRunning(int processId) => Running.Any(process => process.ProcessId == processId);

    public string? SteamDirectory(PlayerSessionUser user) => null;

    public bool DeleteUserValue(PlayerSessionUser user, string subKey, string valueName) => false;

    public bool SetUserDword(PlayerSessionUser user, string subKey, string valueName, int value) => false;
}
