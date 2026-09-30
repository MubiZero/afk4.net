using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AFK4.Agent.Service;

/// <summary>
/// Права на папку данных AFK4 (%ProgramData%\AFK4). По умолчанию она наследует права ProgramData, а там
/// обычные пользователи — и учётка игрока — могут заводить свои файлы. Агент читает из этой папки своё
/// состояние, поэтому писать в неё должны только система и администраторы; игроку — только чтение.
/// Единственное исключение — logs\player: туда пишет журнал сама оболочка, работающая от имени игрока.
/// Выставляется при каждом старте службы: так права приходят и на уже установленные ПК.
/// </summary>
public static class AgentDataFolder
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AFK4");

    public static string PlayerLogDirectory { get; } = Path.Combine(Root, "logs", "player");

    [SupportedOSPlatform("windows")]
    public static void ApplyRights(string root)
    {
        var system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var administrators = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        var rootInfo = Directory.CreateDirectory(root);
        var security = new DirectorySecurity();
        security.SetOwner(administrators);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        rootInfo.SetAccessControl(security);

        var playerLogs = Directory.CreateDirectory(Path.Combine(root, "logs", "player"));
        var playerSecurity = playerLogs.GetAccessControl();
        playerSecurity.AddAccessRule(new FileSystemAccessRule(users, FileSystemRights.Modify, inherit, PropagationFlags.None, AccessControlType.Allow));
        playerLogs.SetAccessControl(playerSecurity);
    }
}
