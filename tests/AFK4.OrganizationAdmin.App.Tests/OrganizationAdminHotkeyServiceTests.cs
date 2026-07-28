using System.Windows.Input;
using AFK4.OrganizationAdmin.App.Hotkeys;
using AFK4.OrganizationAdmin.App.Shell;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminHotkeyServiceTests
{
    [Fact]
    public void Resolve_ReturnsOnlyEnabledCommandForCurrentWorkspace()
    {
        var service = new OrganizationAdminHotkeyService();
        var command = new RecordingCommand(canExecute: true);
        service.Register(OrganizationAdminWorkspaceKind.FloorMap, "F2", command);

        var resolved = service.Resolve(OrganizationAdminWorkspaceKind.FloorMap, "F2");

        Assert.Same(command, resolved);
    }

    [Fact]
    public void Resolve_DoesNotReturnDisabledCommand()
    {
        var service = new OrganizationAdminHotkeyService();
        var command = new RecordingCommand(canExecute: false);
        service.Register(OrganizationAdminWorkspaceKind.FloorMap, "F2", command);

        var resolved = service.Resolve(OrganizationAdminWorkspaceKind.FloorMap, "F2");

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_DoesNotLeakWorkspaceCommand()
    {
        var service = new OrganizationAdminHotkeyService();
        var command = new RecordingCommand(canExecute: true);
        service.Register(OrganizationAdminWorkspaceKind.FloorMap, "F2", command);

        var resolved = service.Resolve(OrganizationAdminWorkspaceKind.Pos, "F2");

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_FallsBackToGlobalCommand()
    {
        var service = new OrganizationAdminHotkeyService();
        var command = new RecordingCommand(canExecute: true);
        service.RegisterGlobal("Ctrl+L", command);

        var resolved = service.Resolve(OrganizationAdminWorkspaceKind.Players, "ctrl + l");

        Assert.Same(command, resolved);
    }

    [Fact]
    public void TryExecute_ExecutesResolvedCommandOnce()
    {
        var service = new OrganizationAdminHotkeyService();
        var command = new RecordingCommand(canExecute: true);
        service.Register(OrganizationAdminWorkspaceKind.Shifts, "F8", command);

        var executed = service.TryExecute(OrganizationAdminWorkspaceKind.Shifts, "F8");

        Assert.True(executed);
        Assert.Equal(1, command.ExecuteCount);
    }

    private sealed class RecordingCommand(bool canExecute) : ICommand
    {
        public int ExecuteCount { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return canExecute;
        }

        public void Execute(object? parameter)
        {
            ExecuteCount++;
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
