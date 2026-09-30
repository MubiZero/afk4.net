using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AFK4.Agent.Service.Tests;

// Папка данных AFK4 не должна наследовать права ProgramData: там обычные пользователи заводят файлы,
// а агент читает из папки своё состояние. Писать — только системе и администраторам; игроку остаётся
// logs\player для журнала оболочки.
[SupportedOSPlatform("windows")]
public sealed class AgentDataFolderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"afk4-data-{Guid.NewGuid():N}");
    private static readonly SecurityIdentifier Users = new(WellKnownSidType.BuiltinUsersSid, null);

    [WindowsOnlyFact]
    public void UsersCanOnlyReadTheDataFolder()
    {
        AgentDataFolder.ApplyRights(root);

        var security = new DirectoryInfo(root).GetAccessControl();
        Assert.True(security.AreAccessRulesProtected);
        var usersRights = UsersRights(security);
        Assert.Equal(FileSystemRights.ReadAndExecute, usersRights & ~FileSystemRights.Synchronize);
        Assert.Equal(0, (int)(usersRights & (FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories | FileSystemRights.Write)));
    }

    [WindowsOnlyFact]
    public void PlayerLogFolderStaysWritableForTheShell()
    {
        AgentDataFolder.ApplyRights(root);

        var security = new DirectoryInfo(Path.Combine(root, "logs", "player")).GetAccessControl();
        Assert.True(UsersRights(security).HasFlag(FileSystemRights.Modify));
    }

    private static FileSystemRights UsersRights(DirectorySecurity security) =>
        security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>()
            .Where(rule => rule.IdentityReference.Equals(Users) && rule.AccessControlType == AccessControlType.Allow)
            .Aggregate(default(FileSystemRights), (all, rule) => all | rule.FileSystemRights);

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
