using System.Windows.Input;
using AFK4.OrganizationAdmin.App.Shell;

namespace AFK4.OrganizationAdmin.App.Hotkeys;

public sealed class OrganizationAdminHotkeyService
{
    private readonly Dictionary<HotkeyKey, ICommand> workspaceCommands = [];
    private readonly Dictionary<string, ICommand> globalCommands = new(StringComparer.OrdinalIgnoreCase);

    public void Register(OrganizationAdminWorkspaceKind workspace, string gesture, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        workspaceCommands[new HotkeyKey(workspace, Normalize(gesture))] = command;
    }

    public void RegisterGlobal(string gesture, ICommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        globalCommands[Normalize(gesture)] = command;
    }

    public ICommand? Resolve(OrganizationAdminWorkspaceKind workspace, string gesture)
    {
        var normalizedGesture = Normalize(gesture);
        if (workspaceCommands.TryGetValue(new HotkeyKey(workspace, normalizedGesture), out var workspaceCommand) &&
            workspaceCommand.CanExecute(null))
        {
            return workspaceCommand;
        }

        return globalCommands.TryGetValue(normalizedGesture, out var globalCommand) &&
            globalCommand.CanExecute(null)
            ? globalCommand
            : null;
    }

    public bool TryExecute(OrganizationAdminWorkspaceKind workspace, string gesture)
    {
        var command = Resolve(workspace, gesture);
        if (command is null)
        {
            return false;
        }

        command.Execute(null);
        return true;
    }

    private static string Normalize(string gesture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gesture);
        return string.Join(
                "+",
                gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToUpperInvariant();
    }

    private readonly record struct HotkeyKey(OrganizationAdminWorkspaceKind Workspace, string Gesture);
}
